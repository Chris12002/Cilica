from pathlib import Path
import subprocess,json,statistics,hashlib,re
out=Path(__file__).resolve().parent
base=(out/'baseline.cpp').read_text();expected=json.loads((out/'baseline-verify.json').read_text())
# DIAGNOSTIC ONLY: relax private malformed-carrier / expired-lifetime detection.
# These are cost ablations, NOT a valid implementation of checked borrowed calls.
def registration(s):
 start=s.index('        if (!active_root_frame(c, &roots)')
 end=s.index('        const auto token = issue_borrow_token();',start)
 return s[:start]+s[end:]
def resolved(s):
 # Keep normal location creation validation, cache its resolved frame slot.
 s=s.replace('    LocationNarrow narrow = LocationNarrow::none;\n};','    LocationNarrow narrow = LocationNarrow::none;\n    void* cached_address = nullptr;\n    StorageKind cached_kind = StorageKind::integer;\n    ReferencePredicate cached_accepts = nullptr;\n};',1)
 anchor='static LocationStorage frame_location_storage(Context& context, const Location& location) noexcept {\n'
 assert s.count(anchor)==1
 s=s.replace(anchor,anchor+'    if (location.cached_address != nullptr) return {LocationStatus::complete, location.cached_kind, location.cached_address, location.cached_accepts};\n',1)
 anchor='    return storage.status == LocationStatus::complete ? LocationResult{storage.status, value} : LocationResult{storage.status};'
 assert s.count(anchor)==2
 s=s.replace(anchor,'    if (storage.status == LocationStatus::complete) { value.cached_address = storage.address; value.cached_kind = storage.kind; value.cached_accepts = storage.accepts; }\n'+anchor,1)
 return s
variants={'registration_ablation':registration(base),'resolved_ablation':resolved(base),'borrow_ablation':resolved(registration(base))}
# Best earlier probe plus both ablations gives a bound on this combination, not production performance.
variants['combined_ablation']=resolved(registration((out/'all_inline_types_hash_flat.cpp').read_text()))
provenance={}
for name,s in variants.items():
 (out/(name+'.cpp')).write_text(s)
 cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti',str(out/(name+'.cpp')),'-o',str(out/name)]
 r=subprocess.run(cmd,capture_output=True,text=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 raw=subprocess.check_output([str(out/name),'verify','0'],text=True,timeout=120);assert json.loads(raw)==expected;(out/(name+'-verify.json')).write_text(raw)
 provenance[name]={'command':cmd,'source_sha256':hashlib.sha256(s.encode()).hexdigest(),'binary_sha256':hashlib.sha256((out/name).read_bytes()).hexdigest()}
 print('verified',name,flush=True)
(out/'borrow-ablation-provenance.json').write_text(json.dumps(provenance,indent=2))
names=['baseline',*variants];results={n:[] for n in names}
for repeat in range(3):
 order=names[repeat:]+names[:repeat]
 if repeat==1:order.reverse()
 for name in order:
  raw=subprocess.check_output([str(out/name),'measure','0','256'],text=True,timeout=60);(out/f'ablation-{name}-{repeat}.jsonl').write_text(raw)
  rows=[json.loads(x) for x in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:])
  results[name]+=[r['ns']/r['iterations'] for r in rows[1:]]
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v)} for n,v in results.items()}
for n in names:summary[n]['speedup_vs_base']=summary['baseline']['median_ns']/summary[n]['median_ns']
(out/'borrow-ablation-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
