"""Owned exact-scope offline validation; no network, external targets or host execution."""
import hashlib,json,pathlib,shutil,subprocess,time,traceback
out=pathlib.Path('/evidence');started=time.monotonic();steps=[]
def run(name,args,cwd='/work/cilica'):
 step={'name':name,'argv':args,'cwd':cwd};steps.append(step)
 (out/'progress.json').write_text(json.dumps(steps,indent=2))
 with (out/(name+'.log')).open('w') as log:r=subprocess.run(args,cwd=cwd,stdout=log,stderr=subprocess.STDOUT,timeout=max(1,1710-(time.monotonic()-started)))
 step['exit_code']=r.returncode
 if r.returncode:raise RuntimeError(name+' failed: '+str(r.returncode))
try:
 shutil.copytree('/approved/cilica','/work/cilica');shutil.copytree('/approved/input','/work/input')
 for p in ('home','recipe','out'):pathlib.Path('/work/'+p).mkdir()
 pathlib.Path('/work/home/.nuget/NuGet').mkdir(parents=True)
 pathlib.Path('/work/home/.nuget/NuGet/NuGet.Config').write_text('<configuration><packageSources><clear/><add key="offline" value="/approved/offline-feed"/></packageSources></configuration>')
 shutil.copy('/tooling-packages.txt',out/'tooling-packages.txt');shutil.copy('/approved/input-provenance.json',out/'input-provenance.json')
 run('versions',['sh','-c','dotnet --info; clang++-18 --version; python3 --version; id; cat /proc/self/status; cat /proc/mounts'])
 for name in ('SelectedDecode.csproj','Probe.cs'):shutil.copy('/work/cilica/tools/CompatibilityCandidates/albireo-validation/'+name,'/work/recipe/'+name)
 source='-p:CandidateSource=/work/input'
 run('candidate-restore',['dotnet','restore','/work/recipe/SelectedDecode.csproj','--source','/approved/offline-feed',source,'-p:NuGetAudit=false'])
 run('candidate-build',['dotnet','build','/work/recipe/SelectedDecode.csproj','-c','Release','--no-restore','--output','/work/out',source])
 dll=pathlib.Path('/work/out/Albireo.SelectedDecode.dll');shutil.copyfile(dll,out/dll.name)
 (out/'candidate-assembly.sha256').write_text(hashlib.sha256(dll.read_bytes()).hexdigest()+'  '+dll.name+'\n')
 shutil.copyfile('/work/input/LICENSE.txt',out/'LICENSE.Albireo.txt')
 run('cilica-build',['dotnet','build','tests/Cilica.Metadata.Tests/Cilica.Metadata.Tests.csproj','--artifacts-path','artifacts/albireo-approved/build','-p:CustomBeforeDirectoryBuildProps=/work/cilica/tools/CompatibilityCandidates/isolated-build.props','-p:RestoreSources=/approved/offline-feed','-p:NuGetAudit=false','-p:UseSharedCompilation=false','-nodeReuse:false','-m:1'])
 run('differential',['dotnet','artifacts/albireo-approved/build/bin/_work_cilica_tests_Cilica.Metadata.Tests/debug/Cilica.Metadata.Tests.dll','-explicit','only','-method','Cilica.Metadata.Tests.ApprovedAlbireoExecutionTests.Selected_roots_match_every_transcript_observation','-culture','invariant','-parallel','none','-showLiveOutput','-noColor'])
 result={'status':'passed','elapsed_seconds':time.monotonic()-started,'steps':steps}
except Exception as ex:
 (out/'runner-error.txt').write_text(traceback.format_exc());result={'status':'failed','error':str(ex),'elapsed_seconds':time.monotonic()-started,'steps':steps}
(out/'result.json').write_text(json.dumps(result,indent=2)+'\n')
# Keep tmpfs evidence alive until the bounded host supervisor exports it and removes this task container.
while True:time.sleep(10)
