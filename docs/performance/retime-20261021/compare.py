"""Six-backend DiffPlex retime on a quiet host: Cilica v116/v117, .NET (FullOpts and
default tiered), NativeAOT and the retained Unity IL2CPP player. Run from the repo root."""
from pathlib import Path
import subprocess, json, statistics, hashlib, os

out = Path(__file__).resolve().parent
root = Path.cwd()
worktree = Path('/Users/christian/.codex/worktrees/profile-v107/Cilica')
app = worktree / 'artifacts/section-timing-20261020/Build/Control.app'
v116 = worktree / 'artifacts/substring-20261020/substring'
v117 = root / 'artifacts/scalar-locals-20261020/locals'
host = Path('/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs/host/DotNetHost.dll')
reference = root / 'artifacts/retime-20261021'
expected_hashes = {v116: '0b73e178b48f31285b193224bff8fd36339d51ad307ece971b5c0a322076886e',
                   v117: 'df9267112557946865c057fb5a5c22f43c844a35f4f114711e66fb8e1df334f8'}
sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
for path, digest in expected_hashes.items(): assert sha(path) == digest, path

clean = {k: v for k, v in os.environ.items() if not k.startswith(('DOTNET_', 'COMPlus_')) or k == 'DOTNET_ROOT'}
fullopts = dict(clean, DOTNET_TieredCompilation='0', DOTNET_ReadyToRun='0')
backends = {
    'cilica-v116': ([str(v116)], clean, '1024', []),
    'cilica-v117': ([str(v117)], clean, '1024', []),
    'dotnet-fullopts': (['dotnet', str(host)], fullopts, '8192', []),
    'dotnet-default': (['dotnet', str(reference / 'jit/AotHost.dll')], clean, '8192', ['5000']),
    'nativeaot': ([str(reference / 'aot/AotHost')], clean, '8192', []),
    'unity': (None, None, '4096', []),
}
commands, results, processes = [], {}, {}

def run(name, work, mode, rep):
    tag = f'compare-{name}-{work}-{mode}-{rep}'
    expected = 6146 if work == 0 else 3122
    prefix, env, iterations, extra = backends[name]
    if name == 'unity':
        result = out / (tag + '.json'); assert not result.exists()
        cmd = ['open', '-W', '-n', str(app), '--args', '-batchmode', '-nographics', '-logFile', str(out / (tag + '.player.log')),
               '--mode', mode, '--work', str(work), '--iterations', iterations, '--timers', 'off', '--result', str(result)]
        r = subprocess.run(cmd, text=True, capture_output=True, timeout=120)
        (out / (tag + '.launch.log')).write_text(r.stdout + r.stderr); r.check_returncode()
        data = json.loads(result.read_text()); assert not data.get('error'); rows = data.get('samples')
    else:
        cmd = prefix + ([mode, str(work)] if mode == 'verify' else [mode, str(work), iterations] + extra)
        raw = subprocess.check_output(cmd, text=True, env=env, timeout=120)
        (out / (tag + '.jsonl')).write_text(raw)
        parts = [json.loads(l) for l in raw.splitlines()]; data = parts[0]; rows = parts[1:]
    commands.append(cmd)
    if mode == 'verify':
        assert data['values'] == json.loads((root / f'docs/performance/splitcache-20261020/before-verify-work{work}.json').read_text())['values'], tag
        return
    assert len(rows) == 7 and all(r['checksum'] == expected * r['iterations'] for r in rows), tag
    v = [r['elapsedTicks'] * 1e9 / r['frequency'] / r['iterations'] if name == 'unity' else r['ns'] / r['iterations'] for r in rows]
    key = f'{name}-{work}'
    results.setdefault(key, []).extend(v); processes.setdefault(key, []).append(statistics.median(v))
    print('measured', tag, flush=True)

for work in (0, 1):
    for name in backends: run(name, work, 'verify', 0)
for rep in range(3):
    for work in ([0, 1] if rep % 2 == 0 else [1, 0]):
        names = list(backends); names = names[rep:] + names[:rep]
        if rep % 2: names.reverse()
        for name in names: run(name, work, 'measure', rep)
summary = {k: {'median_us': statistics.median(v) / 1000, 'min_us': min(v) / 1000, 'max_us': max(v) / 1000,
               'process_medians_us': [x / 1000 for x in processes[k]], 'samples': len(v)} for k, v in results.items()}
(out / 'comparison-summary.json').write_text(json.dumps(summary, indent=2))
files = [host, v116, v117, reference / 'aot/AotHost', reference / 'jit/AotHost.dll',
         app.parent / 'Control.app.settings.json', *(app / 'Contents/MacOS').iterdir(), *app.rglob('GameAssembly.dylib'),
         Path('/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs/dll/DiffPlex.Benchmark.dll')]
(out / 'comparison-provenance.json').write_text(json.dumps({
    'commands': commands, 'files': {str(p): sha(p) for p in files},
    'dotnet_runtime': subprocess.check_output(['dotnet', '--list-runtimes'], text=True).splitlines(),
    'dotnet_fullopts_env': {'DOTNET_TieredCompilation': '0', 'DOTNET_ReadyToRun': '0'},
    'dotnet_default': 'no DOTNET_/COMPlus_ overrides; tiered compilation, ReadyToRun and Dynamic PGO at defaults; 5000 ms warm-up',
    'nativeaot': 'PublishAot Release osx-arm64, symbols retained, no System.Text.Json; reference only',
    'unity_rebuilt': False, 'unity_retimed': True}, indent=2))
print(json.dumps(summary, indent=2))
