#!/usr/bin/env python3
"""Build a generated JNI/Kotlin adapter and an explicit JVM test consumer offline."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess

from generate import generate, package_files
from build_library import sha256


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['package', 'ndk', 'sdk', 'java-home', 'kotlin-home', 'source', 'output']:
        parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--main-class', required=True)
    args = parser.parse_args()
    if not re.fullmatch(r'[A-Za-z][A-Za-z0-9]*(?:\.[A-Za-z][A-Za-z0-9]*)*', args.main_class):
        raise ValueError('Invalid JVM main class')
    package = args.package.resolve(strict=True)
    receipt, library = package_files(package)
    root = Path(__file__).resolve().parent
    native_pin = json.loads((root.parents[1]/'tools/Android/toolchain.json').read_bytes())
    pin = json.loads((root/'toolchain.json').read_bytes())
    ndk = args.ndk.resolve(strict=True); java = args.java_home.resolve(strict=True)
    kotlin = args.kotlin_home.resolve(strict=True); sdk = args.sdk.resolve(strict=True)
    if receipt['toolchain'] != native_pin:
        raise ValueError('Package NDK pin differs')
    revision = re.search(r'^Pkg.Revision\s*=\s*(\S+)', (ndk/'source.properties').read_text(), re.MULTILINE)
    if not revision or revision[1] != native_pin['ndkRevision']:
        raise ValueError('NDK revision differs')
    clang = ndk/'toolchains/llvm/prebuilt'/native_pin['hostTag']/'bin/clang'
    if subprocess.check_output([str(clang), '--version'], text=True, timeout=10).splitlines()[0] != native_pin['clangVersion']:
        raise ValueError('Clang version differs')
    version = subprocess.check_output([str(java/'bin/java'), '-version'], stderr=subprocess.STDOUT, text=True, timeout=10)
    if version.splitlines()[0] != pin['javaVersion'] or (kotlin/'build.txt').read_text().strip() != pin['kotlinBuild']:
        raise ValueError('JVM or Kotlin pin differs')
    build_tools = sdk/'build-tools'/pin['buildToolsRevision']
    props = (build_tools/'source.properties').read_text()
    if '\nPkg.Revision='+pin['buildToolsRevision']+'\n' not in '\n'+props+'\n':
        raise ValueError('Build tools pin differs')
    sources = generate(json.loads((package/'contract.json').read_bytes()), receipt['module'])
    output = args.output.resolve(); output.mkdir(parents=True, exist_ok=False)
    for name, text in sources.items():
        (output/name).write_text(text)
    (output/'Consumer.kt').write_bytes(args.source.read_bytes())
    module = receipt['module']
    env = os.environ.copy(); env['JAVA_HOME'] = str(java)
    commands = []
    def run(command, name):
        command = list(map(str, command)); commands.append(command)
        with (output/(name+'.log')).open('w') as log:
            subprocess.run(command, env=env, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=180)
    run([clang, '--target='+native_pin['triple']+str(native_pin['minimumAPI']), '-std=c11', '-O2', '-fPIC', '-shared',
         '-fvisibility=hidden', '-Wall', '-Wextra', '-Werror', '-Wframe-larger-than=2048', '-I', package/'include',
         output/'bridge.c', library, '-Wl,--no-undefined', '-Wl,-z,relro,-z,now,-z,noexecstack',
         '-Wl,-z,max-page-size=16384', '-Wl,-z,common-page-size=16384', '-Wl,-soname,lib'+module+'Jni.so',
         '-o', output/('lib'+module+'Jni.so')], 'native')
    run(['bash', kotlin/'bin/kotlinc', output/(module+'Runtime.kt'), output/'Consumer.kt',
         '-jvm-target', '1.8', '-Werror', '-include-runtime', '-d', output/'consumer.jar'], 'kotlin')
    dex = output/'dex'; dex.mkdir()
    run([build_tools/'d8', '--min-api', '26', '--lib', sdk/('platforms/android-'+str(pin['compileAPI'])+'/android.jar'),
         '--output', dex, output/'consumer.jar'], 'dex')
    files = ['lib'+module+'Jni.so', 'dex/classes.dex']
    source_files = [*sources.keys(), 'Consumer.kt']
    evidence = {'profile': 'android-jvm-consumer-v1', 'mainClass': args.main_class,
                'pin': pin, 'commands': commands,
                'packageReceiptSha256': sha256((package/'build-receipt.json').read_bytes()),
                'sources': {name: sha256((output/name).read_bytes()) for name in source_files},
                'files': {name: sha256((output/name).read_bytes()) for name in files}, 'runtimeExecuted': False}
    (output/'jvm-receipt.json').write_text(json.dumps(evidence, indent=2)+'\n')
    print(output)


if __name__ == '__main__':
    main()
