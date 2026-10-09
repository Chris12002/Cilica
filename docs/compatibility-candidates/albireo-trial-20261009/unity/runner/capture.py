"""Albireo-specific data capture, preserving generated sources/settings/commands <=1 GiB."""
import hashlib
import json
from pathlib import Path
import shutil
from build import ROOT, PROJECT, APP, verify_inputs, digest

RUNTIME = Path('/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app/Contents/Resources/Scripting/il2cpp/libil2cpp')
SYMBOLS = ['AlbireoDecodeProbe_Run', 'Base32_Decode']

def main():
    h = verify_inputs()
    build = json.loads((ROOT/'build/build-provenance.json').read_text())
    for relative, expected in build['native_sha256'].items(): assert digest(APP/relative) == expected
    out = ROOT/'capture'; out.mkdir()
    rows = []; total = 0
    def copy(source, relative):
        nonlocal total
        assert source.is_file() and not source.is_symlink()
        size = source.stat().st_size
        if total + size > 1024**3: raise RuntimeError('1 GiB capture limit; partial output retained')
        target = out/relative; target.parent.mkdir(parents=True, exist_ok=True)
        assert not target.exists()
        shutil.copy2(source, target); total += size
        rows.append({'path':str(relative), 'bytes':size, 'sha256':digest(target), 'origin':str(source)})
    for sub in ['Library/Bee', 'Temp', 'ProjectSettings', 'Packages', 'Library/ScriptAssemblies']:
        tree = PROJECT/sub
        if not tree.exists(): continue
        for source in sorted(tree.rglob('*')):
            if not source.is_file() or source.is_symlink(): continue
            if sub.startswith(('ProjectSettings', 'Packages')) or source.suffix.lower() in {
                '.cpp','.h','.hpp','.rsp','.json','.txt','.log','.dll','.pdb'}:
                assert source.resolve().is_relative_to(PROJECT)
                copy(source, Path('project')/source.relative_to(PROJECT))
    for relative in ['Library/LastBuild.buildreport','Library/EditorUserBuildSettings.asset',
                     'Assets/Plugins/Albireo.SelectedDecode.dll']:
        source = PROJECT/relative
        if source.is_file(): copy(source, Path('project')/relative)
    for relative in ['codegen/il2cpp-codegen.h','gc/GarbageCollector.h','gc/BoehmGC.cpp',
                     'vm/Object.cpp','vm/Object.h','il2cpp-object-internals.h',
                     'il2cpp-class-internals.h']:
        copy(RUNTIME/relative, Path('runtime-reference')/relative)
    for source in sorted((ROOT/'build').glob('*.json')):
        copy(source, Path('build-records')/source.name)
    for source in sorted((ROOT/'build').glob('*.log')):
        copy(source, Path('build-records')/source.name)
    copy(Path(str(APP)+'.settings.json'), Path('build-records/requested-settings.json'))
    hits = {s:[] for s in SYMBOLS}
    for row in rows:
        if not row['path'].endswith('.cpp'): continue
        for line, text in enumerate((out/row['path']).read_text(errors='replace').splitlines(), 1):
            for symbol in SYMBOLS:
                if symbol in text:
                    hits[symbol].append({'path':row['path'], 'line':line, 'text':text[:800], 'sha256':row['sha256']})
    result = {'input_fingerprint':h['input_fingerprint'], 'candidate_dll_sha256':h['candidate_dll_sha256'],
              'bytes':total, 'files':rows, 'symbol_locations':hits,
              'cpp_files':sum(r['path'].endswith('.cpp') for r in rows),
              'status':'passed' if all(hits.values()) else 'missing-generated-symbol'}
    (out/'manifest.json').write_text(json.dumps(result, indent=2)+'\n')
    assert all(hits.values()), 'Required generated symbols missing'
    print(json.dumps({k:v for k,v in result.items() if k not in ['files','symbol_locations']}))

if __name__ == '__main__': main()
