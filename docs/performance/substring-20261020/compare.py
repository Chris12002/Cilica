from pathlib import Path
import subprocess,json,statistics,hashlib,os
out=Path(__file__).resolve().parent;root=Path.cwd();section=out.parent/'section-timing-20261020'
app=section/'Build/Control.app';host=Path('/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs/host/DotNetHost.dll')
env={k:v for k,v in os.environ.items() if not k.startswith(('DOTNET_Tier','COMPlus_Tier','DOTNET_Jit','COMPlus_Jit','DOTNET_ReadyToRun','COMPlus_ReadyToRun'))};env.update(DOTNET_TieredCompilation='0',DOTNET_ReadyToRun='0')
commands=[];results={};processes={}
def run(n,w,mode,rep):
 tag=f'compare-{n}-{w}-{mode}-{rep}';expected=6146 if w==0 else 3122
 if n=='unity':
  result=out/(tag+'.json');assert not result.exists()
  cmd=['open','-W','-n',str(app),'--args','-batchmode','-nographics','-logFile',str(out/(tag+'.player.log')),'--mode',mode,'--work',str(w),'--iterations','4096','--timers','off','--result',str(result)]
  r=subprocess.run(cmd,text=True,capture_output=True,timeout=60);(out/(tag+'.launch.log')).write_text(r.stdout+r.stderr);r.check_returncode();data=json.loads(result.read_text());assert not data.get('error');rows=data.get('samples')
 else:
  cmd=([str(section/'plain')] if n=='before' else [str(out/'substring')] if n=='substring' else ['dotnet',str(host)])+[mode,str(w),'1024' if n!='dotnet' else '8192']
  raw=subprocess.check_output(cmd,text=True,env=env,timeout=60);(out/(tag+'.jsonl')).write_text(raw);parts=[json.loads(l) for l in raw.splitlines()];data=parts[0];rows=parts[1:]
 commands.append(cmd)
 if mode=='verify':
  assert data['values']==json.loads((root/f'docs/performance/splitcache-20261020/before-verify-work{w}.json').read_text())['values'];return
 assert len(rows)==7 and all(r['checksum']==expected*r['iterations'] for r in rows)
 v=[r['elapsedTicks']*1e9/r['frequency']/r['iterations'] if n=='unity' else r['ns']/r['iterations'] for r in rows]
 key=f'{n}-{w}';results.setdefault(key,[]).extend(v);processes.setdefault(key,[]).append(statistics.median(v));print('measured',tag,flush=True)
for w in [0,1]:
 for n in ['dotnet','unity']:run(n,w,'verify',0)
for rep in range(3):
 for w in ([0,1] if rep%2==0 else [1,0]):
  names=['before','substring','dotnet','unity'];names=names[rep:]+names[:rep]
  if rep%2:names.reverse()
  for n in names:run(n,w,'measure',rep)
summary={k:{'median_us':statistics.median(v)/1000,'min_us':min(v)/1000,'max_us':max(v)/1000,'process_medians_us':[x/1000 for x in processes[k]],'samples':len(v)} for k,v in results.items()}
(out/'comparison-summary.json').write_text(json.dumps(summary,indent=2))
files=[host,section/'plain',out/'substring',app.parent/'Control.app.settings.json',*(app/'Contents/MacOS').iterdir(),*app.rglob('GameAssembly.dylib')]
(out/'comparison-provenance.json').write_text(json.dumps({'commands':commands,'files':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in files},'dotnet_tiered':False,'dotnet_readytorun':False,'unity_rebuilt':False,'unity_retimed':True},indent=2));print(json.dumps(summary,indent=2))
