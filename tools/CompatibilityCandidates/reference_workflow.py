#!/usr/bin/env python3
"""Pinned Unity reference handoffs and completion gates. Never downloads or executes code."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re

VERSION = 1
FIRST_REQUIRED_BATCH = 20
IDENTITY = ('id', 'repository', 'commit', 'license', 'selected_target', 'files')


def relative_path(value):
    p = PurePosixPath(value)
    if not value or not p.parts or p.is_absolute() or '..' in p.parts or '\\' in value or p.as_posix() != value:
        raise ValueError('Expected normalized repository-relative path: ' + str(value))
    return p


def read_evidence(root, item):
    if not isinstance(item, dict) or not re.fullmatch('[0-9a-f]{64}', item.get('sha256', '')):
        raise ValueError('Evidence requires path and SHA-256')
    rel = relative_path(item.get('path', ''))
    path = root
    for part in rel.parts:
        path = path / part
        if path.is_symlink():
            raise ValueError('Evidence symlink refused: ' + str(rel))
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(65536), b''):
            digest.update(block)
    if digest.hexdigest() != item['sha256']:
        raise ValueError('Evidence changed: ' + str(rel))
    return path


def create(candidate, batch):
    return {
        'reference_workflow_version': VERSION,
        'batch': batch,
        'candidate': {key: candidate[key] for key in IDENTITY},
        'screening': {'online_first': candidate.get('screened_before_download') is True,
                      'evidence': None},
        'scope': {'roots': candidate.get('roots', []), 'input_cases': None,
                  'candidate_assemblies': {}, 'owned_inputs': {}, 'excluded': candidate.get('excluded_scope', [])},
        'execution': {'authorization': None, 'approved_stages': [], 'isolation_plan': None},
        'unity': {'status': 'pending', 'version': '', 'settings': None,
                  'candidate_assemblies': {}, 'owned_inputs': {}, 'reference_commit': '',
                  'reference_roots': [], 'reference_input_cases': None, 'capture': None, 'blocker': '',
                  'correctness': {'status': 'not-run', 'input_fingerprint': '', 'evidence': None}},
        'inspection': {'status': 'pending', 'locations': [], 'decision': None},
        'profile': {'status': 'pending', 'runtime': '', 'kind': 'diagnostic',
                    'qualification': '', 'output_verification': None, 'roots': [], 'input_fingerprint': '',
                    'evidence': None, 'reason': '', 'decision': None},
        'exception': None,
        'gaps': [],
        'validation': {'status': 'pending', 'coverage': 'pending', 'input_fingerprint': '', 'cilica_commit': '',
                       'dotnet': None, 'cilica': None, 'performance': None,
                       'performance_not_applicable': '', 'review': None},
        'next_action': 'Record gaps and representative roots; request a candidate-specific Unity handoff. No execution is authorized by this manifest.'
    }


def fingerprint(manifest):
    scope = manifest['scope']
    payload = {'commit': manifest['candidate']['commit'], 'roots': scope['roots'],
               'input_cases': scope['input_cases']['sha256'],
               'candidate_assemblies': scope['candidate_assemblies'], 'owned_inputs': scope['owned_inputs']}
    return hashlib.sha256(json.dumps(payload, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def unity_fingerprint(manifest):
    unity = manifest['unity']
    payload = {'commit': unity['reference_commit'], 'roots': unity['reference_roots'],
               'input_cases': unity['reference_input_cases']['sha256'],
               'candidate_assemblies': unity['candidate_assemblies'], 'owned_inputs': unity['owned_inputs']}
    return hashlib.sha256(json.dumps(payload, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def blockers(m, root, candidate=None, phase="complete"):
    """Return unmet gates, checking retained bytes rather than trusting status labels."""
    problems = []
    def need(ok, message):
        if not ok:
            problems.append(message)
        return bool(ok)
    def evidence(value, label):
        try:
            read_evidence(root, value)
            return True
        except (ValueError, OSError, TypeError) as error:
            problems.append(label + ': ' + str(error))
            return False
    def run_evidence(value, stage, expected, outcome='passed'):
        try:
            path = read_evidence(root, value)
            record = json.loads(path.read_text())
            need(record.get('stage') == stage, stage + ' receipt has wrong execution stage')
            need(record.get('input_fingerprint') == expected and expected is not None, stage + ' receipt has stale input identity')
            need(record.get('outcome') == outcome, stage + ' receipt does not record ' + outcome)
            need(bool(record.get('artifacts')), stage + ' receipt requires raw result artifacts')
            for artifact in record.get('artifacts', []):
                evidence(artifact, stage + ' raw artifact')
        except (ValueError, OSError, TypeError, AttributeError) as error:
            problems.append(stage + ' receipt: ' + str(error))

    def executed(stage):
        need(stage in m['execution']['approved_stages'], stage + ' execution lacks recorded scope approval')
        evidence(m['execution']['authorization'], 'execution authorization receipt')
        evidence(m['execution']['isolation_plan'], 'execution isolation plan')
    def assemblies(value, label):
        return need(isinstance(value, dict) and bool(value) and all(
            isinstance(k, str) and re.fullmatch('[0-9a-f]{64}', v or '')
            for k, v in value.items()), label + ' requires assembly names and SHA-256 pins')

    def owned_inputs(value, label):
        if assemblies(value, label):
            for path, digest in value.items():
                evidence({'path': path, 'sha256': digest}, label + ' adapter/harness input')

    if not need(m.get('reference_workflow_version') == VERSION, 'Unsupported reference workflow version'):
        return problems
    try:
        c = m['candidate']
        need(bool(re.fullmatch('[0-9a-f]{40}', c['commit'])), 'Candidate commit must be a full pin')
        need(bool(c['license']) and bool(c['files']), 'Candidate licence and source pins required')
        source_paths = []
        for entry in c['files']:
            source_paths.append(str(relative_path(entry['path'])))
            need(bool(re.fullmatch('[0-9a-f]{40}', entry['git_blob_sha1'])), 'Source Git blob pin required')
        need(len(set(source_paths)) == len(source_paths), 'Duplicate pinned source paths')
        target = c['selected_target']
        need(bool(re.fullmatch(r'netstandard(?:1\.[0-6]|2\.[01])', target)), 'Select a compatible .NET Standard asset through 2.1')
        if candidate is not None:
            need(all(c.get(k) == candidate.get(k) for k in IDENTITY), 'Reference identity differs from candidate manifest')
        need(m['screening']['online_first'] is True, 'Online screening must precede source download')
        evidence(m['screening']['evidence'], 'screening record')
        scope = m['scope']
        need(bool(scope['roots']), 'Exact selected roots required')
        gaps = m['gaps']
        need(bool(gaps), 'Record applicable gaps, or an explicit no-novel-gap mapping')
        ids = [g['id'] for g in gaps]
        need(len(set(ids)) == len(ids), 'Duplicate gap IDs')
        applicable = [g for g in gaps if g['status'] != 'out-of-scope']
        for g in gaps:
            need(g['status'] in ('open', 'closed', 'out-of-scope'), 'Unknown gap status: ' + g['id'])
            need(bool(g['requirement']), 'Gap requirement required: ' + g['id'])
            if g['status'] == 'out-of-scope':
                need(bool(g.get('reason')), 'Out-of-scope reason required: ' + g['id'])
        # A fully mapped skip still passes through an explicit reference decision.
        skipped = not applicable and bool(gaps)
        if skipped:
            need(m['unity']['status'] == 'not-applicable', 'Mapped skip requires explicit Unity not-applicable status')
            need(m['profile']['status'] == 'not-applicable' and bool(m['profile']['reason']), 'Mapped skip requires profile rationale')
            evidence(m['profile']['decision'], 'mapped-skip review')
            need(m['inspection']['status'] == 'not-applicable', 'Mapped skip must distinguish absent reference inspection')
            return problems
        evidence(scope['input_cases'], 'representative input cases')
        assemblies(scope['candidate_assemblies'], 'common candidate input')
        owned_inputs(scope['owned_inputs'], 'common owned adapter/harness')
        expected = fingerprint(m) if scope['input_cases'] else None
        unity = m['unity']
        captured = unity['status'] in ('captured', 'captured-unmatched')
        actual = None
        if captured:
            executed('unity')
            need(bool(unity['version']), 'Unity version required')
            evidence(unity['settings'], 'Unity build settings')
            assemblies(unity['candidate_assemblies'], 'Unity candidate input')
            owned_inputs(unity['owned_inputs'], 'Unity owned adapter/harness')
            need(bool(re.fullmatch('[0-9a-f]{40}', unity['reference_commit'])), 'Unity reference commit required')
            need(bool(unity['reference_roots']), 'Actual Unity roots required')
            evidence(unity['reference_input_cases'], 'actual Unity input cases')
            actual = unity_fingerprint(m) if unity['reference_input_cases'] else None
            run_evidence(unity['capture'], 'unity-capture', actual)
            if unity['status'] == 'captured':
                need(actual == expected, 'Unity identity differs: use captured-unmatched with an explicit reference exception')
                need(m['exception'] is None, 'Matched capture must not carry an exception')
            else:
                need(actual != expected, 'Use captured for matching Unity inputs')
        elif unity['status'] == 'blocked':
            need(bool(unity['blocker']), 'Exact Unity blocker required')
        else:
            need(False, 'Unity reference capture pending (or explicit blocker/alternative required)')
        if unity['status'] in ('blocked', 'captured-unmatched'):
            ex = m['exception']
            need(isinstance(ex, dict), 'Blocked/unmatched Unity requires an explicit reference exception')
            if isinstance(ex, dict):
                need(bool(ex.get('alternative')) and bool(ex.get('limits')), 'Alternative reference and limitations required')
                evidence(ex.get('blocker_evidence'), 'Unity blocker/identity-mismatch evidence')
                evidence(ex.get('decision'), 'alternative-reference decision')
        correctness = unity['correctness']
        need(correctness['status'] in ('not-run', 'passed', 'failed'), 'Unknown Unity player correctness status')
        if correctness['status'] != 'not-run':
            need(captured, 'Unity player result requires a captured Unity build')
            executed('unity')
            run_evidence(correctness['evidence'], 'unity-correctness', actual, outcome=correctness['status'])
            need(correctness['input_fingerprint'] == actual, 'Unity correctness input identity mismatch')
        need(correctness['status'] != 'failed', 'Unity semantic mismatch requires investigation before closure')
        inspection = m['inspection']
        need(inspection['status'] == 'inspected', 'Static generated/runtime reference inspection incomplete')
        evidence(inspection['decision'], 'reference-to-implementation design review')
        need(bool(inspection['locations']), 'Concrete generated/runtime reference locations required')
        mapped = set()
        for loc in inspection['locations']:
            evidence(loc['evidence'], 'reference source')
            need(bool(loc['symbol']) and bool(loc['finding']), 'Reference symbol and semantic finding required')
            need(bool(loc['gap_ids']) and set(loc['gap_ids']) <= set(ids), 'Reference points to missing gap IDs')
            mapped.update(loc['gap_ids'])
        profile = m['profile']
        if profile['status'] == 'measured':
            executed('profile')
            need(profile['runtime'] == ('unity-il2cpp' if captured else 'alternative'), 'Identify actual profiling runtime')
            run_evidence(profile['evidence'], 'profile', actual if captured else expected, outcome='recorded')
            evidence(profile['decision'], 'runtime profile interpretation review')
            roots = unity['reference_roots'] if captured else scope['roots']
            need(bool(profile['roots']) and set(profile['roots']) <= set(roots), 'Profile roots must be recorded reference roots')
            need(profile['input_fingerprint'] == (actual if captured else expected), 'Profile input identity mismatch')
            if profile['kind'] == 'comparative':
                need(unity['status'] == 'captured', 'Comparative timing requires matching captured Unity inputs')
                need(correctness['status'] == 'passed', 'Comparative timing requires executed Unity output verification')
                need(m['validation']['status'] == 'passed', 'Comparative timing requires completed .NET/Cilica correctness')
                run_evidence(profile['output_verification'], 'comparative-output', expected)
            elif profile['kind'] == 'diagnostic':
                need(bool(profile['qualification']), 'Diagnostic profile must qualify output verification and disallow performance-equivalence claims')
            else:
                need(False, 'Profile kind must be diagnostic or comparative')
        elif profile['status'] == 'not-applicable':
            need(bool(profile['reason']), 'Profile omission requires a concrete path-relevance reason')
            evidence(profile['decision'], 'scoped profiling exception review')
        else:
            need(False, 'Representative runtime profile pending')
        for g in applicable:
            need(g['id'] in mapped, 'No concrete reference mapped to gap: ' + g['id'])
            need(bool(g.get('regressions')), 'Owned regressions required: ' + g['id'])
            if phase == 'complete':
                need(g['status'] == 'closed', 'Gap still open: ' + g['id'])
                evidence(g.get('implementation'), 'implementation record for ' + g['id'])
        if phase == 'reference' and not (profile['status'] == 'measured' and profile['kind'] == 'comparative'):
            return list(dict.fromkeys(problems))
        v = m['validation']
        need(v['status'] == 'passed', 'Differential correctness not complete')
        need(v['coverage'] in ('owned-regression', 'selected-candidate'), 'Declare owned-regression versus selected-candidate validation coverage')
        if v['status'] == 'passed':
            executed('dotnet'); executed('cilica')
        need(bool(re.fullmatch('[0-9a-f]{40}', v['cilica_commit'])), 'Validated Cilica commit required')
        need(v['input_fingerprint'] == expected, 'Differential input identity mismatch')
        run_evidence(v['dotnet'], 'dotnet', expected)
        run_evidence(v['cilica'], 'cilica', expected)
        if v['performance']:
            executed('profile')
            run_evidence(v['performance'], 'performance', expected, outcome='recorded')
        else:
            need(bool(v['performance_not_applicable']), 'Performance check or explicit no-performance-claim rationale required')
        evidence(v['review'], 'final semantic/performance review')
    except (KeyError, TypeError, AttributeError, ValueError) as error:
        problems.append('Malformed/incomplete reference record: ' + str(error))
    return list(dict.fromkeys(problems))


def check_batch(root, batch, candidate):
    """New batches cannot mark candidates verified without passing this gate."""
    required = int(batch.removeprefix('batch-')) >= FIRST_REQUIRED_BATCH
    link = candidate.get('reference_manifest')
    if not link and not required:
        return
    if not link:
        raise ValueError('Batch020+ requires reference_manifest for every candidate')
    path = root
    for part in relative_path(link).parts:
        path = path / part
        if path.is_symlink():
            raise ValueError('Reference manifest symlink refused')
    m = json.loads(path.read_text())
    if m.get('reference_workflow_version') != VERSION:
        raise ValueError('Unsupported reference workflow version')
    if m.get('batch') != batch or any(m.get('candidate', {}).get(k) != candidate.get(k) for k in IDENTITY):
        raise ValueError('Reference manifest batch/candidate identity mismatch')
    if not m.get('screening', {}).get('online_first'):
        raise ValueError('Online screening required before candidate actions')
    read_evidence(root, m['screening']['evidence'])
    if candidate['status'] == 'verified' and (m.get('validation', {}).get('coverage') != 'selected-candidate'
            or not any(g.get('status') != 'out-of-scope' for g in m.get('gaps', []))):
        raise ValueError('Verified candidate requires selected-candidate execution; owned regressions are not candidate equivalence')
    if candidate['status'] in ('verified', 'mapped-skip') or any(g.get('status') == 'closed' for g in m.get('gaps', [])):
        unmet = blockers(m, root, candidate)
        if unmet:
            raise ValueError('Reference completion gate: ' + '; '.join(unmet))


def handoff(m, root):
    return {'kind': 'cilica-unity-reference-handoff-v1', 'candidate': m['candidate'],
            'scope': m['scope'], 'gaps': m['gaps'], 'execution': m['execution'],
            'open_gates': blockers(m, root),
            'instruction': 'Read-only handoff, not execution permission. Adapt the approved Unity pipeline to these exact roots/inputs; do not invoke DiffPlex prepare.py for another candidate.',
            'pipeline': {'owner_thread': '01a11bd8-62f0-7412-8d23-69edc92bd0de',
                         'existing_entry': 'tools/Performance/unity/pipeline.py --stage build|capture|verify|all',
                         'existing_scope': 'DiffPlex-specific; template, capture BenchProbe matcher and transcript checker require explicit candidate adaptation.',
                         'required_outputs': ['Unity version/settings', 'exact candidate assembly hashes', 'generated-source capture manifest and symbol locations', 'static semantic findings mapped to gap IDs', 'representative profile with input_fingerprint or reviewed path-relevance exception', 'normal .NET/Cilica differential receipts; Unity execution reported separately']}}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('action', choices=['init', 'status', 'handoff', 'fingerprint', 'reference-gate', 'gate'])
    p.add_argument('manifest', type=Path)
    p.add_argument('--batch'); p.add_argument('--candidate')
    args = p.parse_args()
    root = Path(__file__).resolve().parents[2]
    try:
        if args.action == 'init':
            if not args.batch or not re.fullmatch('batch-[0-9]{3}', args.batch):
                raise ValueError('init requires --batch batch-NNN and --candidate ID')
            ledger = json.loads((root / 'docs/compatibility-candidates' / (args.batch + '.json')).read_text())
            matches = [c for c in ledger['candidates'] if c['id'] == args.candidate]
            if len(matches) != 1:
                raise ValueError('Select one known candidate')
            with args.manifest.open('x') as output:
                json.dump(create(matches[0], args.batch), output, indent=2); output.write('\n')
            return
        m = json.loads(args.manifest.read_text())
        if args.action == 'handoff':
            print(json.dumps(handoff(m, root), indent=2))
        elif args.action == 'fingerprint':
            print(json.dumps({'common': fingerprint(m), 'unity': unity_fingerprint(m) if m['unity']['reference_input_cases'] else None}, indent=2))
        else:
            unmet = blockers(m, root, phase='reference' if args.action == 'reference-gate' else 'complete')
            print(json.dumps({'gate': args.action, 'complete': not unmet, 'blockers': unmet}, indent=2))
            if args.action in ('gate', 'reference-gate') and unmet:
                raise SystemExit(2)
    except (ValueError, OSError, KeyError, TypeError) as error:
        p.error(str(error))


if __name__ == '__main__':
    main()
