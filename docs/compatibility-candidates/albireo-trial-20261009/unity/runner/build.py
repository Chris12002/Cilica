"""Build the exact approved Albireo template using installed Unity, with retained bounds."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parent
CLI = '/Users/christian/Documents/Codex/2026-10-08/task-2/tooling/unity-cli/unity'
ADAPTER = ROOT / 'frozen/tools/CompatibilityCandidates/albireo-unity'
PROJECT = ROOT / 'projects/CilicaAlbireo'
APP = PROJECT / 'Build/AlbireoReference.app'

def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def call(name, command):
    log = ROOT / 'build' / (name + '.log')
    result = subprocess.run([sys.executable, str(ROOT/'frozen/legacy-runner/run_bounded.py'),
                             '--log', str(log), '--seconds', '1790', '--', *command], cwd=ROOT)
    if result.returncode: raise SystemExit(result.returncode)
    return log

def verify_inputs():
    h = json.loads((ROOT/'handoff/handoff.json').read_text())
    for name, expected in h['files'].items(): assert digest(ROOT/'handoff'/name) == expected, name
    for name, expected in h['owned_inputs'].items(): assert digest(ROOT/'frozen'/name) == expected, name
    return h

def main():
    h = verify_inputs()
    assert json.loads((ROOT/'handoff-verification.json').read_text())['status'] == 'passed'
    os.environ['UNITY_NO_CRASH_REPORT'] = '1'; os.environ['UNITY_NO_UPDATE_CHECK'] = '1'
    if sys.argv[1:] == ['--resume-created']:
        assert PROJECT.is_dir() and not APP.exists() and not (ROOT/'build/unity-build.log').exists()
        create_log = ROOT/'build/create.log'
        assert json.loads(Path(str(create_log)+'.receipt.json').read_text())['exit_code'] == 0
    else:
        assert not sys.argv[1:]
        assert not PROJECT.exists() and not (ROOT/'staged').exists()
        spec = importlib.util.spec_from_file_location('stage_bundle', ADAPTER/'stage_bundle.py')
        stage = importlib.util.module_from_spec(spec); spec.loader.exec_module(stage)
        stage.stage(ADAPTER, ROOT/'handoff/Albireo.SelectedDecode.dll', h['candidate_dll_sha256'],
                    ROOT/'handoff/LICENSE.Albireo.txt', ROOT/'staged')
        (ROOT/'projects').mkdir()
        create_log = call('create', [CLI, 'projects', 'create', PROJECT.name, '--path', str(PROJECT.parent),
            '--editor-version', '6000.3.2f1', '--architecture', 'arm64', '--template', str(ROOT/'staged/template'),
            '--no-cloud', '--non-interactive', '--format', 'json'])
    create = json.loads(create_log.read_text()); assert create['success'], 'CLI project creation did not succeed'
    assert (PROJECT/'ProjectSettings/ProjectVersion.txt').is_file()
    template = ADAPTER/'template/ProjectData~'
    # CLI create expanded the frozen manifest with default dependencies. Preserve
    # that entire generated state, then restore exact approved input before build.
    # No Editor is left running by projects create (opened:false).
    if digest(PROJECT/'Packages/manifest.json') != digest(template/'Packages/manifest.json'):
        assert create['data']['opened'] is False
        archive = ROOT/'build/create-generated-state'; archive.mkdir()
        before = json.loads((PROJECT/'Packages/manifest.json').read_text())
        shutil.copy2(PROJECT/'Packages/manifest.json', archive/'manifest.json')
        for relative in ['Packages/packages-lock.json', 'Library', 'Logs']:
            source = PROJECT/relative
            if source.exists(): shutil.move(str(source), str(archive/source.name))
        shutil.copyfile(template/'Packages/manifest.json', PROJECT/'Packages/manifest.json')
        fix = {'reason':'CLI create injected default dependencies; guard stopped before IL2CPP build',
               'preserved_state':str(archive), 'created_dependencies':before,
               'restored_sha256':digest(PROJECT/'Packages/manifest.json'),
               'frozen_adapter_modified':False, 'scope':'Owned outer runner only'}
        (ROOT/'build/manifest-restoration.json').write_text(json.dumps(fix, indent=2)+'\n')
    for relative in ['Assets/AlbireoUnityHarness.cs', 'Assets/Editor/AlbireoReferenceBuild.cs', 'Packages/manifest.json']:
        assert digest(PROJECT/relative) == digest(template/relative), relative
    assert digest(PROJECT/'Assets/Plugins/Albireo.SelectedDecode.dll') == h['candidate_dll_sha256']
    call('unity-build', [CLI, 'run', str(PROJECT), '--editor-version', '6000.3.2f1', '--timeout', '1780', '--',
            '-nographics', '-logFile', '-', '-job-worker-count', '2', '-executeMethod', 'AlbireoReferenceBuild.Build',
            '--cilica-output', str(APP)])
    settings = json.loads(Path(str(APP)+'.settings.json').read_text()); assert settings['result'] == 'Succeeded'
    verify_inputs()
    native = {str(f.relative_to(APP)):digest(f) for f in sorted(APP.rglob('*'))
              if f.is_file() and not f.is_symlink() and (f.suffix == '.dylib' or f.parent == APP/'Contents/MacOS')}
    assert native, 'No native output'
    result = {'input_fingerprint': h['input_fingerprint'], 'candidate_dll_sha256': h['candidate_dll_sha256'],
              'native_sha256': native, 'requested_settings_receipt': settings, 'status': 'passed',
              'boundary': 'Normal Mac account; wall/log limits only; no competitive timing claim'}
    (ROOT/'build/build-provenance.json').write_text(json.dumps(result, indent=2)+'\n')
    print(json.dumps({'status':'passed', 'native_files':len(native), 'app':str(APP)}))

if __name__ == '__main__': main()
