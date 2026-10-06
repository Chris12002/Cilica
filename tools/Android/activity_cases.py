"""Bounded activity cases for the owned disposable emulator only."""
import json
from pathlib import Path
import re
import time

from build_library import sha256
from package_support import package_files


def validate_trace(trace, cycles):
    expected = ['READY 0']
    for cycle in range(cycles): expected.extend(['BACKGROUND '+str(cycle), 'RESUMED '+str(cycle+1)])
    expected.append('CLOSED')
    if trace.splitlines() != expected:
        raise RuntimeError('Unexpected/duplicate activity callbacks: '+trace)


def activity_files(package, consumer):
    receipt, _ = package_files(package)
    folder = Path(consumer).resolve(strict=True)
    evidence = json.loads((folder/'activity-receipt.json').read_bytes())
    if evidence['profile'] != 'android-activity-consumer-v1' or evidence['packageReceiptSha256'] != sha256((package/'build-receipt.json').read_bytes()):
        raise ValueError('Activity consumer belongs to another package')
    if not re.fullmatch(r'dev\.cilica\.test\.p[0-9a-f]{32}', evidence['appId']) or evidence['cycles'] != 10:
        raise ValueError('Unexpected test app identity/cycle bound')
    module = receipt['module']
    if set(evidence['sources']) != {module+'Runtime.kt', module+'ActivityHost.kt', 'bridge.c', 'MainActivity.kt', 'Scenario.kt'}:
        raise ValueError('Unexpected activity source set')
    for name, fingerprint in {**evidence['sources'], 'consumer.apk': evidence['apkSha256']}.items():
        path = (folder/name).resolve(strict=True)
        if folder not in path.parents or sha256(path.read_bytes()) != fingerprint:
            raise ValueError('Activity file/hash mismatch: '+name)
    return folder, evidence


def run_activity_case(adb, folder, evidence, output):
    app = evidence['appId']
    result = {'appId': app, 'apkSha256': evidence['apkSha256'], 'modes': [], 'passed': False}
    def launch(*extras):
        adb(['shell', 'am', 'start', '-W', '-n', app+'/dev.cilica.MainActivity', *extras])
    for stress in (False, True):
        mode = 'forced' if stress else 'normal'
        entry = {'mode': mode, 'uninstalled': False}
        result['modes'].append(entry)
        if adb(['shell', 'pm', 'path', app], check=False).stdout.strip():
            raise RuntimeError('Refusing to replace an existing test app')
        installed = False
        trace = ''
        def await_marker(marker):
            nonlocal trace
            deadline = time.monotonic()+25
            while True:
                trace = adb(['shell', 'run-as', app, 'cat', 'files/trace.txt'], timeout=10, check=False).stdout
                (output/(mode+'.trace')).write_text(trace)
                if any(line.startswith('FAIL ') for line in trace.splitlines()):
                    raise RuntimeError('Activity scenario failed: '+trace)
                if marker in trace.splitlines():
                    return
                if time.monotonic() >= deadline:
                    raise TimeoutError('Activity marker missing: '+marker)
                time.sleep(0.25)
        try:
            installed = True # The unique ID was absent; also clean up an uncertain install.
            adb(['install', str(folder/'consumer.apk')], timeout=60)
            launch('--ez', 'stress', str(stress).lower())
            await_marker('READY 0')
            for cycle in range(10):
                adb(['shell', 'input', 'keyevent', 'KEYCODE_HOME'])
                await_marker('BACKGROUND '+str(cycle))
                launch()
                await_marker('RESUMED '+str(cycle+1))
            adb(['shell', 'am', 'broadcast', '-a', app+'.CLOSE', '-p', app])
            await_marker('CLOSED')
            validate_trace(trace, 10)
            entry['cycles'] = 10
        except BaseException as error:
            entry['error'] = str(error)
            log = adb(['logcat', '-d', '-t', '500'], check=False)
            (output/(mode+'.logcat')).write_text(log.stdout+log.stderr)
            raise
        finally:
            if installed:
                uninstall = adb(['uninstall', app], timeout=30, check=False)
                entry['uninstalled'] = uninstall.returncode == 0 and not adb(['shell', 'pm', 'path', app], check=False).stdout.strip()
            (output/'activity-run.json').write_text(json.dumps(result, indent=2)+'\n')
        if not entry['uninstalled']:
            raise RuntimeError('Activity cleanup did not complete')
        print('Activity '+app+' '+mode+': ten background/resume cycles and explicit close passed', flush=True)
    result['passed'] = True
    (output/'activity-run.json').write_text(json.dumps(result, indent=2)+'\n')
    return result
