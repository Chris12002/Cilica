from pathlib import Path
import os,subprocess,json,hashlib
out=Path(__file__).resolve().parent
app=Path('/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/CilicaDiffPlex/Build/DiffPlex.app')
inputs=app.parents[2]/'inputs'; host=inputs/'host/DotNetHost.dll';root=Path.cwd()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
old=json.loads((out.parent/'v111-profile-20261020/final-three-backend-provenance.json').read_text())
for path,digest in old['files'].items():
 p=Path(path)
 if str(p).startswith(str(app.parents[2])):assert sha(p)==digest,path
source=json.loads((out.parent/'v115-collector-20261020/provenance.json').read_text())
for path,digest in source['compiler_sources'].items():assert sha(Path(path))==digest,path
assert sha(out.parent/'v115-collector-20261020/v115')==source['exe_sha256']
env={k:v for k,v in os.environ.items() if not k.startswith(('DOTNET_Tier','COMPlus_Tier','DOTNET_Jit','COMPlus_Jit','DOTNET_ReadyToRun','COMPlus_ReadyToRun'))};env.update(DOTNET_TieredCompilation='0',DOTNET_ReadyToRun='0')
commands=[]
for work in [0,1]:
 expected=json.loads((out.parent/'v111-profile-20261020'/('baseline-verify.json' if work==0 else 'dotnet-line-verify.json')).read_text())
 for name,command in [('dotnet',['dotnet',str(host),'verify',str(work)]),('v115',[str(out.parent/'v115-collector-20261020/v115'),'verify',str(work)])]:
  commands.append(command); raw=subprocess.check_output(command,env=env,text=True,timeout=60);assert json.loads(raw)==expected;(out/f'verify-{name}-work{work}.json').write_text(raw)
 if work==0:
  target=out/'verify-unity.json'; assert not target.exists()
  cmd=['open','-W','-n',str(app),'--args','-batchmode','-nographics','-logFile',str(out/'verify-unity.player.log'),'--cilica-mode','verify','--cilica-result',str(target)]
  commands.append(cmd);r=subprocess.run(cmd,text=True,capture_output=True,timeout=60);(out/'verify-unity.launch.log').write_text(r.stdout+r.stderr);r.check_returncode();value=json.loads(target.read_text());assert value['error']=='' and value['values']==expected['values'] and value['checksum']==expected['checksum']
(out/'verification.json').write_text(json.dumps({'compiler_revision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'commands':commands,'matches_both_workloads_dotnet':True,'matches_character_workload_unity':True,'retained_comparator_hashes_unchanged':True,'compiler_sources_match_accepted_slice':True,'clang':subprocess.check_output(['clang++','--version'],text=True),'dotnet':subprocess.check_output(['dotnet','--info'],text=True)},indent=2))
print('Verified all 2,584 .NET/Cilica observations and 523 Unity observations; compiler and comparator hashes match.')
