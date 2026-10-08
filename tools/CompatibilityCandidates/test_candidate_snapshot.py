import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('snapshot', Path(__file__).with_name('candidate_snapshot.py'))
snapshot = importlib.util.module_from_spec(spec)
spec.loader.exec_module(snapshot)


class SnapshotSafetyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.candidate = {'id': 'sample', 'repository': 'owner/repo', 'commit': 'a' * 40,
                          'status': 'mapped-skip', 'next_action': 'skip',
                          'files': [{'path': 'src/a.cs', 'git_blob_sha1': snapshot.blob_hash(b'original')}]}
        self.directory = snapshot.target(self.root, self.candidate)
        (self.directory / 'src').mkdir(parents=True)
        (self.directory / 'src/a.cs').write_bytes(b'original')
        (self.directory / snapshot.MARKER).write_text(json.dumps(snapshot.marker(self.candidate)))

    def test_verified_owned_snapshot_can_be_removed(self):
        snapshot.inspect(self.directory, self.candidate)
        snapshot.cleanup(self.directory, self.candidate)
        self.assertFalse(self.directory.exists())

    def test_unique_or_modified_content_is_never_removed(self):
        for name, content in [('src/a.cs', b'my edits'), ('notes.txt', b'unique')]:
            with self.subTest(name=name):
                path = self.directory / name
                path.write_bytes(content)
                with self.assertRaises(ValueError):
                    snapshot.cleanup(self.directory, self.candidate)
                self.assertEqual(content, path.read_bytes())
                if name == 'src/a.cs':
                    path.write_bytes(b'original')

    def test_symlinked_file_marker_and_output_parent_are_rejected(self):
        path = self.directory / 'src/a.cs'
        path.unlink()
        path.symlink_to(self.root / 'outside')
        with self.assertRaises(ValueError):
            snapshot.cleanup(self.directory, self.candidate)
        path.unlink()
        path.write_bytes(b'original')
        notice = self.directory / snapshot.MARKER
        notice.unlink()
        notice.symlink_to(self.root / 'outside')
        with self.assertRaises(ValueError):
            snapshot.inspect(self.directory, self.candidate)
        parent = self.root / 'other'
        parent.mkdir()
        (parent / 'artifacts').symlink_to(self.directory, target_is_directory=True)
        with self.assertRaises(ValueError):
            snapshot.target(parent, self.candidate)

    def test_wrong_owner_and_unfinished_outcome_are_rejected(self):
        self.candidate['status'] = 'in-progress'
        with self.assertRaises(ValueError):
            snapshot.cleanup(self.directory, self.candidate)
        self.candidate['status'] = 'mapped-blocked'
        (self.directory / snapshot.MARKER).write_text('{}')
        with self.assertRaises(ValueError):
            snapshot.cleanup(self.directory, self.candidate)

    def test_paths_must_be_normalized_relative_unique_and_pinned(self):
        for path in ['../a', '/a', 'a/../b', 'a\\b', './a', 'a//b', snapshot.MARKER]:
            with self.subTest(path=path):
                self.candidate['files'][0]['path'] = path
                with self.assertRaises(ValueError):
                    snapshot.validate(self.candidate)
        self.candidate['files'][0]['path'] = 'a'
        self.candidate['files'] *= 2
        with self.assertRaises(ValueError):
            snapshot.validate(self.candidate)

    def test_connector_stage_checks_hashes_before_writing(self):
        source = self.root / 'import.json'
        (self.directory / 'src/a.cs').unlink()
        source.write_text(json.dumps({'src/a.cs': 'different'}))
        with self.assertRaises(ValueError):
            snapshot.stage(self.directory, self.candidate, source)
        self.assertFalse((self.directory / 'src/a.cs').exists())
        source.write_text(json.dumps({'src/a.cs': 'original', 'extra': 'unique'}))
        with self.assertRaises(ValueError):
            snapshot.stage(self.directory, self.candidate, source)
        self.assertFalse((self.directory / 'src/a.cs').exists())

    def test_connector_stage_resumes_without_overwriting(self):
        source = self.root / 'import.json'
        source.write_text(json.dumps({'src/a.cs': 'original'}))
        (self.directory / 'src/a.cs').unlink()
        snapshot.stage(self.directory, self.candidate, source)
        snapshot.stage(self.directory, self.candidate, source)
        self.assertEqual(b'original', (self.directory / 'src/a.cs').read_bytes())
        (self.directory / 'src/a.cs').write_text('unique changes')
        with self.assertRaises(ValueError):
            snapshot.stage(self.directory, self.candidate, source)
        self.assertEqual('unique changes', (self.directory / 'src/a.cs').read_text())

    def test_incomplete_snapshot_can_resume_but_is_not_verified(self):
        (self.directory / 'src/a.cs').unlink()
        with self.assertRaises(ValueError):
            snapshot.inspect(self.directory, self.candidate)
        self.assertEqual(set(), snapshot.inspect(self.directory, self.candidate, complete=False))


if __name__ == '__main__':
    unittest.main()
