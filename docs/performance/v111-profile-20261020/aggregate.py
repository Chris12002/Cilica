from pathlib import Path
import json,re,collections,hashlib,subprocess,statistics
out=Path(__file__).resolve().parent
methods={m['token']:m for m in json.loads((out/'core-methods.json').read_text())['methods']}
app={m['token']:m for m in json.loads(Path('/Users/christian/Cilica/artifacts/roslyn-il-investigation-20261019/methods.json').read_text())['methods']}
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
  if stack and ('_02000072_accepts' in key or key in ['valid_array_storage','frame_location_storage','location_storage','find_borrow','BorrowScope::BorrowScope','active_root_frame','empty_array','allocate_string','System.Char.ToString']):callers[key][name(stack[-1][2])]+=count
  stack.append((depth,count,label))
buckets={
 'borrow_location_registration':{k:v for k,v in groups.items() if ('location' in k and not k.startswith('allocation')) or k in ['BorrowScope::BorrowScope','active_root_frame','find_borrow']},
 'descriptor_predicates':{k:v for k,v in groups.items() if k.endswith('_accepts')},
 'dictionary_methods':{k:v for k,v in groups.items() if 'System.Collections.Generic.Dictionary`2.' in k},
 'string_hash_kernel':{k:v for k,v in groups.items() if 'StringKeyOperations' in k},
 'empty_array_cache':{'empty_array':groups['empty_array']}}
a={'app_samples':sum(groups.values()),'all_samples':sum(allg.values()),'exclusive':dict(groups.most_common()),'all_exclusive':dict(allg.most_common()),'buckets':{k:{'samples':sum(v.values()),'app_percent':sum(v.values())*100/sum(groups.values()),'members':v} for k,v in buckets.items()},'inclusive_callers_not_additive':{k:dict(v.most_common()) for k,v in callers.items()}}
(out/'profile-aggregate.json').write_text(json.dumps(a,indent=2))
