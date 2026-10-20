from pathlib import Path
import subprocess,os,json,hashlib,statistics,sys
root=Path.cwd();out=(Path.cwd()/'artifacts/dictionary-compact-20261020').resolve();name=sys.argv[1];baseline=sys.argv[2] if len(sys.argv)>2 else None
source=out/(name+'.cpp');core=out/(name+'-core')
before={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/'src/Cilica.Compiler').glob('*.cs')}
env=dict(os.environ,CILICA_APPROVED_DIFFPLEX_PIN='f500e73f28e28813f942f1f16dec5a34b40e48e3',CILICA_DIFFPLEX_ASSEMBLY='/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs/dll/DiffPlex.Benchmark.dll',CILICA_BENCH_TEMPLATE=str(root/'tools/CompatibilityCandidates/diffplex-benchmark/native-host.cpp.template'),CILICA_BENCH_CPP=str(source),CILICA_BENCH_CORE_CAPTURE=str(core))
r=subprocess.run(['dotnet','tests/Cilica.Metadata.Tests/bin/Debug/net10.0/Cilica.Metadata.Tests.dll','-explicit','only','-method','Cilica.Metadata.Tests.ApprovedDiffPlexBenchmarkEmissionTests.Emit_approved_benchmark','-culture','invariant','-noColor'],env=env,text=True,capture_output=True,timeout=180);(out/(name+'-emission.log')).write_text(r.stdout+r.stderr);r.check_returncode()
assert before == {str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/'src/Cilica.Compiler').glob('*.cs')}, 'Compiler changed during emission'
