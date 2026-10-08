"""Classify sampled instruction PCs and summarize separate instrumented scopes."""
from pathlib import Path
import argparse,collections,json,re,gzip

p=argparse.ArgumentParser();p.add_argument('evidence',type=Path);a=p.parse_args();b=a.evidence
def read(name):
    path=b/name
    return path.read_text() if path.exists() else gzip.decompress((b/(name+'.gz')).read_bytes()).decode()
groups={};current=None
for line in read('lines.log').splitlines():
    if re.fullmatch(r'0x[0-9a-f]+',line):current=int(line,16);groups[current]=[]
    else:groups[current].append(line)
data=read('run-sample.log').splitlines();counts=collections.Counter();functions=collections.Counter();external=collections.Counter()
for line in data[1:]:
    pc,lib,func=line.split(' ',2);frames=groups[int(pc,16)][0::2];s='\n'.join(frames)
    if 'valid_roots' in s:category='root_validation'
    elif any(n in s for n in ['mark_graph','mark_edge','collect(','visit_','rebuild_index']):category='collection_tracing'
    elif any(n in s for n in ['allocate<','allocate_array','allocate_string']):category='allocation_other'
    elif '_steps::' in s:category='generated_steps'
    elif '::cilica_m_' in s:category='generated_method_driver'
    elif 'Storage<' in s:category='frame_storage'
    elif 'Instance::owns' in s:category='ownership_other'
    elif any(n in s for n in ['location_','LocationStorage','array_location','heap_location','active_root_frame','find_borrow','BorrowScope','retire_borrows','issue_borrow_token']):category='managed_locations_and_borrows'
    elif lib!='/work/sample':category='external_library';external[Path(lib).name+' '+func]+=1
    elif 'accepts' in s or 'known_type' in s:category='type_checks'
    else:category='other_native'
    counts[category]+=1;functions[frames[0]]+=1
rows=[json.loads(x)for x in (b/'run-scopes.log').read_text().splitlines()]
total=sum(r['ns']for r in rows);iterations=sum(r['iterations']for r in rows)
result={'sample_metadata':json.loads(data[0]),'sample_counts':dict(counts),
    'sample_shares':{k:v/(len(data)-1)for k,v in counts.items()},
    'top_leaf_functions':functions.most_common(30),'external_leaf_symbols':external.most_common(),
    'instrumented_scope_shares':{k:sum(r[k]for r in rows)/total for k in ['root_ns','gc_ns','allocation_inclusive_ns']},
    'instrumented_counts_per_diff':{k:sum(r[k]for r in rows)/iterations for k in ['root_calls','root_slots','nonnull_slots','frames','frame_bytes','steps']},
    'boundary_baseline':[json.loads(x)for x in (b/'run-boundary-baseline.log').read_text().splitlines()],
    'boundary_index':[json.loads(x)for x in (b/'run-boundary-index.log').read_text().splitlines()]}
print(json.dumps(result,indent=2))
