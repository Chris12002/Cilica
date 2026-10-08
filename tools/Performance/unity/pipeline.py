"""Resume the approved local Unity pipeline. Builds/verifies only; timings are opt-in."""
import argparse,datetime,hashlib,json,os,pathlib,plistlib,shutil,subprocess,sys
p=argparse.ArgumentParser();p.add_argument('--work',type=pathlib.Path,required=True);p.add_argument('--unity-cli',type=pathlib.Path,required=True);p.add_argument('--dotnet',type=pathlib.Path,required=True);p.add_argument('--stage',choices=['build','verify','capture','all'],default='all');a=p.parse_args()
w=a.work.resolve();here=pathlib.Path(__file__).resolve().parent;e=w/'evidence';project=w/'CilicaDiffPlex';dll=w/'inputs/dll/DiffPlex.Benchmark.dll'
if not (e/'input-provenance.json').is_file() or not dll.is_file():raise SystemExit('Run prepare.py and the two documented offline .NET builds first')
# Every run gets fresh logs/results. Earlier successes and failed attempts are retained.
run=e/('run-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));run.mkdir()
os.environ['UNITY_NO_CRASH_REPORT']='1';os.environ['UNITY_NO_UPDATE_CHECK']='1'
cli=str(a.unity_cli.resolve());dotnet=str(a.dotnet.resolve());app=project/'Build/DiffPlex.app'
source_hashes={str(f.relative_to(here)):hashlib.sha256(f.read_bytes()).hexdigest() for f in sorted((here/'template').rglob('*.cs'))}
receipt={'authored_source_sha256':source_hashes,'unity_version':'6000.3.2f1','candidate_dll_sha256':hashlib.sha256(dll.read_bytes()).hexdigest(),'steps':[],'timing_claim':False}
def save(): (run/'pipeline.json').write_text(json.dumps(receipt,indent=2)+'\n')
def call(name,cmd,seconds=1790):
 save();r=subprocess.run([sys.executable,str(here/'run_bounded.py'),'--log',str(run/(name+'.log')),'--seconds',str(seconds),'--']+cmd,cwd=w)
 receipt['steps'].append({'name':name,'exit_code':r.returncode});save()
 if r.returncode:raise SystemExit(r.returncode)
if a.stage in ['build','all']:
 plugins=w/'template/ProjectData~/Assets/Plugins';plugins.mkdir(parents=True,exist_ok=True);shutil.copy2(dll,plugins/dll.name)
 if not (project/'ProjectSettings/ProjectVersion.txt').is_file():
  if project.exists():shutil.move(str(project),str(run/'incomplete-project'))
  call('create',[cli,'projects','create','CilicaDiffPlex','--path',str(w),'--editor-version','6000.3.2f1','--architecture','arm64','--template',str(w/'template'),'--no-cloud','--non-interactive','--format','json'])
 plugins=project/'Assets/Plugins';plugins.mkdir(parents=True,exist_ok=True);shutil.copy2(dll,plugins/dll.name)
 # Refresh authored code only. Unity owns its scene/settings and generated .meta files.
 for f in (here/'template/ProjectData~/Assets').rglob('*.cs'):
  target=project/'Assets'/f.relative_to(here/'template/ProjectData~/Assets');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,target)
 call('unity-build',[cli,'run',str(project),'--editor-version','6000.3.2f1','--timeout','1780','--','-nographics','-logFile','-','-job-worker-count','2','-executeMethod','BenchmarkBuild.Build','--cilica-target','mac','--cilica-output',str(project/'Build/DiffPlex.app')])
if a.stage in ['build','all']:
 built={'candidate_dll_sha256':receipt['candidate_dll_sha256'],'authored_source_sha256':source_hashes,'native_sha256':{}}
 for f in sorted(app.rglob('*')):
  if f.is_file() and (f.suffix=='.dylib' or f.parent==app/'Contents/MacOS'):
   built['native_sha256'][str(f.relative_to(app))]=hashlib.sha256(f.read_bytes()).hexdigest()
 if not built['native_sha256']:raise SystemExit('Build returned success without native output')
 (project/'Build/build-provenance.json').write_text(json.dumps(built,indent=2)+'\n')
if a.stage in ['capture','verify','all']:
 built=json.loads((project/'Build/build-provenance.json').read_text())
 if built['candidate_dll_sha256']!=receipt['candidate_dll_sha256'] or built['authored_source_sha256']!=source_hashes:
  raise SystemExit('Inputs changed since the last successful build; rebuild before capture/verification')
 for relative,digest in built['native_sha256'].items():
  if hashlib.sha256((app/relative).read_bytes()).hexdigest()!=digest:raise SystemExit('Native output changed: '+relative)
 receipt['native_sha256']=built['native_sha256'];save()
if a.stage in ['capture','all']:
 call('capture',[sys.executable,str(here/'capture.py'),'--project',str(project),'--output',str(run/'generated')],120)
if a.stage in ['verify','all']:
 call('oracle',[dotnet,str(w/'inputs/host/DotNetHost.dll'),'verify','0'],60)
 # Oracle log contains only the single JSON result, not the supervisor receipt.
 call('check-oracle',[sys.executable,str(here/'check_result.py'),str(e/'expected.json'),str(run/'oracle.log')],10)
 with (app/'Contents/Info.plist').open('rb') as f:executable=plistlib.load(f)['CFBundleExecutable']
 if pathlib.Path(executable).name!=executable:raise SystemExit('Invalid bundle executable')
 binary=app/'Contents/MacOS'/executable
 if not binary.is_file():raise SystemExit('Built player missing: '+str(binary))
 actual=run/'unity-result.json'
 call('unity-verify',[str(binary),'-batchmode','-nographics','-logFile',str(run/'player.log'),'--cilica-mode','verify','--cilica-result',str(actual)],180)
 call('check-unity',[sys.executable,str(here/'check_result.py'),str(e/'expected.json'),str(actual)],10)
 receipt['player_sha256']=hashlib.sha256(binary.read_bytes()).hexdigest()
receipt['completed']=True;save();print('Evidence:',run)
