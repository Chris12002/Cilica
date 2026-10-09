"""Owned synthetic data only: no candidate DLL, Unity API or upstream code execution."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

BASE = Path(__file__).parent / 'albireo-unity'
if not BASE.exists():
    BASE = Path(__file__).parent

def load(name):
    spec = importlib.util.spec_from_file_location(name, BASE / (name + '.py'))
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    return module

transcript = load('transcript')
stager = load('stage_bundle')


class AlbireoTranscriptTests(unittest.TestCase):
    def setUp(self):
        self.rows = [[s, i, 6 if i == -1 else 0] for s in range(32) for i in range(-1, 6)]
        self.expected = transcript.normalize(self.rows, 'a' * 64)
        self.actual = dict(self.expected, mode='verify')

    def test_exact_complete_transcript_passes(self):
        self.assertEqual(224, transcript.compare(self.expected, self.actual))

    def test_changed_observation_rejects_even_with_new_checksum(self):
        self.actual = copy.deepcopy(self.actual); self.actual['values'][5] += 1
        self.actual['checksum'] = transcript.checksum(self.actual['values'])
        with self.assertRaises(ValueError):
            transcript.compare(self.expected, self.actual)

    def test_pin_fingerprint_runtime_error_and_missing_mode_reject(self):
        for key, value in [('candidateCommit', 'b' * 40), ('inputFingerprint', 'c' * 64), ('mode', 'noop'), ('error', 'failed')]:
            with self.subTest(key=key), self.assertRaises(ValueError):
                transcript.compare(self.expected, dict(self.actual, **{key:value}))

    def test_missing_reordered_oversized_or_boolean_rows_reject(self):
        variants = [self.rows[:-1], self.rows + [[32, -1, 1]], self.rows[1:]]
        bad = copy.deepcopy(self.rows); bad[0][2] = 4097; variants.append(bad)
        bad = copy.deepcopy(self.rows); bad[1][2] = True; variants.append(bad)
        for rows in variants:
            with self.assertRaises(ValueError):
                transcript.normalize(rows, 'a' * 64)

    def test_profile_requires_all_samples_matching_scope_and_observations(self):
        prof = dict(self.actual, mode='profile', profileScenarios=[2,8,29,31], warmIterations=8, iterations=32,
                    elapsedTicks=[1,2,3], frequency=1000, sampleChecksums=[768]*3,
                    profileFile='/evidence/profile.raw', qualification='Synthetic diagnostic fixture')
        self.assertEqual(224, transcript.compare(self.expected, prof))
        for key,value in [('iterations',129),('profileScenarios',[1]),('elapsedTicks',[1,2]),('frequency',0),('sampleChecksums',[1]*3),('profileFile','')]:
            with self.subTest(key=key),self.assertRaises(ValueError):
                transcript.compare(self.expected,dict(prof,**{key:value}))


class AlbireoStagingTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(); self.addCleanup(self.tmp.cleanup); self.root = Path(self.tmp.name).resolve()
        self.source = self.root / 'source'
        for path in stager.TEMPLATE_FILES:
            p=self.source/'template'/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_text('owned fixture')
        self.dll=self.root/'input.dll';self.dll.write_bytes(b'opaque test bytes, never loaded')
        self.sha=stager.digest(self.dll);self.license=self.root/'license.txt';self.license.write_text('Owned licence test fixture')
        data=self.license.read_bytes();self.old=stager.LICENSE_BLOB
        stager.LICENSE_BLOB=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
        self.addCleanup(setattr,stager,'LICENSE_BLOB',self.old)
        self.output=self.root/'staged'

    def test_only_exact_pinned_data_is_copied_without_execution(self):
        result=stager.stage(self.source,self.dll,self.sha,self.license,self.output)
        self.assertFalse(result['executed']);self.assertEqual(6,len(result['files']))
        self.assertEqual(self.dll.read_bytes(),(self.output/'template/ProjectData~/Assets/Plugins/Albireo.SelectedDecode.dll').read_bytes())
        with self.assertRaises(ValueError):stager.stage(self.source,self.dll,self.sha,self.license,self.output)

    def test_bad_dll_or_license_creates_no_output(self):
        with self.assertRaises(ValueError):stager.stage(self.source,self.dll,'0'*64,self.license,self.output)
        self.assertFalse(self.output.exists());self.license.write_text('changed')
        with self.assertRaises(ValueError):stager.stage(self.source,self.dll,self.sha,self.license,self.output)
        self.assertFalse(self.output.exists())

    def test_symlink_output_parent_or_input_is_rejected(self):
        link=self.root/'link';link.symlink_to(self.source,target_is_directory=True)
        with self.assertRaises(ValueError):stager.stage(self.source,self.dll,self.sha,self.license,link/'new')
        alias=self.root/'alias.dll';alias.symlink_to(self.dll)
        with self.assertRaises(ValueError):stager.stage(self.source,alias,self.sha,self.license,self.output)


if __name__ == '__main__':
    unittest.main()
