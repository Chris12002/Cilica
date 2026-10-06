#!/usr/bin/env python3
"""Run a prelinked SceneApp consumer on an existing simulator; remove owned apps."""
import argparse
import hashlib
import json
from pathlib import Path
import plistlib
import shutil
import subprocess
import time
import uuid


def wait_for(path, marker, seconds=30):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        text = path.read_text(errors='replace') if path.exists() else ''
        if 'FAIL ' in text:
            raise RuntimeError(text)
        if marker in text:
            return
        time.sleep(0.1)
    raise TimeoutError(f'Missing {marker!r} in {path}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=Path, required=True, help='check_package.py output for SceneApp.swift.in with --adapter --scene-host')
    parser.add_argument('--output', type=Path, required=True, help='new evidence directory')
    parser.add_argument('--simulator', type=uuid.UUID, required=True, help='explicit already-booted simulator UUID')
    args = parser.parse_args()
    build = args.build.resolve(strict=True)
    binary = build / 'iphonesimulator'
    receipt = json.loads((build / 'smoke-receipt.json').read_text())
    if receipt['simulator'] is not None:
        raise ValueError('Use a build-only SceneApp receipt, not a spawned CLI consumer')
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    simulator = str(args.simulator).upper()
    commands = []

    def simctl(*arguments, check=True):
        command = ['xcrun', 'simctl', *arguments]
        try:
            result = subprocess.run(command, capture_output=True, text=True, timeout=30)
        except subprocess.TimeoutExpired:
            commands.append({'command': command, 'timedOut': True})
            raise
        commands.append({'command': command, 'exitCode': result.returncode,
                         'stdout': result.stdout, 'stderr': result.stderr})
        if check and result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
        return result

    devices = json.loads(simctl('list', 'devices', 'booted', '--json').stdout)
    device = next((d for group in devices['devices'].values() for d in group if d['udid'] == simulator), None)
    if not device or device['state'] != 'Booted':
        raise ValueError('Selected simulator must already be booted')
    identifier = 'dev.cilica.sceneprobe.' + uuid.uuid4().hex
    installed = []
    passed = False
    cleanup_ok = True
    cleanup_errors = []
    try:
        for suffix in ('probe', 'cover'):
            bundle = output / (suffix + '.app')
            bundle.mkdir()
            shutil.copy2(binary, bundle / 'Probe')
            info = {'CFBundleIdentifier': identifier + '.' + suffix, 'CFBundleExecutable': 'Probe',
                    'CFBundleName': 'Cilica Scene Probe', 'CFBundlePackageType': 'APPL',
                    'CFBundleVersion': '1', 'CFBundleShortVersionString': '1.0', 'MinimumOSVersion': '17.0',
                    'UIDeviceFamily': [1, 2], 'LSRequiresIPhoneOS': True, 'UILaunchScreen': {},
                    'CilicaCover': suffix == 'cover',
                    'UIApplicationSceneManifest': {'UIApplicationSupportsMultipleScenes': False,
                        'UISceneConfigurations': {'UIWindowSceneSessionRoleApplication': [{'UISceneConfigurationName': 'Probe'}]}}}
            (bundle / 'Info.plist').write_bytes(plistlib.dumps(info))
            installed.append(info['CFBundleIdentifier'])
            simctl('install', simulator, str(bundle))
        for mode in ('normal', 'stress'):
            log = output / (mode + '.txt')
            error = output / (mode + '-stderr.txt')
            flags = ['--stress'] if mode == 'stress' else []
            simctl('launch', '--terminate-running-process', '--stdout=' + str(log), '--stderr=' + str(error),
                   simulator, identifier + '.probe', *flags)
            wait_for(log, 'READY foreground session')
            simctl('launch', simulator, identifier + '.cover')
            wait_for(log, 'PAUSED background copies preserved')
            simctl('launch', simulator, identifier + '.probe')
            wait_for(log, 'PASS UIKit scene lifecycle:')
            text = log.read_text()
            if text.count('UIKIT ACTIVE') < 2 or 'UIKIT BACKGROUND' not in text or 'UIKIT INACTIVE' not in text:
                raise RuntimeError('Missing real UIKit transition evidence')
            simctl('terminate', simulator, identifier + '.probe')
        passed = True
    finally:
        for app in reversed(installed):
            # Only unique IDs created by this run are touched, including recovery
            # after an uncertain install/launch. A stopped process needs no kill.
            for action in ('terminate', 'uninstall'):
                try:
                    result = simctl(action, simulator, app, check=False)
                    if action == 'uninstall' and result.returncode:
                        cleanup_ok = False
                        cleanup_errors.append(f'Uninstall failed: {app}')
                except (subprocess.TimeoutExpired, OSError) as error:
                    cleanup_ok = False
                    cleanup_errors.append(f'{action} {app}: {error}')
        (output / 'scene-receipt.json').write_text(json.dumps({
            'passed': passed, 'cleanupSucceeded': cleanup_ok, 'cleanupErrors': cleanup_errors, 'simulator': device,
            'buildReceiptSha256': hashlib.sha256((build / 'smoke-receipt.json').read_bytes()).hexdigest(),
            'binarySha256': hashlib.sha256(binary.read_bytes()).hexdigest(),
            'bundleIdentifiers': installed, 'commands': commands,
            'physicalDeviceExecuted': False, 'OSSuspensionVerified': False,
        }, indent=2) + '\n')
    if not cleanup_ok:
        raise RuntimeError('Owned app cleanup failed; inspect scene-receipt.json')
    print('PASS UIKit foreground/background checks in normal/stress modes; both owned apps removed.')


if __name__ == '__main__':
    main()
