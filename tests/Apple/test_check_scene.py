import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from check_scene import verify_build, wait_for


class SceneEvidenceTests(unittest.TestCase):
    def test_changed_source_or_executable_is_rejected_before_simulator_work(self):
        with tempfile.TemporaryDirectory() as folder:
            build = Path(folder)
            binary, source = build / 'iphonesimulator', build / 'Support1.swift'
            binary.write_bytes(b'linked binary')
            source.write_text('consumer source')
            receipt = {'simulator': None,
                       'binaryHashes': {binary.name: hashlib.sha256(binary.read_bytes()).hexdigest()},
                       'sourceHashes': {source.name: hashlib.sha256(source.read_bytes()).hexdigest()}}
            (build / 'smoke-receipt.json').write_text(json.dumps(receipt))
            self.assertEqual(verify_build(build), binary)
            source.write_text('edited after compilation')
            with self.assertRaisesRegex(ValueError, 'source differs'):
                verify_build(build)
            source.write_text('consumer source')
            binary.write_bytes(b'another executable')
            with self.assertRaisesRegex(ValueError, 'executable differs'):
                verify_build(build)

    def test_old_receipt_requests_rebuild(self):
        with tempfile.TemporaryDirectory() as folder:
            build = Path(folder)
            (build / 'smoke-receipt.json').write_text('{"simulator": null}')
            with self.assertRaisesRegex(ValueError, 'Rebuild'):
                verify_build(build)

    def test_cycle_markers_do_not_accept_another_cycle_or_a_failed_log(self):
        with tempfile.TemporaryDirectory() as folder:
            log = Path(folder) / 'events.txt'
            log.write_text('READY cycle=10\n')
            with self.assertRaises(TimeoutError):
                wait_for(log, 'READY cycle=1\n', seconds=0.001)
            wait_for(log, 'READY cycle=10\n')
            log.write_text('READY cycle=1\nFAIL assertion\n')
            with self.assertRaisesRegex(RuntimeError, 'FAIL'):
                wait_for(log, 'READY cycle=1\n')


if __name__ == '__main__':
    unittest.main()
