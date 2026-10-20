from pathlib import Path
import json, hashlib, subprocess, shutil

out = Path(__file__).resolve().parent
dest = Path('docs/performance/v104-profile-20261020')
dest.mkdir(exist_ok=True)
sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
refs = json.loads(Path('artifacts/v101-profile-20261020/reference-sources.json').read_text())
for path, old in refs.items():
    # Compiler files changed in v102-v104; all external/library sources must match.
    if not path.startswith('src/Cilica.Compiler/'): assert sha(path) == old, path
    refs[path] = sha(path)
unity = Path('/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app/Contents/Resources/Scripting/il2cpp/libil2cpp')
for path in [unity/'vm/Object.cpp', unity/'il2cpp-config.h', Path('library/provenance.json')]:
    refs[str(path)] = sha(path)
(out/'reference-sources.json').write_text(json.dumps({
    'files': refs,
    'roslyn': json.loads(Path('artifacts/roslyn-il-investigation-20261019/inspection-summary.json').read_text())['roslyn_build_options'],
    'roslyn_source_revision': 'e4e2833be7abbc54776788ca35d5c4d2697b0261',
    'exact_roslyn_source_match': False,
    'unity_editor': '6000.3.2f1',
    'unity_timing_rerun': False,
    'dotnet_upstream': json.loads(Path('library/provenance.json').read_text())['upstream'],
    'web_sources': [
        'https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/fundamentals',
        'https://docs.unity.cn/6000.3/Documentation/Manual/performance-incremental-garbage-collection.html'
    ]
}, indent=2))
inputs = Path('/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs')
paths = [out/'baseline.cpp', out/'baseline', out/'profile', out/'baseline-core/Cilica.Core.dll',
    out/'baseline-core/build-options.json', *out.glob('sample-*.txt'), out/'native-arm64.txt',
    out/'inline-native-arm64.txt', Path('artifacts/roslyn-il-investigation-20261019/DiffPlex.Benchmark.il'),
    Path('docs/performance/runtime-costs-20261020/dotnet-arm64.txt'), inputs/'dll/DiffPlex.Benchmark.dll',
    inputs/'dll/DiffPlex.Benchmark.pdb', inputs/'host/DotNetHost.dll']
identity = {
    'revision': subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),
    'compiler_tree_clean': not subprocess.check_output(['git','diff','HEAD','--','src/Cilica.Compiler','library/Cilica.Core'],text=True),
    'fresh_cpp_matches_accepted_v104': (out/'baseline.cpp').read_bytes() == Path('artifacts/local-character-performance-20261020/committed-local.cpp').read_bytes(),
    'compiler_files': {str(p):sha(p) for p in Path('src/Cilica.Compiler').glob('*.cs')},
    'files': {str(p):sha(p) for p in paths}
}
assert identity['compiler_tree_clean'] and identity['fresh_cpp_matches_accepted_v104']
(out/'identity.json').write_text(json.dumps(identity,indent=2))
(out/'raw-timings.json').write_text(json.dumps({p.name:[json.loads(l) for l in p.read_text().splitlines()]
    for p in sorted(out.glob('*.jsonl')) if p.name.startswith(('timing-','experiment-','followup-'))},indent=2))
for p in out.glob('*.py'): shutil.copy2(p,dest/p.name)
for name in ['identity.json','profile-provenance.json','timing-summary.json','profile-aggregate.json',
    'experiment-provenance.json','experiment-summary.json','followup-provenance.json','followup-summary.json',
    'acceptance-results.json','counts-summary.json','metrics-summary.json','method-counts.json',
    'native-summary.json','native-selected.txt','reference-sources.json','raw-timings.json']:
    shutil.copy2(out/name,dest/name)
for name in ['build-options.json','library-build.json']:
    path=out/'baseline-core'/name
    if path.exists(): shutil.copy2(path,dest/('core-'+name))
# The core hash is unchanged; reuse exact prior metadata, not another assembly.
prior = json.loads(Path('artifacts/v101-profile-20261020/profile-provenance.json').read_text())
assert prior['core_sha256'] == sha(out/'baseline-core/Cilica.Core.dll')
shutil.copy2(Path('artifacts/v101-profile-20261020/rea-selected.json'),dest/'rea-selected.json')
(dest/'pdb-module-info.txt').write_text(Path('artifacts/roslyn-il-investigation-20261019/pdb-module-info.txt').read_text().replace('\0','\n').rstrip()+'\n')
print('Packaged',len(list(dest.iterdir())),'files; compiler unchanged')
