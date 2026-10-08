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
    run('stage',['python3','tools/CompatibilityCandidates/candidate_snapshot.py','stage','diffplex','--batch','batch-006','--input-json','/work/input/reviewed.json'])
    run('verify',['python3','tools/CompatibilityCandidates/candidate_snapshot.py','verify','diffplex','--batch','batch-006'])
    source='-p:CandidateSource=/work/cilica/artifacts/compatibility-candidates/diffplex-f500e73f28e28813f942f1f16dec5a34b40e48e3'
    bench='/work/cilica/tools/CompatibilityCandidates/diffplex-benchmark/'
    for name in ['SelectedRoots.csproj','BenchProbe.cs','DotNetHost.csproj','DotNetHost.cs']:
        shutil.copy(bench+name,'/work/recipe/'+name)
    run('candidate-restore',['dotnet','restore','/work/recipe/SelectedRoots.csproj','--source','/approved/offline-feed',source,'-p:NuGetAudit=false'])
    run('candidate-build',['dotnet','build','/work/recipe/SelectedRoots.csproj','-c','Release','--no-restore','--output','/work/out',source])
    (out/'candidate-assembly.sha256').write_text(hashlib.sha256(pathlib.Path('/work/out/DiffPlex.Benchmark.dll').read_bytes()).hexdigest()+'  DiffPlex.Benchmark.dll\n')
    run('dotnet-host-build',['dotnet','build','/work/recipe/DotNetHost.csproj','-c','Release','--output','/work/dotnet-host','-p:RestoreSources=/approved/offline-feed','-p:NuGetAudit=false'])
    run('cilica-build',['dotnet','build','tests/Cilica.Metadata.Tests/Cilica.Metadata.Tests.csproj','--artifacts-path','artifacts/diffplex-approved/build','-p:CustomBeforeDirectoryBuildProps=/work/cilica/tools/CompatibilityCandidates/isolated-build.props','-p:RestoreSources=/approved/offline-feed','-p:NuGetAudit=false','-p:UseSharedCompilation=false','-nodeReuse:false','-m:1'])
    os.environ['CILICA_DIFFPLEX_ASSEMBLY']='/work/out/DiffPlex.Benchmark.dll'
    os.environ['CILICA_BENCH_TEMPLATE']=bench+'native-host.cpp.template'
    os.environ['CILICA_BENCH_CPP']='/work/out/native-bench.cpp'
    run('emit',['dotnet','artifacts/diffplex-approved/build/bin/_work_cilica_tests_Cilica.Metadata.Tests/debug/Cilica.Metadata.Tests.dll','-explicit','only','-method','Cilica.Metadata.Tests.ApprovedDiffPlexBenchmarkEmissionTests.Emit_approved_benchmark','-culture','invariant','-noColor'])
    run('native-build',['clang++-18','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-Wall','-Wextra','-Werror','/work/out/native-bench.cpp','-o','/work/out/native-bench'])
    (out/'native-source.sha256').write_text(hashlib.sha256(pathlib.Path('/work/out/native-bench.cpp').read_bytes()).hexdigest()+'  native-bench.cpp\n')
    shutil.copy('/approved/expected-transcript.json',out/'expected-transcript.json')
    host=pathlib.Path('/work/recipe/DotNetHost.cs')
    host.write_text(host.read_text().replace('string mode = args[0]; int work = int.Parse(args[1]);', 'string mode = args[0]; if(mode == "noop") { Console.WriteLine("0"); return; } int work = int.Parse(args[1]);'))
    run('dotnet-noop-build',['dotnet','build','/work/recipe/DotNetHost.csproj','-c','Release','--no-restore','--output','/work/dotnet-host'])
    for variant in ['clean','profile','index']:
        run('transform-'+variant,['python3','/approved/profiling/transform.py','/work/out/native-bench.cpp','/work/out/'+variant+'.cpp',variant])
        run('compile-'+variant,['clang++-18','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-Wall','-Wextra','-Werror','/work/out/'+variant+'.cpp','-o','/work/out/'+variant])
        (out/(variant+'-sha256.json')).write_text(json.dumps({ext:hashlib.sha256(pathlib.Path('/work/out/'+variant+ext).read_bytes()).hexdigest() for ext in ['', '.cpp']}))
    shutil.copy('/work/out/native-bench.cpp',out/'native-bench.cpp')
    for variant in ['clean','index']:
        run('micro-'+variant,['/work/out/'+variant,'micro','0'])
    run('profile-continuous',['/work/out/profile','profile','0'])
    run('profile-collect-each',['/work/out/profile','profile','0','collect-each'])
    (out/'ready-for-clean.json').write_text(json.dumps({'elapsed':time.monotonic()-started}))
    while not pathlib.Path('/work/run-clean').exists():
        if time.monotonic()-started>1400:raise TimeoutError('No coordinated clean timing window')
        time.sleep(1)
    run('clean-suite',['python3','/approved/profiling/measure.py'])
    save({'status':'passed','elapsed_seconds':time.monotonic()-started,'steps':steps})
except Exception as ex:
    (out/'runner-error.txt').write_text(traceback.format_exc())
    save({'status':'failed','error':str(ex),'elapsed_seconds':time.monotonic()-started,'steps':steps})
# tmpfs disappears on stop. Host watchdog copies bounded evidence, then removes this container.
while True:time.sleep(10)
