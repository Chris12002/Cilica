#!/usr/bin/env python3
"""Build a uniquely identified, locally signed APK for bounded activity tests."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import uuid
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[2]/'adapters/Android'))
from generate import generate
from package_support import jvm_case_files, package_files
from build_library import sha256


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['package', 'consumer', 'sdk', 'java-home', 'kotlin-home', 'scenario', 'output']:
        parser.add_argument('--'+name, type=Path, required=True)
    args = parser.parse_args()
    package = args.package.resolve(strict=True)
    receipt, library = package_files(package)
    _, files, _ = jvm_case_files(package, args.consumer)
    bridge = next(p for p in files if p.suffix == '.so')
    module = receipt['module']
    root = Path(__file__).resolve().parent
    pin = json.loads((root.parents[1]/'adapters/Android/toolchain.json').read_bytes())
    sdk = args.sdk.resolve(strict=True); java = args.java_home.resolve(strict=True); kotlin = args.kotlin_home.resolve(strict=True)
    version = subprocess.check_output([str(java/'bin/java'), '-version'], stderr=subprocess.STDOUT, text=True, timeout=10)
    if version.splitlines()[0] != pin['javaVersion'] or (kotlin/'build.txt').read_text().strip() != pin['kotlinBuild']:
        raise ValueError('JVM or Kotlin differs from pin')
    tools = sdk/'build-tools'/pin['buildToolsRevision']
    if '\nPkg.Revision='+pin['buildToolsRevision']+'\n' not in '\n'+(tools/'source.properties').read_text()+'\n':
        raise ValueError('Build tools differ from pin')
    android = sdk/('platforms/android-'+str(pin['compileAPI'])+'/android.jar')
    sources = generate(json.loads((package/'contract.json').read_bytes()), module, True)
    # Reuse JNI only when it matches the generated bridge and runtime exactly.
    for name in ['bridge.c', module+'Runtime.kt']:
        if (args.consumer/name).read_text() != sources[name]:
            raise ValueError('Rebuild JNI consumer before activity test: '+name)
    sources['MainActivity.kt'] = (root/'Activity.kt.in').read_text().replace('@MODULE@', module)
    sources['Scenario.kt'] = args.scenario.read_text()
    output = args.output.resolve(); output.mkdir(parents=True, exist_ok=False)
    app_id = 'dev.cilica.test.p'+uuid.uuid4().hex
    manifest = f'''<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="{app_id}">
<uses-sdk android:minSdkVersion="26" android:targetSdkVersion="36"/>
<application android:debuggable="true" android:extractNativeLibs="true" android:label="Cilica Test" android:theme="@android:style/Theme.Material.Light.NoActionBar">
<activity android:name="dev.cilica.MainActivity" android:exported="true" android:launchMode="singleTop"/>
</application></manifest>'''
    (output/'AndroidManifest.xml').write_text(manifest)
    for name, text in sources.items(): (output/name).write_text(text)
    env = os.environ.copy(); env['JAVA_HOME'] = str(java)
    commands = []
    def run(command, name):
        command = list(map(str, command)); commands.append(command)
        with (output/(name+'.log')).open('w') as log:
            subprocess.run(command, env=env, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=180)
    run(['bash', kotlin/'bin/kotlinc', *[output/name for name in sources if name.endswith('.kt')],
         '-classpath', android, '-jvm-target', '1.8', '-Werror', '-include-runtime', '-d', output/'consumer.jar'], 'kotlin')
    dex = output/'dex'; dex.mkdir()
    run([tools/'d8', '--min-api', '26', '--lib', android, '--output', dex, output/'consumer.jar'], 'dex')
    run([tools/'aapt2', 'link', '--manifest', output/'AndroidManifest.xml', '-I', android, '-o', output/'unsigned.apk'], 'aapt')
    with zipfile.ZipFile(output/'unsigned.apk', 'a', compression=zipfile.ZIP_DEFLATED) as apk:
        apk.write(dex/'classes.dex', 'classes.dex')
        for file in [library, bridge]: apk.write(file, 'lib/arm64-v8a/'+file.name)
    run([tools/'zipalign', '-P', '16', '4', output/'unsigned.apk', output/'aligned.apk'], 'align')
    key = output/'test.p12'
    try:
        run([java/'bin/keytool', '-genkeypair', '-storetype', 'PKCS12', '-keystore', key, '-storepass', 'android',
             '-keypass', 'android', '-alias', 'test', '-dname', 'CN=Cilica Local Test', '-keyalg', 'RSA',
             '-keysize', '2048', '-validity', '2'], 'key')
        run([tools/'apksigner', 'sign', '--ks', key, '--ks-pass', 'pass:android', '--out', output/'consumer.apk', output/'aligned.apk'], 'sign')
    finally:
        if key.exists(): key.unlink()
    run([tools/'apksigner', 'verify', '--verbose', output/'consumer.apk'], 'verify')
    evidence = {'profile': 'android-activity-consumer-v1', 'appId': app_id, 'cycles': 10,
                'packageReceiptSha256': sha256((package/'build-receipt.json').read_bytes()),
                'jvmReceiptSha256': sha256((args.consumer/'jvm-receipt.json').read_bytes()),
                'apkSha256': sha256((output/'consumer.apk').read_bytes()),
                'sources': {name: sha256((output/name).read_bytes()) for name in sources},
                'pin': pin, 'commands': commands, 'runtimeExecuted': False}
    (output/'activity-receipt.json').write_text(json.dumps(evidence, indent=2)+'\n')
    print(output)


if __name__ == '__main__': main()
