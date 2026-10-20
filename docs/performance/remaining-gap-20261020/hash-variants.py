from pathlib import Path
import subprocess,json,os,re
out=Path(__file__).resolve().parent;source=out/'source';path=source/'library/Cilica.Core/StringKeyOperations.cs'
original=path.read_text();start=original.index('        internal static int Hash(');opening=original.index('{',start);depth=1;end=opening+1
while depth:
 if original[end]=='{':depth+=1
 if original[end]=='}':depth-=1
 end+=1
body=original[opening+1:end-1]
snapshot='\n            string snapshot = value;'+re.sub(r'\bvalue\b','snapshot',body)
flat='''
            string text = value;
            if (text == null) return 0;
            uint first = (5381u << 16) + 5381u, second = first;
            int remaining = text.Length, index = 0;
            while (remaining > 2)
            {
                remaining -= 4;
                uint pair = (uint)text[index] | ((index + 1 < text.Length ? (uint)text[index + 1] : 0u) << 16);
                first = (((first << 5) | (first >> 27)) + first) ^ pair;
                pair = (uint)text[index + 2] | ((index + 3 < text.Length ? (uint)text[index + 3] : 0u) << 16);
                second = (((second << 5) | (second >> 27)) + second) ^ pair;
                index += 4;
            }
            if (remaining > 0)
            {
                uint pair = (uint)text[index] | ((index + 1 < text.Length ? (uint)text[index + 1] : 0u) << 16);
                second = (((second << 5) | (second >> 27)) + second) ^ pair;
            }
            return unchecked((int)(first + second * 1566083941u));
        '''
try:
 for name,content in [('hash_snapshot',snapshot),('hash_flat',flat)]:
  modified=original[:opening+1]+content+original[end-1:];path.write_text(modified);(out/(name+'.cs')).write_text(modified)
  env=dict(os.environ,CILICA_APPROVED_DIFFPLEX_PIN='f500e73f28e28813f942f1f16dec5a34b40e48e3',CILICA_DIFFPLEX_ASSEMBLY='/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs/dll/DiffPlex.Benchmark.dll',CILICA_BENCH_TEMPLATE=str(source/'tools/CompatibilityCandidates/diffplex-benchmark/native-host.cpp.template'),CILICA_BENCH_CPP=str(out/(name+'.cpp')),CILICA_BENCH_CORE_CAPTURE=str(out/(name+'-core')))
  r=subprocess.run(['dotnet','tests/Cilica.Metadata.Tests/bin/Debug/net10.0/Cilica.Metadata.Tests.dll','-explicit','only','-method','Cilica.Metadata.Tests.ApprovedDiffPlexBenchmarkEmissionTests.Emit_approved_benchmark','-culture','invariant','-noColor'],cwd=source,env=env,capture_output=True,text=True,timeout=180)
  (out/(name+'-emission.log')).write_text(r.stdout+r.stderr);r.check_returncode();print('emitted',name,flush=True)
finally:path.write_text(original)
