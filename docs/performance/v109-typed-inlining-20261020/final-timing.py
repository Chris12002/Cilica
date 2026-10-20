from pathlib import Path
import subprocess,json,statistics,os,hashlib
out=Path(__file__).resolve().parent;host=Path('/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs/host/DotNetHost.dll')
commands={n:[str(p),'measure','0','1024'] for n,p in {'v107':out.parent/'v107-profile-20261020/baseline','v108':out.parent/'v108-small-predicates-20261020/v108','v109':out/'v109'}.items()};commands['dotnet']=['dotnet',str(host),'measure','0','16384']
results={n:[] for n in commands};env={k:v for k,v in os.environ.items() if not k.startswith(('DOTNET_Tier','COMPlus_Tier','DOTNET_Jit','COMPlus_Jit','DOTNET_ReadyToRun','COMPlus_ReadyToRun'))};env.update(DOTNET_TieredCompilation='0',DOTNET_ReadyToRun='0')
for repeat in range(3):
 names=list(commands);names=names[repeat:]+names[:repeat]
 if repeat%2:names.reverse()
 for name in names:
  raw=subprocess.check_output(commands[name],text=True,env=env,timeout=60);(out/f'final-{name}-{repeat}.jsonl').write_text(raw);rows=[json.loads(l) for l in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:]);results[name]+=[r['ns']/r['iterations'] for r in rows[1:]]
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'reduction_percent':100*(1-statistics.median(v)/statistics.median(results['v107']))} for n,v in results.items()}
(out/'final-summary.json').write_text(json.dumps(summary,indent=2));(out/'final-provenance.json').write_text(json.dumps({'commands':commands,'host_sha256':hashlib.sha256(host.read_bytes()).hexdigest(),'tiered_compilation':0,'ready_to_run':0,'candidate_cpp_sha256':hashlib.sha256((out/'v109.cpp').read_bytes()).hexdigest()},indent=2));print(json.dumps(summary,indent=2))
