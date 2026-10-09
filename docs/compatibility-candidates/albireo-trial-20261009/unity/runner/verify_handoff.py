"""Freeze and independently verify the approved peer export; never load candidate IL."""
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parent
SOURCE = Path('/Users/christian/Documents/Codex/2026-10-08/task/albireo-approved-20261009/handoff')
REPO = Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica')
PIN = 'dbfcca2deac3b941e0a857afaf92eab68a04c7d3'
FP = 'd79e8c4d539d8f1e486b50360ac2f6ec3196d1a851da29e20ec61e08554a87eb'
DLL = 'a7fefd9010e223e7e58809c7c34a946e3fb5d7294522ab3cc2ff20d2352d4760'

def sha(data): return hashlib.sha256(data).hexdigest()
def blob(data): return hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result

def main():
    hdata = (SOURCE / 'handoff.json').read_bytes(); h = json.loads(hdata)
    assert h['adapter_commit'] == PIN and h['input_fingerprint'] == FP
    assert h['candidate_dll_sha256'] == DLL and h['isolated_status'] == 'passed'
    assert h['oracle_observations'] == 851 and h['scenarios'] == 32
    data = {}
    for name, digest in h['files'].items():
        assert Path(name).name == name and not (SOURCE / name).is_symlink()
        data[name] = (SOURCE / name).read_bytes()
        assert sha(data[name]) == digest, name
    assert blob(data['Base32.cs']) == 'bd6584e246151e3e493ed1eb9f17de269cb1ddc1'
    assert blob(data['LICENSE.Albireo.txt']) == '1b3da92904fd9e9c1f4f327fafa5c7e0c771b150'
    manifest = json.loads(data['workflow-manifest.json'])
    workflow = module('reference_workflow', ROOT / 'frozen/tools/CompatibilityCandidates/reference_workflow.py')
    transcript = module('transcript', ROOT / 'frozen/tools/CompatibilityCandidates/albireo-unity/transcript.py')
    assert workflow.fingerprint(manifest) == FP == workflow.unity_fingerprint(manifest)
    assert manifest['scope']['owned_inputs'] == h['owned_inputs'] == manifest['unity']['owned_inputs']
    assert manifest['scope']['input_cases']['sha256'] == sha(data['input-cases.json'])
    assert manifest['scope']['candidate_assemblies'] == {'Albireo.SelectedDecode.dll': DLL}
    for name, digest in h['owned_inputs'].items():
        assert not Path(name).is_absolute() and '..' not in Path(name).parts
        target = ROOT / 'frozen' / name
        expected = subprocess.check_output(['git', '-C', str(REPO), 'show', PIN + ':' + name])
        assert sha(expected) == digest, name
        if target.exists(): assert target.read_bytes() == expected, name
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            with target.open('xb') as stream: stream.write(expected)
    expected = transcript.normalize(json.loads(data['oracle-observations.json']), FP)
    assert expected == json.loads(data['expected.json']) and len(expected['values']) == 851 * 3
    dest = ROOT / 'handoff'; dest.mkdir()
    for name, content in {**data, 'handoff.json': hdata}.items():
        (dest / name).write_bytes(content)
        assert sha((dest / name).read_bytes()) == sha(content)
    result = {'status': 'passed', 'candidate_commit': h['candidate_commit'], 'adapter_commit': PIN,
              'input_fingerprint': FP, 'files_checked': len(data), 'owned_inputs_checked': len(h['owned_inputs']),
              'oracle_observations': 851, 'sha256': {'handoff.json': sha(hdata), **h['files']},
              'candidate_code_executed': False}
    (ROOT / 'handoff-verification.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({k:v for k,v in result.items() if k != 'sha256'}))

if __name__ == '__main__': main()
