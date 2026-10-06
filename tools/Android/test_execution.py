import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import Mock, patch

from build_library import sha256
from package_support import case_files, package_files, jvm_case_files
from run_emulator import stop_child
import run_emulator as runner
from activity_cases import activity_files, validate_trace


class ExecutionTests(unittest.TestCase):
    def package(self, root):
        files = ['lib/arm64-v8a/libProbe.so', 'include/cilica.h', 'contract.json', 'NOTICE.toolchain']
        for name in files:
            path = root/name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(name)
        (root/'checks').mkdir()
        (root/'checks/lifecycle').write_bytes(b'consumer')
        receipt = {'profile': 'android-shared-v3-arm64-v1', 'module': 'Probe',
                   'packageFiles': {name: sha256((root/name).read_bytes()) for name in files},
                   'consumerSha256': sha256(b'consumer')}
        (root/'build-receipt.json').write_text(json.dumps(receipt))
        return receipt

    def test_changed_package_and_consumer_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            self.package(root)
            case_files(root, 'lifecycle')
            (root/'checks/lifecycle').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'executable differs'):
                case_files(root, 'lifecycle')
            (root/'include/cilica.h').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'file/hash mismatch'):
                package_files(root)

    def test_receipt_cannot_add_paths_or_escape_package(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            receipt = self.package(root)
            receipt['packageFiles']['../../unrelated'] = sha256(b'')
            (root/'build-receipt.json').write_text(json.dumps(receipt))
            with self.assertRaisesRegex(ValueError, 'file set'):
                package_files(root)
            del receipt['packageFiles']['../../unrelated']
            (root/'build-receipt.json').write_text(json.dumps(receipt))
            (root/'contract.json').unlink()
            (root/'contract.json').symlink_to(Path(__file__).resolve())
            with self.assertRaisesRegex(ValueError, 'file/hash mismatch'):
                package_files(root)

    def test_custom_consumer_must_match_package_and_source(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            self.package(root)
            folder = root/'custom'; folder.mkdir()
            (folder/'consumer.c').write_bytes(b'source')
            (folder/'consumer').write_bytes(b'binary')
            receipt = {'packageReceiptSha256': sha256((root/'build-receipt.json').read_bytes()),
                       'sourceSha256': sha256(b'source'), 'binarySha256': sha256(b'binary')}
            path = folder/'consumer-receipt.json'; path.write_text(json.dumps(receipt))
            case_files(root, str(folder))
            (folder/'consumer.c').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'source changed'):
                case_files(root, str(folder))
            receipt['packageReceiptSha256'] = 'wrong'
            path.write_text(json.dumps(receipt))
            with self.assertRaisesRegex(ValueError, 'different package'):
                case_files(root, str(folder))

    def test_cleanup_is_bounded_and_targets_only_owned_child(self):
        child = Mock()
        child.poll.return_value = None
        child.wait.side_effect = [subprocess.TimeoutExpired('owned', 15), 0]
        stop_child(child)
        child.terminate.assert_called_once_with()
        child.kill.assert_called_once_with()
        self.assertEqual([call.kwargs['timeout'] for call in child.wait.call_args_list], [15, 10])
        stopped = Mock(); stopped.poll.return_value = 0
        stop_child(stopped)
        stopped.terminate.assert_not_called()
        stopped.kill.assert_not_called()
        stop_child(None)

    def test_jvm_receipt_rejects_changed_dex_and_shell_main_names(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve(); self.package(root)
            folder = root/'jvm'; (folder/'dex').mkdir(parents=True)
            files = ['libProbeJni.so', 'dex/classes.dex']
            sources = ['ProbeRuntime.kt', 'bridge.c', 'Consumer.kt']
            for name in files+sources:
                (folder/name).write_text(name)
            evidence = {'profile': 'android-jvm-consumer-v1', 'mainClass': 'dev.cilica.Probe',
                        'packageReceiptSha256': sha256((root/'build-receipt.json').read_bytes()),
                        'files': {name: sha256((folder/name).read_bytes()) for name in files},
                        'sources': {name: sha256((folder/name).read_bytes()) for name in sources}}
            path = folder/'jvm-receipt.json'; path.write_text(json.dumps(evidence))
            self.assertEqual(jvm_case_files(root, str(folder))[2], 'dev.cilica.Probe')
            evidence['mainClass'] = 'dev.cilica.Probe; true'
            path.write_text(json.dumps(evidence))
            with self.assertRaisesRegex(ValueError, 'main class'):
                jvm_case_files(root, str(folder))
            evidence['mainClass'] = 'dev.cilica.Probe'; path.write_text(json.dumps(evidence))
            (folder/'dex/classes.dex').write_text('changed')
            with self.assertRaisesRegex(ValueError, 'hash mismatch'):
                jvm_case_files(root, str(folder))

    def test_activity_trace_requires_ordered_cycles_and_close(self):
        valid = 'READY 0\nBACKGROUND 0\nRESUMED 1\nBACKGROUND 1\nRESUMED 2\nCLOSED\n'
        validate_trace(valid, 2)
        for changed in [valid.replace('CLOSED\n', ''), valid+'CLOSED\n',
                        valid.replace('BACKGROUND 1', 'BACKGROUND 0'), valid.replace('RESUMED 1\n', '')]:
            with self.assertRaisesRegex(RuntimeError, 'callbacks'):
                validate_trace(changed, 2)

    def test_activity_identity_and_apk_hash_are_required(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve(); self.package(root)
            folder = root/'apk'; folder.mkdir()
            names = ['ProbeRuntime.kt', 'ProbeActivityHost.kt', 'bridge.c', 'MainActivity.kt', 'Scenario.kt']
            for name in names+['consumer.apk']: (folder/name).write_text(name)
            evidence = {'profile':'android-activity-consumer-v1', 'appId':'dev.cilica.test.p'+'a'*32, 'cycles':10,
                'packageReceiptSha256':sha256((root/'build-receipt.json').read_bytes()),
                'sources':{name:sha256((folder/name).read_bytes()) for name in names},
                'apkSha256':sha256((folder/'consumer.apk').read_bytes())}
            receipt = folder/'activity-receipt.json'; receipt.write_text(json.dumps(evidence))
            activity_files(root, folder)
            evidence['appId'] = 'com.someone.else'; receipt.write_text(json.dumps(evidence))
            with self.assertRaisesRegex(ValueError, 'identity'):
                activity_files(root, folder)
            evidence['appId'] = 'dev.cilica.test.p'+'a'*32; receipt.write_text(json.dumps(evidence))
            (folder/'consumer.apk').write_text('changed')
            with self.assertRaisesRegex(ValueError, 'hash mismatch'):
                activity_files(root, folder)

    def test_failed_boot_stops_owned_server_and_records_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            package = root/'package'; package.mkdir(); self.package(package)
            sdk = root/'sdk'
            pin = json.loads(Path(runner.__file__).with_name('emulator.json').read_bytes())
            (sdk/'emulator').mkdir(parents=True)
            (sdk/'emulator/source.properties').write_text('Pkg.Revision='+pin['emulatorRevision'])
            image = sdk/pin['image']; image.mkdir(parents=True)
            (image/'source.properties').write_text('Pkg.Revision='+pin['imageRevision']+
                '\nAndroidVersion.ApiLevel='+pin['api']+'\nSystemImage.Abi='+pin['abi'])
            server = Mock(pid=100); server.poll.return_value = None
            guest = Mock(pid=101); guest.poll.return_value = 1
            output = root/'run'
            with patch.object(runner.subprocess, 'check_output', return_value='Version '+pin['adbVersion']), \
                 patch.object(runner.subprocess, 'Popen', side_effect=[server, guest]) as launch, \
                 patch.object(runner, 'free_port_pair', return_value=5554), \
                 patch.object(runner.socket, 'socket') as socket_factory, \
                 patch.object(runner.socket, 'create_connection'):
                socket_factory.return_value.__enter__.return_value.getsockname.return_value = ('127.0.0.1', 54321)
                with self.assertRaisesRegex(RuntimeError, 'Owned emulator exited'):
                    runner.main(['--sdk', str(sdk), '--output', str(output), '--case', str(package), 'lifecycle'])
            self.assertEqual(launch.call_count, 2)
            server.terminate.assert_called_once_with()
            guest.terminate.assert_not_called()
            receipt = json.loads((output/'run-receipt.json').read_bytes())
            self.assertFalse(receipt['passed'])
            self.assertEqual(receipt['cases'], [])
            self.assertTrue(receipt['cleanup']['emulatorStopped'])
            self.assertTrue(receipt['cleanup']['adbStopped'])
            self.assertTrue(receipt['cleanup']['temporaryDeviceRemoved'])
            self.assertFalse((output/'temporary-device').exists())


if __name__ == '__main__':
    unittest.main()
