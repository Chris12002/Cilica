from pathlib import Path
import subprocess,json,statistics,os,hashlib
out=Path(__file__).resolve().parent
host=Path('/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs/host/DotNetHost.dll')
old=out.parent/'string-pool-performance-20261020/baseline'
commands={'v104':[str(old),'measure','0','256'],'v107':[str(out/'outputs'),'measure','0','256'],'dotnet':['dotnet',str(host),'measure','0','16384']}
results={n:[] for n in commands}
env={k:v for k,v in os.environ.items() if not k.startswith(('DOTNET_Tier','COMPlus_Tier','DOTNET_Jit','COMPlus_Jit','DOTNET_ReadyToRun','COMPlus_ReadyToRun'))}
env.update(DOTNET_TieredCompilation='0',DOTNET_ReadyToRun='0')
for repeat in range(3):
 order=list(commands) if repeat!=1 else list(reversed(commands))
 for name in order:
  raw=subprocess.check_output(commands[name],text=True,env=env,timeout=90)
  (out/f'final-timing-{name}-{repeat}.jsonl').write_text(raw)
  rows=[json.loads(l) for l in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:])
  results[name]+=[r['ns']/r['iterations'] for r in rows[1:]]
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v)} for n,v in results.items()}
(out/'final-summary.json').write_text(json.dumps(summary,indent=2))
(out/'final-provenance.json').write_text(json.dumps({'commands':commands,'dotnet_env':{'DOTNET_TieredCompilation':'0','DOTNET_ReadyToRun':'0'},'sha256':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in [old,out/'outputs',host,host.parent.parent/'dll/DiffPlex.Benchmark.dll',host.parent.parent/'dll/DiffPlex.Benchmark.pdb']},'revision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()},indent=2))
print(json.dumps(summary,indent=2))
