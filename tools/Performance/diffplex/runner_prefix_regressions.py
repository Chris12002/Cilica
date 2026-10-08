"""Offline owned-runtime regression check in a disposable pinned source export."""
from pathlib import Path
import os,shutil,subprocess,json,time,traceback,hashlib,sys
sys.path.insert(0,'/approved')
from lazy_prefix import transform
out=Path('/evidence');steps=[];started=time.monotonic()
def run(name,args):
    steps.append(dict(name=name,argv=args))
    with (out/(name+'.log')).open('w') as log:
        p=subprocess.run(args,cwd='/work/cilica',stdout=log,stderr=subprocess.STDOUT,timeout=max(1,1650-(time.monotonic()-started)))
    steps[-1]['exit_code']=p.returncode
    assert p.returncode==0,name
try:
    shutil.copytree('/approved/cilica','/work/cilica')
    runtime=Path('/work/cilica/src/Cilica.Compiler/ObjectCppRuntime.cs');marking=runtime.with_name('ObjectCppMarking.cs')
    joined=runtime.read_text()+'\n/*PREFIX_SPLIT*/\n'+marking.read_text()
    changed=transform(joined).split('\n/*PREFIX_SPLIT*/\n');assert len(changed)==2
    runtime.write_text(changed[0]);marking.write_text(changed[1])
    (out/'transformed-template-hashes.json').write_text(json.dumps({p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [runtime,marking]}))
    run('build',['dotnet','build','tests/Cilica.Metadata.Tests/Cilica.Metadata.Tests.csproj','--artifacts-path','artifacts/prefix/build','-p:CustomBeforeDirectoryBuildProps=/work/cilica/tools/CompatibilityCandidates/isolated-build.props','-p:RestoreSources=/approved/offline-feed','-p:NuGetAudit=false','-p:UseSharedCompilation=false','-nodeReuse:false','-m:1'])
    args=['dotnet','artifacts/prefix/build/bin/_work_cilica_tests_Cilica.Metadata.Tests/debug/Cilica.Metadata.Tests.dll','-noColor','-culture','invariant','-parallel','none']
    for name in ['NativeTracingTests','NativeObjectExecutionTests','NativeCollectionTests','NativeFrameBudgetTests','NativeExecutionFrameTests','NativeExceptionRuntimeTests','RuntimeTypeIdentityTests']:
        run(name,args+['-class','Cilica.Metadata.Tests.'+name])
    result=dict(status='passed',elapsed_seconds=time.monotonic()-started,steps=steps)
except Exception as ex:
    (out/'runner-error.txt').write_text(traceback.format_exc());result=dict(status='failed',error=str(ex),steps=steps)
(out/'result.json').write_text(json.dumps(result,indent=2))
while True:time.sleep(10)
