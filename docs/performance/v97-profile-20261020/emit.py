from pathlib import Path
import subprocess,os,json,hashlib,statistics,sys
root=Path.cwd();out=Path(__file__).resolve().parent;name=sys.argv[1];baseline=sys.argv[2] if len(sys.argv)>2 else None
source=out/(name+'.cpp');core=out/(name+'-core')
env=dict(os.environ,CILICA_APPROVED_DIFFPLEX_PIN='f500e73f28e28813f942f1f16dec5a34b40e48e3',CILICA_DIFFPLEX_ASSEMBLY='/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs/dll/DiffPlex.Benchmark.dll',CILICA_BENCH_TEMPLATE=str(root/'tools/CompatibilityCandidates/diffplex-benchmark/native-host.cpp.template'),CILICA_BENCH_CPP=str(source),CILICA_BENCH_CORE_CAPTURE=str(core))
r=subprocess.run(['dotnet','tests/Cilica.Metadata.Tests/bin/Debug/net10.0/Cilica.Metadata.Tests.dll','-explicit','only','-method','Cilica.Metadata.Tests.ApprovedDiffPlexBenchmarkEmissionTests.Emit_approved_benchmark','-culture','invariant','-noColor'],env=env,text=True,capture_output=True,timeout=180);(out/(name+'-emission.log')).write_text(r.stdout+r.stderr);r.check_returncode()
if name == 'final':
 assert source.read_bytes() == (out/'borrows.cpp').read_bytes(), 'Committed compiler must reproduce measured v97 output'
 assert subprocess.check_output(['git','diff','HEAD','--','src/Cilica.Compiler'],text=True) == '', 'Compiler source changed during emission'
cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(source),'-o',str(out/name)]
r=subprocess.run(cmd,text=True,capture_output=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
raw=subprocess.check_output([str(out/name),'verify','0'],text=True,timeout=120);assert json.loads(raw)==json.loads(Path('artifacts/remaining-gap-20261020/baseline-verify.json').read_text());(out/(name+'-verify.json')).write_text(raw)
provenance={'revision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'command':cmd,'compiler_files':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/'src/Cilica.Compiler').glob('ObjectCpp*.cs')},'cpp_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'core_sha256':hashlib.sha256((core/'Cilica.Core.dll').read_bytes()).hexdigest(),'max_native_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines())}
(out/(name+'-provenance.json')).write_text(json.dumps(provenance,indent=2))
