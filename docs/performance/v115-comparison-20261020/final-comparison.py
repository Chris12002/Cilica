from pathlib import Path
import subprocess,json,statistics,hashlib,os,time
out=Path(__file__).resolve().parent
app=Path('/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/CilicaDiffPlex/Build/DiffPlex.app')
inputs=app.parents[2]/'inputs';host=inputs/'host/DotNetHost.dll'
expected=json.loads((out.parent/'v111-profile-20261020/baseline-verify.json').read_text())
env={k:v for k,v in os.environ.items() if not k.startswith(('DOTNET_Tier','COMPlus_Tier','DOTNET_Jit','COMPlus_Jit','DOTNET_ReadyToRun','COMPlus_ReadyToRun'))};env.update(DOTNET_TieredCompilation='0',DOTNET_ReadyToRun='0')
results={n:[] for n in ['cilica_v111','cilica_v115','unity','dotnet']};processes={n:[] for n in results};commands=[]
for repeat in range(3):
 names=list(results);names=names[repeat:]+names[:repeat]
 if repeat%2:names.reverse()
 for name in names:
  if name=='unity':
   output=out/f'final-unity-{repeat}.json';assert not output.exists()
   cmd=['open','-W','-n',str(app),'--args','-batchmode','-nographics','-logFile',str(out/f'final-unity-{repeat}.player.log'),'--cilica-mode','steady','--cilica-iterations','4096','--cilica-result',str(output)]
   r=subprocess.run(cmd,capture_output=True,text=True,timeout=60);(out/f'final-unity-{repeat}.launch.log').write_text(r.stdout+r.stderr);r.check_returncode();d=json.loads(output.read_text());assert d['error']=='' and d['checksum']==6146*d['iterations'] and len(d['elapsedTicks'])==7
   v=[x*1e9/d['frequency']/d['iterations'] for x in d['elapsedTicks']]
  else:
   cmd=[str(out.parent/('v111-profile-20261020/baseline' if name=='cilica_v111' else 'v115-collector-20261020/v115')),'measure','0','1024'] if name!='dotnet' else ['dotnet',str(host),'measure','0','16384']
   raw=subprocess.check_output(cmd,text=True,env=env,timeout=60);(out/f'final-{name}-{repeat}.jsonl').write_text(raw);rows=[json.loads(l) for l in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:]);v=[r['ns']/r['iterations'] for r in rows[1:]]
  commands.append(cmd);results[name]+=v;processes[name].append(statistics.median(v));print('measured',name,repeat,flush=True)
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'process_medians':processes[n],'ratio_to_dotnet':statistics.median(v)/statistics.median(results['dotnet']),'ratio_to_unity':statistics.median(v)/statistics.median(results['unity'])} for n,v in results.items()}
(out/'final-three-backend-summary.json').write_text(json.dumps(summary,indent=2))
files=[out.parent/'v111-profile-20261020/baseline.cpp',out.parent/'v111-profile-20261020/baseline',out.parent/'v115-collector-20261020/v115.cpp',out.parent/'v115-collector-20261020/v115',inputs/'dll/DiffPlex.Benchmark.dll',inputs/'dll/DiffPlex.Benchmark.pdb',host,app.parent/'DiffPlex.app.settings.json',app.parent/'DiffPlex_BackUpThisFolder_ButDontShipItWithYourGame/Managed/DiffPlex.Benchmark.dll',*(app/'Contents/MacOS').iterdir(),*app.rglob('GameAssembly.dylib')]
(out/'final-three-backend-provenance.json').write_text(json.dumps({'compiler_revision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'commands':commands,'files':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in files},'dotnet_tiered_compilation':0,'dotnet_ready_to_run':0,'unity_rebuilt':False,'unity_retimed':True},indent=2));print(json.dumps(summary,indent=2))
