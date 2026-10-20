from pathlib import Path
import json,re,hashlib,subprocess
out=Path(__file__).resolve().parent
# Reuse the checked sampling parser and non-additive character-subtree computation.
s=Path('artifacts/v99-profile-20261020/summarize.py').read_text();exec(s[:s.index("r=json.loads((out/'rea-core.json')")])
r=json.loads((out/'rea-core.json').read_text());selected=[]
for m in r['normalized_result']['methods']:
 if m['name'] in ['ToString','Find','TryGetValue','.ctor','Count','Get']:
  selected.append({k:m[k] for k in ['token','declaring_type','name','signature','body']})
(out/'rea-selected.json').write_text(json.dumps({'evidence_id':r['evidence_id'],'subject':r['subject'],'coverage':r['normalized_result']['coverage'],'methods':selected},indent=2))
c=dict((k,int(v)) for k,v in re.findall(r'(\w+)=(\d+)', '\n'.join((out/'counts.txt').read_text().splitlines()[-2:])))
assert json.loads((out/'counted-output.json').read_text())['checksum']==6146*256
(out/'counts-summary.json').write_text(json.dumps({'iterations':256,'warmup_diffs':8,'totals':c,'per_diff':{k:v/256 for k,v in c.items()},'notes':['Untimed instrumentation.','Kernel/typed/packet method counters are separate and must not be summed as managed calls.','Frame bytes are cumulative reused arena reservations.','Heap-byte peaks exclude native allocator metadata and execution storage.']},indent=2))
gc={}
for n in ['baseline','heap4x']:
 line=next(l for l in (out/(n+'-gc.txt')).read_text().splitlines() if l.startswith('GC'))
 gc[n]={k:int(v) for k,v in re.findall(r'(\w+)=(\d+)',line)}
(out/'gc-counts.json').write_text(json.dumps(gc,indent=2))
refs=json.loads(Path('artifacts/v99-profile-20261020/reference-sources.json').read_text())
assert all(hashlib.sha256(Path(f).read_bytes()).hexdigest()==h for f,h in refs.items())
for f in ['library/Cilica.Core/List.cs','src/Cilica.Compiler/ObjectCppTypedMethods.cs','src/Cilica.Compiler/ObjectCppCharacterMethods.cs','src/Cilica.Compiler/ObjectCppRuntime.cs','src/Cilica.Compiler/ObjectCppMarking.cs']:
 refs[f]=hashlib.sha256(Path(f).read_bytes()).hexdigest()
(out/'reference-sources.json').write_text(json.dumps(refs,indent=2))
identity={'revision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'compiler_tree_clean':not subprocess.check_output(['git','diff','HEAD','--','src/Cilica.Compiler'],text=True),'fresh_matches_accepted_v101':(out/'baseline.cpp').read_bytes()==Path('artifacts/character-performance-20261020/wrappers-committed.cpp').read_bytes(),'compiler_files':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in Path('src/Cilica.Compiler').glob('ObjectCpp*.cs')},'files':{}}
inputs=Path('/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs')
paths=[out/'baseline.cpp',out/'baseline',out/'profile',out/'baseline-core/Cilica.Core.dll',out/'baseline-core/build-options.json',*out.glob('sample-*.txt'),out/'rea-core.json',Path('artifacts/roslyn-il-investigation-20261019/DiffPlex.Benchmark.il'),Path('docs/performance/runtime-costs-20261020/dotnet-arm64.txt'),inputs/'dll/DiffPlex.Benchmark.dll',inputs/'dll/DiffPlex.Benchmark.pdb',inputs/'host/DotNetHost.dll']
for p in paths:identity['files'][str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()
(out/'identity.json').write_text(json.dumps(identity,indent=2))
(out/'raw-timings.json').write_text(json.dumps({p.name:[json.loads(x) for x in p.read_text().splitlines()] for p in sorted(out.glob('*.jsonl')) if p.name.startswith(('timing-','experiment-','heap-'))},indent=2))
lines=(out/'native-arm64.txt').read_text().splitlines();chosen=[];sizes={}
for i,line in enumerate(lines):
 if line.endswith(':') and any(x in line for x in ['valid_array_storage','empty_array','frame_location_storage','location_storage']):
  end=i+1
  while end<len(lines) and not lines[end].endswith(':'):end+=1
  body=lines[i:end];chosen.extend(body);sizes[line]=4*sum(bool(re.match(r'^[0-9a-f]{16}\s',l)) for l in body)
(out/'native-selected.txt').write_text('\n'.join(chosen)+'\n');(out/'native-summary.json').write_text(json.dumps(sizes,indent=2))
print('character subtree',a['character_subtree_nonadditive']);print('per diff',{k:v/256 for k,v in c.items()});print('compiler clean',identity['compiler_tree_clean']);print('GC',gc)
