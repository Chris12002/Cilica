#!/usr/bin/env python3
"""Execute pinned arm64 native cases in a fresh, disposable Android emulator."""
import argparse
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import socket
import subprocess
import time
import uuid

from build_library import sha256
from package_support import case_files, jvm_case_files


def properties(path):
    return dict(line.split('=', 1) for line in
                (line.strip() for line in path.read_text().splitlines())
                if '=' in line and not line.startswith('#'))


def free_port_pair():
    # Probe both ports; the emulator itself arbitrates any race after release.
    for port in range(5554, 5683, 2):
        with socket.socket() as console, socket.socket() as adb:
            try:
                console.bind(('127.0.0.1', port))
                adb.bind(('127.0.0.1', port + 1))
                return port
            except OSError:
                pass
    raise RuntimeError('No free emulator port pair')


def stop_child(child):
    if child is None:
        return
    if child.poll() is None:
        child.terminate()
        try:
            child.wait(timeout=15)
        except subprocess.TimeoutExpired:
            child.kill()
            child.wait(timeout=10)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sdk', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--case', nargs=2, action='append', default=[],
                        metavar=('PACKAGE', 'CONSUMER'), help='CONSUMER: lifecycle or build_consumer output')
    parser.add_argument('--jvm-case', nargs=2, action='append', default=[], metavar=('PACKAGE', 'CONSUMER'),
                        help='CONSUMER: adapters/Android/build.py output, run with CheckJNI')
    args = parser.parse_args(argv)
    if not args.case and not args.jvm_case:
        parser.error('At least one --case or --jvm-case is required')
    sdk = args.sdk.resolve(strict=True)
    pin = json.loads(Path(__file__).with_name('emulator.json').read_bytes())
    emulator = sdk/'emulator/emulator'
    adb = sdk/'platform-tools/adb'
    image = sdk/pin['image']
    emulator_props = {k.strip(): v.strip() for k, v in properties(emulator.parent/'source.properties').items()}
    image_props = {k.strip(): v.strip() for k, v in properties(image/'source.properties').items()}
    if emulator_props['Pkg.Revision'] != pin['emulatorRevision'] or any(
            image_props[k] != pin[v] for k, v in [('Pkg.Revision', 'imageRevision'),
            ('AndroidVersion.ApiLevel', 'api'), ('SystemImage.Abi', 'abi')]):
        raise ValueError('Emulator or system image differs from pin')
    adb_version = subprocess.check_output([str(adb), 'version'], text=True, timeout=10)
    if 'Version '+pin['adbVersion'] not in adb_version.splitlines():
        raise ValueError('adb differs from pin')
    cases = []
    for package_name, consumer in args.case:
        package = Path(package_name).resolve(strict=True)
        library, binary = case_files(package, consumer)
        cases.append((package, library, [binary], None))
    for package_name, consumer in args.jvm_case:
        package = Path(package_name).resolve(strict=True)
        library, files, main_class = jvm_case_files(package, consumer)
        cases.append((package, library, files, main_class))
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    scratch = output/'temporary-device'
    avds = scratch/'avd'; avds.mkdir(parents=True)
    name = 'cilica_' + uuid.uuid4().hex
    device = avds/(name+'.avd'); device.mkdir()
    config = '\n'.join([
        'AvdId='+name, 'avd.ini.encoding=UTF-8', 'abi.type=arm64-v8a',
        'hw.cpu.arch=arm64', 'hw.cpu.ncore=2', 'hw.ramSize=2048',
        'hw.lcd.width=720', 'hw.lcd.height=1280', 'hw.lcd.density=320',
        'hw.gpu.enabled=yes', 'hw.gpu.mode=swiftshader_indirect',
        'hw.sdCard=no', 'disk.dataPartition.size=2G', 'vm.heapSize=256',
        'image.sysdir.1='+str(image)+'/', 'target=android-'+pin['api'],
        'PlayStore.enabled=true', 'showDeviceFrame=no', 'fastboot.forceColdBoot=yes'])+'\n'
    (device/'config.ini').write_text(config)
    (output/'device-config.ini').write_text(config)
    (avds/(name+'.ini')).write_text('avd.ini.encoding=UTF-8\npath='+str(device)+'\ntarget=android-'+pin['api']+'\n')
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', 0)); server_port = probe.getsockname()[1]
    console_port = free_port_pair()
    serial = 'emulator-'+str(console_port)
    env = os.environ.copy()
    env.pop('ADB_SERVER_SOCKET', None)
    env.update(ANDROID_HOME=str(sdk), ANDROID_SDK_ROOT=str(sdk),
               ANDROID_USER_HOME=str(scratch/'user'), ANDROID_EMULATOR_HOME=str(scratch/'user'),
               ANDROID_AVD_HOME=str(avds), ANDROID_ADB_SERVER_PORT=str(server_port),
               ADB_VENDOR_KEYS=str(scratch/'user/adbkey'))
    (scratch/'user').mkdir()
    receipt = {'profile': 'android-emulator-native-v1', 'pin': pin, 'passed': False,
               'serial': serial, 'adbPort': server_port, 'cases': [], 'cleanup': {}}
    server = guest = None
    remote = '/data/local/tmp/'+name

    def adb_run(arguments, timeout=30, check=True):
        if server is None or server.poll() is not None:
            raise RuntimeError('Owned adb server is not running')
        command = [str(adb), '-P', str(server_port), '-s', serial] + arguments
        result = subprocess.run(command, env=env, capture_output=True, text=True, timeout=timeout)
        if check and result.returncode:
            raise RuntimeError('adb command failed: '+shlex.join(arguments)+'\n'+result.stdout+result.stderr)
        return result

    try:
        with (output/'adb.log').open('w') as server_log, (output/'emulator.log').open('w') as guest_log:
            server_command = [str(adb), '-L', 'tcp:localhost:'+str(server_port),
                              '--one-device', serial, 'server', 'nodaemon']
            server = subprocess.Popen(server_command, env=env, stdout=server_log, stderr=subprocess.STDOUT)
            receipt['adbPid'] = server.pid
            deadline = time.monotonic()+10
            while True:
                if server.poll() is not None:
                    raise RuntimeError('Owned adb server exited; see adb.log')
                try:
                    with socket.create_connection(('127.0.0.1', server_port), timeout=0.2):
                        break
                except OSError:
                    if time.monotonic() >= deadline:
                        raise TimeoutError('Owned adb server did not start')
                    time.sleep(0.2)
            guest_command = [str(emulator), '-avd', name, '-no-snapshot', '-no-window', '-no-audio',
                             '-gpu', 'swiftshader_indirect', '-port', str(console_port)]
            receipt['commands'] = [server_command, guest_command]
            guest = subprocess.Popen(guest_command, env=env, stdout=guest_log, stderr=subprocess.STDOUT)
            receipt['emulatorPid'] = guest.pid
            deadline = time.monotonic()+180
            while True:
                if guest.poll() is not None:
                    raise RuntimeError('Owned emulator exited; see emulator.log')
                boot = adb_run(['shell', 'getprop', 'sys.boot_completed'], timeout=10, check=False)
                if boot.returncode == 0 and boot.stdout.strip() == '1':
                    break
                if time.monotonic() >= deadline:
                    raise TimeoutError('Emulator boot exceeded 180 seconds')
                time.sleep(1)
            info = {}
            for key in ['ro.build.fingerprint', 'ro.build.version.sdk', 'ro.product.cpu.abi', 'ro.kernel.qemu']:
                info[key] = adb_run(['shell', 'getprop', key]).stdout.strip()
            info['pageSize'] = int(adb_run(['shell', 'getconf', 'PAGE_SIZE']).stdout.strip())
            receipt['device'] = info
            if info['ro.kernel.qemu'] != '1' or info['ro.product.cpu.abi'] != pin['abi'] or info['ro.build.version.sdk'] != pin['api']:
                raise RuntimeError('Unexpected emulator identity')
            print('Booted '+info['ro.build.fingerprint'], flush=True)
            for index, (package, library, files, main_class) in enumerate(cases):
                folder = remote+'/case'+str(index)
                adb_run(['shell', 'mkdir', '-p', folder])
                adb_run(['push', str(library), folder+'/'+library.name], timeout=60)
                for file in files:
                    adb_run(['push', str(file), folder+'/'+(file.name if main_class else 'consumer')], timeout=60)
                command = ['shell', 'timeout', '60', 'env', 'LD_LIBRARY_PATH='+folder]
                if main_class:
                    command += ['CLASSPATH='+folder+'/classes.dex', '/system/bin/app_process', '-Xcheck:jni',
                                '-Djava.library.path='+folder, '/system/bin', main_class]
                else:
                    adb_run(['shell', 'chmod', '700', folder+'/consumer'])
                    command += [folder+'/consumer']
                result = adb_run(command, timeout=70, check=False)
                (output/('case'+str(index)+'.stdout')).write_text(result.stdout)
                (output/('case'+str(index)+'.stderr')).write_text(result.stderr)
                evidence = {'package': str(package), 'mainClass': main_class, 'command': command,
                            'packageReceiptSha256': sha256((package/'build-receipt.json').read_bytes()),
                            'librarySha256': sha256(library.read_bytes()),
                            'files': {str(file): sha256(file.read_bytes()) for file in files},
                            'exitCode': result.returncode, 'stdout': result.stdout, 'stderr': result.stderr}
                receipt['cases'].append(evidence)
                if result.returncode or not re.search(r'^PASS(?:\s|$)', result.stdout, re.MULTILINE):
                    if main_class:
                        diagnostics = adb_run(['logcat', '-d', '-t', '300'], check=False)
                        (output/('case'+str(index)+'.logcat')).write_text(diagnostics.stdout+diagnostics.stderr)
                    raise RuntimeError('Native case '+str(index)+' failed; see case output')
                print('Case '+str(index)+': '+result.stdout.strip(), flush=True)
            adb_run(['shell', 'rm', '-rf', remote])
            receipt['cleanup']['guestFilesRemoved'] = True
            receipt['passed'] = True
    except BaseException as error:
        receipt['passed'] = False
        receipt['error'] = str(error)
        raise
    finally:
        # Never enumerate/kill another process, use a shared adb server, or stop a user's AVD.
        errors = []
        for label, child in [('emulatorStopped', guest), ('adbStopped', server)]:
            try:
                stop_child(child)
                receipt['cleanup'][label] = True
            except Exception as error:
                errors.append(str(error)); receipt['cleanup'][label] = False
        if not errors:
            try:
                shutil.rmtree(scratch)
                receipt['cleanup']['temporaryDeviceRemoved'] = True
            except Exception as error:
                errors.append(str(error))
        if errors:
            receipt['passed'] = False
            receipt['cleanup']['errors'] = errors
        (output/'run-receipt.json').write_text(json.dumps(receipt, indent=2)+'\n')
        if errors:
            raise RuntimeError('Cleanup failed: '+'; '.join(errors))
    print(output/'run-receipt.json')


if __name__ == '__main__':
    main()
