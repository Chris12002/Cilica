from pathlib import Path
import json,re,collections,hashlib,subprocess
out=Path(__file__).resolve().parent
exec((out/'aggregate.py').read_text())
# Attribute each exclusive sample once to the outer Char.ToString subtree.
char_subtree=0;char_external=0
for i in range(2):
 parsed=[]
 for line in (out/f'sample-{i}.txt').read_text().split('Call graph:',1)[1].split('Total number in stack',1)[0].splitlines():
  m=re.match(r'^([\s+!:|]+)(\d+) (.*)$',line)
  if m:parsed.append((len(m[1]),int(m[2]),m[3]))
 stack=[]
 for j,(depth,count,label) in enumerate(parsed):
  while stack and stack[-1][0]>=depth:stack.pop()
  is_char=bool(re.search(r'cilica_m_655\w+_060002a[23]',label));under_char=is_char or any(x[1] for x in stack)
  child=0;k=j+1
  while k<len(parsed) and parsed[k][0]>depth:
   child+=parsed[k][1];d=parsed[k][0];k+=1
   while k<len(parsed) and parsed[k][0]>d:k+=1
  exclusive=max(0,count-child)
  if under_char:
   char_subtree+=exclusive
   if '(in profile)' not in label:char_external+=exclusive
  stack.append((depth,is_char))
p=out/'profile-aggregate.json';a=json.loads(p.read_text());a['character_subtree_nonadditive']={'all_samples':char_subtree,'external_samples':char_external,'percent_all':100*char_subtree/a['all_samples']};p.write_text(json.dumps(a,indent=2))
r=json.loads((out/'rea-core.json').read_text())
selected=[{'token':m['token'],'type':m['declaring_type'],'name':m['name'],'signature':m['signature'],'body':m['body']} for m in r['normalized_result']['methods'] if m['name'] in ['ToString','Find','TryGetValue','.ctor','Count','Get']]
(out/'rea-selected.json').write_text(json.dumps({'evidence_id':r['evidence_id'],'subject':r['subject'],'coverage':r['normalized_result']['coverage'],'methods':selected},indent=2))
c=dict((k,int(v)) for k,v in re.findall(r'(\w+)=(\d+)', '\n'.join((out/'counts.txt').read_text().splitlines()[-2:])))
assert json.loads((out/'counted-output.json').read_text())['checksum']==6146*256
(out/'counts-summary.json').write_text(json.dumps({'iterations':256,'warmup_diffs':8,'totals':c,'per_diff':{k:v/256 for k,v in c.items()},'notes':['Instrumentation excluded from timing.','type_calls/type_comparisons count only small linear sets; hash_type_calls/hash_type_probes count broad hash sets.','frames are cumulative reused arena reservations, not simultaneous live memory.','steps counter omits direct-flow blocks.']},indent=2))
refs=json.loads(Path('artifacts/v97-profile-20261020/reference-sources.json').read_text());assert all(hashlib.sha256(Path(f).read_bytes()).hexdigest()==h for f,h in refs.items())
for f in ['library/upstream/dotnet-runtime/src/libraries/System.Private.CoreLib/src/System/Char.cs','library/upstream/dotnet-runtime/src/libraries/System.Private.CoreLib/src/System/String.cs','library/upstream/dotnet-runtime/src/libraries/System.Private.CoreLib/src/System/Array.cs','/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app/Contents/Resources/Scripting/il2cpp/libil2cpp/vm/String.cpp','/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app/Contents/Resources/Scripting/il2cpp/libil2cpp/il2cpp-object-internals.h','library/Cilica.Core/CoreTypes.cs','/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/evidence/run-20261008T141054068341Z/generated/Library/Bee/artifacts/MacStandalonePlayerBuildProgram/il2cppOutput/cpp/Generics__11.cpp']:
 refs[f]=hashlib.sha256(Path(f).read_bytes()).hexdigest()
(out/'reference-sources.json').write_text(json.dumps(refs,indent=2))
identity={'revision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'compiler_tree_clean':not subprocess.check_output(['git','diff','HEAD','--','src/Cilica.Compiler'],text=True),'fresh_matches_accepted_v99':(out/'baseline.cpp').read_bytes()==Path('artifacts/descriptor-typed-20261020/committed.cpp').read_bytes(),'rea_version':'6.7.0','files':{}}
paths=[out/'baseline.cpp',out/'baseline',out/'profile',out/'baseline-core/Cilica.Core.dll',*out.glob('sample-*.txt'),out/'rea-core.json',Path('artifacts/roslyn-il-investigation-20261019/DiffPlex.Benchmark.il'),Path('docs/performance/runtime-costs-20261020/dotnet-arm64.txt')]
inputs=Path('/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs');paths.extend([inputs/'dll/DiffPlex.Benchmark.dll',inputs/'dll/DiffPlex.Benchmark.pdb',inputs/'host/DotNetHost.dll'])
for p in paths:identity['files'][str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()
(out/'identity.json').write_text(json.dumps(identity,indent=2))
(out/'raw-timings.json').write_text(json.dumps({p.name:[json.loads(x) for x in p.read_text().splitlines()] for p in sorted(out.glob('*.jsonl')) if p.name.startswith(('timing-','experiment-'))},indent=2))
lines=(out/'native-arm64.txt').read_text().splitlines();selected=[];sizes={}
for i,line in enumerate(lines):
 if line.endswith(':') and any(x in line for x in ['valid_array_storage','_060002a2E','_060002a3E','_060002a2ER','_060002a3ER']):
  end=i+1
  while end<len(lines) and not lines[end].endswith(':'):end+=1
  body=lines[i:end];selected.extend(body);sizes[line]=4*sum(bool(re.match(r'^[0-9a-f]{16}\s',l)) for l in body)
(out/'native-selected.txt').write_text('\n'.join(selected)+'\n');(out/'native-summary.json').write_text(json.dumps(sizes,indent=2))
print('character subtree',a['character_subtree_nonadditive']);print('per diff',{k:v/256 for k,v in c.items()});print('compiler clean',identity['compiler_tree_clean'])
