from pathlib import Path
import json,re,collections,hashlib,subprocess,statistics
out=Path(__file__).resolve().parent
methods={m['token']:m for m in json.loads((out/'core-methods.json').read_text())['methods']}
app={m['token']:m for m in json.loads(Path('artifacts/roslyn-il-investigation-20261019/methods.json').read_text())['methods']}
def name(label):
 symbol=label.split('  (in ',1)[0].split(' (in ',1)[0].split('_objects_packed_closure::',1)[-1].split('(',1)[0]
 token=re.search(r'cilica_m_(\w{64})_(060[0-9a-f]{5})',symbol)
 if token:
  m=(methods if token[1].startswith('655cc451') else app).get(token[2]);return (m['type']+'.'+m['name']) if m else token[2]
 return symbol.split(' + ',1)[0]
allg=collections.Counter();groups=collections.Counter();callers=collections.defaultdict(collections.Counter)
for i in range(2):
 d=json.loads((out/f'profile-summary-{i}.json').read_text());groups.update(d['groups']);allg.update(d['all_groups'])
 parsed=[]
 for l in (out/f'sample-{i}.txt').read_text().split('Call graph:',1)[1].split('Total number in stack',1)[0].splitlines():
  m=re.match(r'^([\s+!:|]+)(\d+) (.*)$',l)
  if m:parsed.append((len(m[1]),int(m[2]),m[3]))
 stack=[]
 for depth,count,label in parsed:
  while stack and stack[-1][0]>=depth:stack.pop()
  key=name(label)
  if stack and ('_02000072_accepts' in key or key in ['frame_location_storage','BorrowScope::BorrowScope','active_root_frame','empty_array']):callers[key][name(stack[-1][2])]+=count
  stack.append((depth,count,label))
buckets={
 'borrow_location_registration':{k:v for k,v in groups.items() if 'location' in k or k in ['BorrowScope::BorrowScope','active_root_frame','find_borrow']},
 'descriptor_predicates':{k:v for k,v in groups.items() if k.endswith('_accepts')},
 'dictionary_methods':{k:v for k,v in groups.items() if 'System.Collections.Generic.Dictionary`2.' in k},
 'string_hash_kernel':{k:v for k,v in groups.items() if 'StringKeyOperations' in k},
 'empty_array_cache':{'empty_array':groups['empty_array']}}
a={'app_samples':sum(groups.values()),'all_samples':sum(allg.values()),'exclusive':dict(groups.most_common()),'all_exclusive':dict(allg.most_common()),'buckets':{k:{'samples':sum(v.values()),'app_percent':sum(v.values())*100/sum(groups.values()),'members':v} for k,v in buckets.items()},'inclusive_callers_not_additive':{k:dict(v.most_common()) for k,v in callers.items()}}
(out/'profile-aggregate.json').write_text(json.dumps(a,indent=2))
r=json.loads((out/'rea-core.json').read_text());selected=[]
for m in r['normalized_result']['methods']:
 if m['name'] in ['Hash','HashKey','CheckKey','Find','TryGetValue','Equal','GetHashCode','ToString']:
  selected.append({'token':m['token'],'type':m['declaring_type'],'name':m['name'],'signature':m['signature'],'body':m['body']})
(out/'rea-selected.json').write_text(json.dumps({'evidence_id':r['evidence_id'],'subject':r['subject'],'coverage':r['normalized_result']['coverage'],'methods':selected},indent=2))
counts=dict((k,int(v)) for k,v in re.findall(r'(\w+)=(\d+)',(out/'counts.txt').read_text().splitlines()[-1])); assert json.loads((out/'counted-output.json').read_text())['checksum']==6146*256
(out/'counts-summary.json').write_text(json.dumps({'scope':'256 diffs after 8 warmup diffs; not timed performance evidence','iterations':256,'totals':counts,'per_diff':{k:v/256 for k,v in counts.items()},'note':'frames are reused arena reservations, not live heap allocation; steps counter does not count native direct-flow blocks'},indent=2))
refs=json.loads(Path('docs/performance/remaining-gap-20261020/reference-sources.json').read_text());assert all(hashlib.sha256(Path(f).read_bytes()).hexdigest()==h for f,h in refs.items());(out/'reference-sources.json').write_text(json.dumps(refs,indent=2))
identity={'revision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'compiler_tree_clean':not subprocess.check_output(['git','diff','HEAD','--','src/Cilica.Compiler'],text=True),'fresh_matches_accepted_v97':(out/'baseline.cpp').read_bytes()==Path('artifacts/typed-internal-20261020/final.cpp').read_bytes(),'rea_version':'6.7.0','files':{}}
paths=[out/'baseline.cpp',out/'baseline',out/'profile',out/'baseline-core/Cilica.Core.dll',*out.glob('sample-*.txt'),Path('artifacts/roslyn-il-investigation-20261019/DiffPlex.Benchmark.il'),Path('docs/performance/runtime-costs-20261020/dotnet-arm64.txt')]
inputs=Path('/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs');paths.extend([inputs/'dll/DiffPlex.Benchmark.dll',inputs/'dll/DiffPlex.Benchmark.pdb',inputs/'host/DotNetHost.dll'])
for p in paths:identity['files'][str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()
(out/'identity.json').write_text(json.dumps(identity,indent=2))
(out/'raw-timings.json').write_text(json.dumps({p.name:[json.loads(x) for x in p.read_text().splitlines()] for p in sorted(out.glob('*.jsonl')) if p.name.startswith(('timing-','experiment-'))},indent=2))
print('buckets',[(k,v['samples'],v['app_percent']) for k,v in a['buckets'].items()]);print('callers',a['inclusive_callers_not_additive'])
