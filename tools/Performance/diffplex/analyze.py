"""Summarize retained evidence; no benchmark execution and no ratio gate."""
from pathlib import Path
import argparse,json,statistics
from measure import summarize

p=argparse.ArgumentParser();p.add_argument('evidence',type=Path);p.add_argument('--exclude-repeat',action='append',type=int,default=[]);a=p.parse_args();b=a.evidence
def rows(name):return [json.loads(line) for line in (b/name).read_text().splitlines()]
profile=rows('profile-continuous.log');warm=profile[1:]
total=sum(r['ns'] for r in warm)
result={'instrumented_warm':{
    'samples':len(warm),'total_ns':total,
    'root_validation_share':sum(r['root_ns'] for r in warm)/total,
    'collection_share':sum(r['gc_ns'] for r in warm)/total,
    'root_share_range':[min(r['root_ns']/r['ns'] for r in warm),max(r['root_ns']/r['ns'] for r in warm)],
    'calls_by_phase':[sum(r['calls'][i] for r in warm) for i in range(3)],
    'nodes_by_phase':[sum(r['nodes'][i] for r in warm) for i in range(3)],
    'root_scopes_per_op':sorted(set(r['root_scopes'] for r in warm)),
    'collections':sum(r['collections'] for r in warm)},
    'explicit_collection_survivors':rows('profile-collect-each.log')[1::2]}
result['lookup_reproducer']={}
for name in ['clean','index']:
    data=rows('micro-'+name+'.log')
    result['lookup_reproducer'][name]={n:statistics.median(r['ns']/r['queries'] for r in data if r['objects']==n) for n in sorted(set(r['objects'] for r in data))}
raw=json.loads((b/'clean-results.json').read_text())
accepted=[r for r in raw['runs'] if int(r['name'].rsplit('-',1)[1]) not in a.exclude_repeat]
clean=summarize(accepted);result['clean']=clean
result['excluded_repeats']=a.exclude_repeat
result['experimental_speedup']=clean['baseline']['median_ns']/clean['index']['median_ns']
print(json.dumps(result,indent=2))
