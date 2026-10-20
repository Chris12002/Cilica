from pathlib import Path
import os,subprocess,json,statistics
out=Path(__file__).resolve().parent;host='/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs/host/DotNetHost.dll'
commands={'baseline':[str(out.parent/'v111-profile-20261020/baseline'),'measure','1','1024'],'v115':[str(out.parent/'v115-collector-20261020/v115'),'measure','1','1024'],'dotnet':['dotnet',host,'measure','1','8192']}
env={k:v for k,v in os.environ.items() if not k.startswith(('DOTNET_Tier','COMPlus_Tier','DOTNET_Jit','COMPlus_Jit','DOTNET_ReadyToRun','COMPlus_ReadyToRun'))};env.update(DOTNET_TieredCompilation='0',DOTNET_ReadyToRun='0');results={n:[] for n in commands};processes={n:[] for n in commands}
for repeat in range(3):
 names=list(commands);names=names[repeat:]+names[:repeat]
 if repeat%2:names.reverse()
 for name in names:
  raw=subprocess.check_output(commands[name],env=env,text=True,timeout=60);(out/f'final-line-{name}-{repeat}.jsonl').write_text(raw);rows=[json.loads(x) for x in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==3122*r['iterations'] for r in rows[1:]);values=[r['ns']/r['iterations'] for r in rows[1:]];results[name]+=values;processes[name].append(statistics.median(values))
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'process_medians':processes[n],'reduction_percent':100*(1-statistics.median(v)/statistics.median(results['baseline']))} for n,v in results.items()}
(out/'final-line-summary.json').write_text(json.dumps({'commands':commands,'results':summary},indent=2));print(json.dumps(summary,indent=2))
