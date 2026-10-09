import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('reference', Path(__file__).with_name('reference_workflow.py'))
reference = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reference)


class ReferenceWorkflowTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.candidate = {'id': 'sample', 'repository': 'owner/repo', 'commit': 'a' * 40,
                          'license': 'MIT', 'selected_target': 'netstandard2.0',
                          'files': [{'path': 'Library.cs', 'git_blob_sha1': 'b' * 40}],
                          'roots': ['Probe.Run'], 'screened_before_download': True, 'status': 'mapped-blocked'}
        self.receipt = self.evidence('review.txt', 'authored test evidence only')
        self.m = reference.create(self.candidate, 'batch-020')

    def evidence(self, name, text):
        (self.root / name).write_text(text)
        return {'path': name, 'sha256': hashlib.sha256(text.encode()).hexdigest()}

    def run_receipt(self, stage, stamp, outcome='passed'):
        return self.evidence(stage + '.json', json.dumps({'stage': stage, 'input_fingerprint': stamp,
                             'outcome': outcome, 'artifacts': [self.receipt]}))

    def completed(self):
        m = self.m
        m['screening']['evidence'] = self.receipt
        m['scope'].update(input_cases=self.receipt, candidate_assemblies={'Library.dll': 'c' * 64}, owned_inputs={'review.txt': self.receipt['sha256']})
        m['execution'].update(authorization=self.receipt, approved_stages=['unity', 'profile', 'dotnet', 'cilica'], isolation_plan=self.receipt)
        m['unity'].update(status='captured', version='6000.3.2f1', settings=self.receipt,
                          capture=self.receipt, candidate_assemblies={'Library.dll': 'c' * 64},
                          owned_inputs={'review.txt': self.receipt['sha256']}, reference_commit='a' * 40,
                          reference_roots=['Probe.Run'], reference_input_cases=self.receipt)
        m['inspection'].update(status='inspected', decision=self.receipt, locations=[{
            'symbol': 'GeneratedMethod:10', 'finding': 'Return value remains rooted across allocation',
            'gap_ids': ['G1'], 'evidence': self.receipt}])
        m['gaps'] = [{'id': 'G1', 'status': 'closed', 'requirement': 'Rooted reference return',
                      'regressions': ['OwnedTests.ReferenceReturn'], 'implementation': self.receipt}]
        stamp = reference.fingerprint(m)
        m['profile'].update(status='measured', runtime='unity-il2cpp', roots=['Probe.Run'], input_fingerprint=stamp, evidence=self.receipt, decision=self.receipt, qualification='Diagnostic attribution only; no output-equivalence or speedup claim')
        m['validation'].update(status='passed', coverage='selected-candidate', input_fingerprint=stamp, cilica_commit='d' * 40,
                               dotnet=self.receipt, cilica=self.receipt, performance=self.receipt, review=self.receipt)
        m['unity']['capture'] = self.run_receipt('unity-capture', stamp)
        m['profile']['evidence'] = self.run_receipt('profile', stamp, 'recorded')
        m['validation'].update(dotnet=self.run_receipt('dotnet', stamp), cilica=self.run_receipt('cilica', stamp),
                               performance=self.run_receipt('performance', stamp, 'recorded'))
        return m

    def test_new_handoff_stays_incomplete_and_does_not_execute(self):
        packet = reference.handoff(self.m, self.root)
        self.assertTrue(packet['open_gates'])
        self.assertIsNone(packet['execution']['authorization'])
        self.assertIn('DiffPlex-specific', packet['pipeline']['existing_scope'])
        self.assertEqual(['review.txt'], [p.name for p in self.root.iterdir()])

    def test_matching_complete_receipts_pass(self):
        self.assertEqual([], reference.blockers(self.completed(), self.root, self.candidate))

    def test_capture_is_neither_inspection_nor_runtime_profile(self):
        for section, field, value in [('inspection', 'status', 'pending'), ('profile', 'status', 'pending'),
                                      ('validation', 'status', 'pending'), ('unity', 'status', 'pending')]:
            self.completed()[section][field] = value
            with self.subTest(section=section):
                self.assertTrue(reference.blockers(self.m, self.root))

    def test_stale_inputs_and_unrelated_roots_fail(self):
        for mutate in [lambda m: m['unity']['candidate_assemblies'].update({'Library.dll': 'e' * 64}),
                       lambda m: m['scope']['roots'].append('Different.Run'),
                       lambda m: m['scope'].update(input_cases=self.evidence('other.txt', 'different inputs')),
                       lambda m: m['profile'].update(roots=['Unselected.Run']),
                       lambda m: m['validation'].update(input_fingerprint='e' * 64)]:
            self.m = reference.create(self.candidate, 'batch-020')
            mutate(self.completed())
            self.assertTrue(reference.blockers(self.m, self.root))

    def test_claimed_results_need_scope_approval_and_isolation(self):
        for stage in ['unity', 'profile', 'dotnet', 'cilica']:
            self.completed()['execution']['approved_stages'].remove(stage)
            self.assertTrue(any('approval' in p for p in reference.blockers(self.m, self.root)))
        self.completed()['execution']['isolation_plan'] = None
        self.assertTrue(reference.blockers(self.m, self.root))

    def test_missing_tampered_traversal_and_symlink_receipts_fail(self):
        self.completed()
        for value in [None, {'path': '../review.txt', 'sha256': self.receipt['sha256']},
                      {'path': 'absent', 'sha256': self.receipt['sha256']},
                      {'path': 'review.txt', 'sha256': '0' * 64}]:
            self.m['inspection']['decision'] = value
            self.assertTrue(reference.blockers(self.m, self.root))
        (self.root / 'link').symlink_to(self.root / 'review.txt')
        self.m['inspection']['decision'] = {**self.receipt, 'path': 'link'}
        self.assertTrue(reference.blockers(self.m, self.root))
        self.m['inspection']['decision'] = self.receipt
        (self.root / 'review.txt').write_text('changed')
        self.assertTrue(reference.blockers(self.m, self.root))

    def test_unity_blocker_requires_reviewed_alternative_not_fake_capture(self):
        m = self.completed(); m['unity'].update(status='blocked', blocker='Unsupported target API')
        self.assertTrue(reference.blockers(m, self.root))
        m['exception'] = {'alternative': 'Pinned official runtime implementation', 'limits': 'No Unity execution equivalence',
                          'blocker_evidence': self.receipt, 'decision': self.receipt}
        m['profile'].update(runtime='alternative')
        self.assertEqual([], reference.blockers(m, self.root))
        m['unity']['blocker'] = ''
        self.assertTrue(reference.blockers(m, self.root))

    def test_small_static_gap_may_omit_profile_only_with_reviewed_reason(self):
        m = self.completed(); m['profile'].update(status='not-applicable', evidence=None)
        self.assertTrue(reference.blockers(m, self.root))
        m['profile'].update(reason='Metadata-only diagnostic; no changed executed path', decision=self.receipt)
        self.assertEqual([], reference.blockers(m, self.root))

    def test_unity_correctness_is_distinct_and_failure_blocks(self):
        m = self.completed()
        self.assertEqual('not-run', m['unity']['correctness']['status'])
        m['unity']['correctness'].update(status='failed', evidence=self.run_receipt('unity-correctness', reference.fingerprint(m), 'failed'), input_fingerprint=reference.fingerprint(m))
        self.assertTrue(reference.blockers(m, self.root))
        m['unity']['correctness'].update(status='passed', evidence=self.run_receipt('unity-correctness', reference.fingerprint(m)))
        self.assertEqual([], reference.blockers(m, self.root))

    def test_every_closed_gap_requires_concrete_reference_and_owned_regression(self):
        for field, value in [('gap_ids', ['nonexistent']), ('gap_ids', []), ('symbol', ''), ('finding', '')]:
            m = self.completed(); m['inspection']['locations'][0][field] = value
            self.assertTrue(reference.blockers(m, self.root))
        self.completed()['gaps'][0]['regressions'] = []
        self.assertTrue(reference.blockers(self.m, self.root))

    def test_mapped_skip_is_explicit_and_needs_no_execution(self):
        m = self.m; m['screening']['evidence'] = self.receipt
        m['gaps'] = [{'id': 'none', 'requirement': 'Already mapped', 'status': 'out-of-scope', 'reason': 'No new requirement at this pin'}]
        self.assertTrue(reference.blockers(m, self.root))
        m['unity']['status'] = 'not-applicable'; m['inspection']['status'] = 'not-applicable'
        m['profile'].update(status='not-applicable', reason='No selected feature change', decision=self.receipt)
        self.assertEqual([], reference.blockers(m, self.root))

    def test_owned_harness_identity_is_bound_to_capture_profile_and_validation(self):
        m = self.completed()
        m['scope']['owned_inputs'] = {'changed.cs': self.evidence('changed.cs', 'new adapter')['sha256']}
        self.assertTrue(reference.blockers(m, self.root))

    def test_unmatched_capture_can_guide_but_never_support_matched_timing(self):
        m = self.completed(); m['unity']['candidate_assemblies']['Library.dll'] = 'e' * 64
        m['unity']['status'] = 'captured-unmatched'
        self.assertTrue(reference.blockers(m, self.root))
        m['exception'] = {'alternative': 'Generated code at different pin', 'limits': 'Architectural reference only',
                          'blocker_evidence': self.receipt, 'decision': self.receipt}
        stamp = reference.unity_fingerprint(m)
        m['profile'].update(input_fingerprint=stamp, evidence=self.run_receipt('profile', stamp, 'recorded'))
        m['unity']['capture'] = self.run_receipt('unity-capture', stamp)
        self.assertEqual([], reference.blockers(m, self.root))
        m['profile']['kind'] = 'comparative'
        self.assertTrue(reference.blockers(m, self.root))

    def test_comparative_profile_requires_executed_matching_outputs(self):
        m = self.completed(); m['profile']['kind'] = 'comparative'
        self.assertTrue(reference.blockers(m, self.root))
        m['unity']['correctness'].update(status='passed', input_fingerprint=reference.unity_fingerprint(m), evidence=self.run_receipt('unity-correctness', reference.unity_fingerprint(m)))
        m['profile']['output_verification'] = self.run_receipt('comparative-output', reference.fingerprint(m))
        self.assertEqual([], reference.blockers(m, self.root))
        m['profile'].update(kind='diagnostic', qualification='')
        self.assertTrue(reference.blockers(m, self.root))

    def test_reference_gate_can_pass_before_implementation_but_cannot_close_gap(self):
        m = self.completed(); m['gaps'][0].update(status='open', implementation=None)
        m['validation']['status'] = 'pending'
        self.assertEqual([], reference.blockers(m, self.root, phase='reference'))
        self.assertTrue(reference.blockers(m, self.root))

    def test_relabelled_fingerprints_cannot_reuse_stale_execution_receipts(self):
        m = self.completed()
        changed = self.evidence('changed-cases.json', 'new input cases')
        m['scope']['input_cases'] = changed; m['unity']['reference_input_cases'] = changed
        stamp = reference.fingerprint(m)
        m['profile']['input_fingerprint'] = stamp; m['validation']['input_fingerprint'] = stamp
        problems = reference.blockers(m, self.root)
        self.assertTrue(any('receipt has stale input identity' in p for p in problems))

    def test_wrong_execution_stage_or_missing_raw_artifact_is_rejected(self):
        m = self.completed(); m['validation']['cilica'] = m['validation']['dotnet']
        self.assertTrue(reference.blockers(m, self.root))
        m = self.completed()
        m['profile']['evidence'] = self.evidence('empty.json', json.dumps({'stage': 'profile',
             'input_fingerprint': reference.fingerprint(m), 'outcome': 'recorded', 'artifacts': []}))
        self.assertTrue(reference.blockers(m, self.root))

    def test_owned_regression_does_not_authorize_candidate_verified_status(self):
        m = self.completed(); m['validation']['coverage'] = 'owned-regression'
        self.assertEqual([], reference.blockers(m, self.root))
        self.candidate.update(status='verified', reference_manifest='reference.json')
        (self.root / 'reference.json').write_text(json.dumps(m))
        with self.assertRaises(ValueError):
            reference.check_batch(self.root, 'batch-020', self.candidate)

    def test_future_snapshot_requires_screening_before_any_candidate_action(self):
        self.candidate['reference_manifest'] = 'reference.json'
        (self.root / 'reference.json').write_text(json.dumps(self.m))
        with self.assertRaises(ValueError):
            reference.check_batch(self.root, 'batch-020', self.candidate)

    def test_mapped_skip_cannot_be_promoted_to_verified_without_execution(self):
        self.test_mapped_skip_is_explicit_and_needs_no_execution()
        self.m['validation']['coverage'] = 'selected-candidate'
        self.candidate.update(status='verified', reference_manifest='reference.json')
        (self.root / 'reference.json').write_text(json.dumps(self.m))
        with self.assertRaises(ValueError):
            reference.check_batch(self.root, 'batch-020', self.candidate)

    def test_batch_boundary_preserves_history_and_gates_future_completion(self):
        reference.check_batch(self.root, 'batch-019', self.candidate)
        with self.assertRaises(ValueError):
            reference.check_batch(self.root, 'batch-020', self.candidate)
        self.m['screening']['evidence'] = self.receipt
        self.candidate['reference_manifest'] = 'reference.json'
        (self.root / 'reference.json').write_text(json.dumps(self.m))
        reference.check_batch(self.root, 'batch-020', self.candidate)
        self.candidate['status'] = 'verified'
        with self.assertRaises(ValueError):
            reference.check_batch(self.root, 'batch-020', self.candidate)
        (self.root / 'reference.json').write_text(json.dumps(self.completed()))
        reference.check_batch(self.root, 'batch-020', self.candidate)
        self.candidate['commit'] = 'e' * 40
        with self.assertRaises(ValueError):
            reference.check_batch(self.root, 'batch-020', self.candidate)


if __name__ == '__main__':
    unittest.main()
