"""Owned entrypoint: run the exact approved recipe offline, retain logs, await pickup."""
import hashlib,json,os,pathlib,shutil,subprocess,time,traceback
out=pathlib.Path('/evidence'); started=time.monotonic(); steps=[]
def save(value):
    (out/'result.json').write_text(json.dumps(value,indent=2)+'\n')
def run(name,args,cwd='/work/cilica'):
    step={'name':name,'argv':args,'cwd':cwd};steps.append(step)
    (out/'progress.json').write_text(json.dumps(steps,indent=2))
    with (out/(name+'.log')).open('w') as log:
        result=subprocess.run(args,cwd=cwd,stdout=log,stderr=subprocess.STDOUT,timeout=max(1,1710-(time.monotonic()-started)))
    step['exit_code']=result.returncode
    if result.returncode:raise RuntimeError(name+' failed: '+str(result.returncode))
try:
    shutil.copytree('/approved/cilica','/work/cilica')
    shutil.copytree('/approved/input','/work/input')
    pathlib.Path('/work/home').mkdir();pathlib.Path('/work/recipe').mkdir();pathlib.Path('/work/out').mkdir()
    shutil.copy('/tooling-packages.txt',out/'tooling-packages.txt')
    shutil.copy('/approved/input-provenance.json',out/'input-provenance.json')
    run('versions',['sh','-c','dotnet --info; clang++-18 --version; python3 --version; id; cat /proc/self/status; cat /proc/mounts'])
    provenance=json.loads(pathlib.Path('/approved/input-provenance.json').read_text())
    for item in provenance['candidate_files']:
        data=(pathlib.Path('/work/input')/item['path']).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==item['git_blob_sha1']
        assert hashlib.sha256(data).hexdigest()==item['sha256']
    (out/'verify.log').write_text('Both pinned candidate blobs verified before any build.\n')
    for name in ['SelectedRoots.csproj','Probe.cs']:
        shutil.copy('/work/cilica/tools/CompatibilityCandidates/circularbuffer-validation/'+name,'/work/recipe/'+name)
    source='-p:CandidateSource=/work/input'
    run('candidate-restore',['dotnet','restore','/work/recipe/SelectedRoots.csproj','--source','/approved/offline-feed',source,'-p:NuGetAudit=false'])
    run('candidate-build',['dotnet','build','/work/recipe/SelectedRoots.csproj','-c','Release','--no-restore','--output','/work/out',source])
    (out/'candidate-assembly.sha256').write_text(hashlib.sha256(pathlib.Path('/work/out/CircularBuffer.SelectedRoots.dll').read_bytes()).hexdigest()+'  CircularBuffer.SelectedRoots.dll\n')
    run('cilica-build',['dotnet','build','tests/Cilica.Metadata.Tests/Cilica.Metadata.Tests.csproj','--artifacts-path','artifacts/circularbuffer-approved/build','-p:CustomBeforeDirectoryBuildProps=/work/cilica/tools/CompatibilityCandidates/isolated-build.props','-p:RestoreSources=/approved/offline-feed','-p:NuGetAudit=false','-p:UseSharedCompilation=false','-nodeReuse:false','-m:1'])
    run('differential',['dotnet','artifacts/circularbuffer-approved/build/bin/_work_cilica_tests_Cilica.Metadata.Tests/debug/Cilica.Metadata.Tests.dll','-explicit','only','-method','Cilica.Metadata.Tests.ApprovedCircularBufferExecutionTests.Selected_roots_match_every_transcript_observation','-parallel','none','-culture','invariant','-showLiveOutput','-noColor'])
    save({'status':'passed','elapsed_seconds':time.monotonic()-started,'steps':steps})
except Exception as ex:
    (out/'runner-error.txt').write_text(traceback.format_exc())
    save({'status':'failed','error':str(ex),'elapsed_seconds':time.monotonic()-started,'steps':steps})
# tmpfs disappears on stop. Host watchdog copies bounded evidence, then removes this container.
while True:time.sleep(10)
