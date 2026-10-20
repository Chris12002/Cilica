"""Paired v116 / v117 / .NET / Unity release timing (protocol of substring-20261020/compare.py)."""
from pathlib import Path
import subprocess, json, statistics, hashlib, os

out = Path(__file__).resolve().parent
root = Path.cwd()
worktree = Path('/Users/christian/.codex/worktrees/profile-v107/Cilica')
app = worktree / 'artifacts/section-timing-20261020/Build/Control.app'
before = worktree / 'artifacts/substring-20261020/substring'  # accepted v116 executable
candidate = root / 'artifacts/scalar-locals-20261020/locals'  # moved from this directory after emission
host = Path('/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs/host/DotNetHost.dll')
assert hashlib.sha256(before.read_bytes()).hexdigest() == '0b73e178b48f31285b193224bff8fd36339d51ad307ece971b5c0a322076886e'
env = {k: v for k, v in os.environ.items() if not k.startswith(('DOTNET_Tier', 'COMPlus_Tier', 'DOTNET_Jit', 'COMPlus_Jit', 'DOTNET_ReadyToRun', 'COMPlus_ReadyToRun'))}
env.update(DOTNET_TieredCompilation='0', DOTNET_ReadyToRun='0')
commands, results, processes = [], {}, {}

def run(name, work, mode, rep):
    tag = f'compare-{name}-{work}-{mode}-{rep}'
    expected = 6146 if work == 0 else 3122
    if name == 'unity':
        result = out / (tag + '.json'); assert not result.exists()
        cmd = ['open', '-W', '-n', str(app), '--args', '-batchmode', '-nographics', '-logFile', str(out / (tag + '.player.log')),
               '--mode', mode, '--work', str(work), '--iterations', '4096', '--timers', 'off', '--result', str(result)]
        r = subprocess.run(cmd, text=True, capture_output=True, timeout=60)
        (out / (tag + '.launch.log')).write_text(r.stdout + r.stderr); r.check_returncode()
        data = json.loads(result.read_text()); assert not data.get('error'); rows = data.get('samples')
    else:
        cmd = ([str(before)] if name == 'v116' else [str(candidate)] if name == 'v117' else ['dotnet', str(host)]) \
            + [mode, str(work), '1024' if name != 'dotnet' else '8192']
        raw = subprocess.check_output(cmd, text=True, env=env, timeout=60)
        (out / (tag + '.jsonl')).write_text(raw)
        parts = [json.loads(l) for l in raw.splitlines()]; data = parts[0]; rows = parts[1:]
    commands.append(cmd)
    if mode == 'verify':
        assert data['values'] == json.loads((root / f'docs/performance/splitcache-20261020/before-verify-work{work}.json').read_text())['values']
        return
    assert len(rows) == 7 and all(r['checksum'] == expected * r['iterations'] for r in rows)
    v = [r['elapsedTicks'] * 1e9 / r['frequency'] / r['iterations'] if name == 'unity' else r['ns'] / r['iterations'] for r in rows]
    key = f'{name}-{work}'
    results.setdefault(key, []).extend(v); processes.setdefault(key, []).append(statistics.median(v))
    print('measured', tag, flush=True)

for work in (0, 1):
    for name in ('dotnet', 'unity'): run(name, work, 'verify', 0)
for rep in range(3):
    for work in ([0, 1] if rep % 2 == 0 else [1, 0]):
        names = ['v116', 'v117', 'dotnet', 'unity']; names = names[rep:] + names[:rep]
        if rep % 2: names.reverse()
        for name in names: run(name, work, 'measure', rep)
summary = {k: {'median_us': statistics.median(v) / 1000, 'min_us': min(v) / 1000, 'max_us': max(v) / 1000,
               'process_medians_us': [x / 1000 for x in processes[k]], 'samples': len(v)} for k, v in results.items()}
for work in (0, 1):
    base = summary[f'v116-{work}']['median_us']
    summary[f'v117-{work}']['reduction_percent_vs_v116'] = 100 * (1 - summary[f'v117-{work}']['median_us'] / base)
(out / 'comparison-summary.json').write_text(json.dumps(summary, indent=2))
files = [host, before, candidate, app.parent / 'Control.app.settings.json', *(app / 'Contents/MacOS').iterdir(), *app.rglob('GameAssembly.dylib')]
(out / 'comparison-provenance.json').write_text(json.dumps({'commands': commands, 'files': {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in files},
    'dotnet_tiered': False, 'dotnet_readytorun': False, 'unity_rebuilt': False, 'unity_retimed': True}, indent=2))
print(json.dumps(summary, indent=2))
