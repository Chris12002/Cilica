from pathlib import Path
import json,re,subprocess,hashlib,statistics
out=Path(__file__).resolve().parent
exec((out/'experiments.py').read_text().split("variants={'borrow_inline':")[0])
def array_once(s):
 old='''    Location value{owner, 0, slot, declaring, LocationOrigin::array_slot, read_only, index};
    const auto storage = location_storage(context, value, false);
    return storage.status == LocationStatus::complete ? LocationResult{storage.status, value} : LocationResult{storage.status};'''
 new='''    if (integer || declaring->array_kind == 2) {
        if (slot != 0 || (!integer && declaring->accepts_element == nullptr)) return {LocationStatus::invalid};
    } else {
        const auto storage = array_component_location_storage(reinterpret_cast<ArrayObject*>(owner), *declaring, index, slot);
        if (storage.status != LocationStatus::complete) return {storage.status};
    }
    return {LocationStatus::complete, {owner, 0, slot, declaring, LocationOrigin::array_slot, read_only, index}};'''
 assert s.count(old)==1;return s.replace(old,new)
def allocation_inline(s):
 s=inline(s,['allocation_room'])
 for name in ['allocation_room','allocation_safepoint']:
  old='[[maybe_unused]] static bool '+name+'(';assert s.count(old)==1
  s=s.replace(old,'[[maybe_unused, gnu::always_inline]] static bool '+name+'(')
 return s
variants={'array_once':array_once(base),'allocation_inline':allocation_inline(base),'string_allocation_inline':allocation_inline(inline(base,['allocate_string','string_from_character'])),'combined':gc_fuse(array_once(allocation_inline(inline(base,frame+['allocate_string','string_from_character']))))}
provenance={}
for name,s in variants.items():
 cpp=out/(name+'.cpp');cpp.write_text(s);exe=out/name;cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(cpp),'-o',str(exe)]
 r=subprocess.run(cmd,text=True,capture_output=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 for work in [0,1]:
  raw=subprocess.check_output([str(exe),'verify',str(work)],text=True,timeout=60);assert json.loads(raw)==expected[work];(out/(name+f'-verify-{work}.json')).write_text(raw)
 provenance[name]={'command':cmd,'cpp_sha256':hashlib.sha256(cpp.read_bytes()).hexdigest(),'bytes':exe.stat().st_size,'max_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines())};print('verified',name,flush=True)
(out/'followup-provenance.json').write_text(json.dumps(provenance,indent=2))
results={str(w):{n:[] for n in ['baseline',*variants]} for w in [0,1]};processes={w:{n:[] for n in names} for w,names in results.items()}
for repeat in range(3):
 for work in [0,1]:
  names=list(results[str(work)]);names=names[repeat:]+names[:repeat]
  if repeat%2:names.reverse()
  for name in names:
   raw=subprocess.check_output([str(out/name),'measure',str(work),'1024'],text=True,timeout=60);(out/f'followup-{name}-work{work}-{repeat}.jsonl').write_text(raw);rows=[json.loads(l) for l in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==expected[work]['checksum']*r['iterations'] for r in rows[1:]);v=[r['ns']/r['iterations'] for r in rows[1:]];results[str(work)][name]+=v;processes[str(work)][name].append(statistics.median(v))
summary={w:{n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'process_medians':processes[w][n],'reduction_percent':100*(1-statistics.median(v)/statistics.median(values['baseline']))} for n,v in values.items()} for w,values in results.items()}
(out/'followup-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
