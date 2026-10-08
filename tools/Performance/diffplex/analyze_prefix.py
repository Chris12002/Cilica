"""Informational native comparison. Correct outputs gate; timing ratios never do."""
import argparse,json,statistics
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('evidence',type=Path);a=p.parse_args()
data=json.loads((a.evidence/'clean-results.json').read_text());summary={}
for runtime in ['baseline','prefix']:
    rows=[r for r in data['runs'] if r['runtime']==runtime and r['mode']=='measure'];assert len(rows)==3
    medians=[]
    for row in rows:
        samples=[s for s in row['records'] if 'sample' in s];assert len(samples)==7
        for s in samples:assert s['iterations']==16 and s['checksum']==6146*16
        medians.append(statistics.median(s['ns']/s['iterations'] for s in samples))
    summary[runtime]={**data['summary'][runtime],'process_median_ns':medians}
ratio=summary['baseline']['median_ns']/summary['prefix']['median_ns']
pairs=[x/y for x,y in zip(summary['baseline']['process_median_ns'],summary['prefix']['process_median_ns'])]
memory=[s for r in data['runs'] if r['runtime']=='prefix' and r['mode']=='measure' for s in r['records'] if 'scratch_bytes' in s]
result=dict(summary=summary,median_speedup=ratio,paired_process_speedups=pairs,
            memory={k:sorted({s[k] for s in memory}) for k in memory[0]},dotnet='not measured in this revision',timing_gate=False)
host=a.evidence/'host-load.jsonl'
if host.exists():
    load=[json.loads(line) for line in host.read_text().splitlines()]
    result['host_load']={'samples':sum('processes' in r for r in load),
        'compiler_process_cpu_peak_percent':max((p['cpu_percent'] for r in load for p in r.get('processes',[]) if any(x in p['name'].lower() for x in ['dotnet','clang'])),default=0),
        'measurement_clock_bracketed':all(load[0]['unix_ns']<=r['started_unix_ns'] and r['started_unix_ns']+r['wall_ns']<=load[-1]['unix_ns'] for r in data['runs'])}
print(json.dumps(result,indent=2))
