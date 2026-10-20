"""Generated-C++ diagnostic experiments; production sources stay unchanged."""
from pathlib import Path
import re,subprocess,json,statistics,hashlib
out=Path(__file__).resolve().parent;base=(out/'baseline.cpp').read_text()
expected={0:json.loads((out/'baseline-verify.json').read_text()),1:json.loads((out/'dotnet-line-verify.json').read_text())}
def inline(s,names):
 for name in names:
  s,n=re.subn(r'\[\[([^\n]*?)gnu::noinline\]\] static ([^\n]+ '+name+r'\()',r'[[\1gnu::always_inline]] static \2',s)
  assert n==1,(name,n)
 return s
def gc_sweep(s):
 old='if (value->marked) { link = &value->next; continue; }';assert s.count(old)==1
 s=s.replace(old,'if (value->marked) { value->marked = false; value->mark_next = nullptr; link = &value->next; continue; }')
 old='''        value->type->destroy(value);
    }
    return report;''';assert s.count(old)==1
 return s.replace(old,'''        value->type->destroy(value);
    }
    state.prepared = false;
    return report;''')
def gc_fuse(s):
 old='''    for (Object* value = heap.head; value != nullptr; value = value->next) {
        value->marked = false; value->mark_next = nullptr;
    }
    auto result = visit_roots''';assert s.count(old)==1
 return gc_sweep(s.replace(old,'    auto result = visit_roots'))
def hash_gc(s):
 old='return static_cast<std::size_t>((address >> 3) ^ (address >> 17));';assert s.count(old)==1
 return s.replace(old,'const auto mixed = (address >> 3) * std::uintptr_t{0x9e3779b97f4a7c15ull}; return static_cast<std::size_t>(mixed ^ (mixed >> 32));')
def hash_descriptor(s):
 old='return ((address >> 3) ^ (address >> 17)) & (Capacity - 1);';assert s.count(old)==1
 return s.replace(old,'const auto mixed = (address >> 3) * std::uintptr_t{0x9e3779b97f4a7c15ull}; return (mixed ^ (mixed >> 32)) & (Capacity - 1);')
frame=['find_borrow','frame_location_storage','location_storage','frame_location','valid_location_argument','load_location_integer','store_location_integer','load_location_reference']
variants={'borrow_inline':inline(base,frame),'array_borrow_inline':inline(base,['array_location_storage','array_location','array_component_location_storage']),'string_inline':inline(base,['allocate_string','string_from_character']),'gc_sweep':gc_sweep(base),'gc_fuse':gc_fuse(base),'gc_hash':hash_gc(base),'descriptor_hash':hash_descriptor(base)}
provenance={}
for name,s in variants.items():
 cpp=out/(name+'.cpp');cpp.write_text(s);exe=out/name;cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(cpp),'-o',str(exe)]
 r=subprocess.run(cmd,text=True,capture_output=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 for work in [0,1]:
  raw=subprocess.check_output([str(exe),'verify',str(work)],text=True,timeout=60);assert json.loads(raw)==expected[work];(out/(name+f'-verify-{work}.json')).write_text(raw)
 provenance[name]={'command':cmd,'cpp_sha256':hashlib.sha256(cpp.read_bytes()).hexdigest(),'bytes':exe.stat().st_size,'max_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines())};print('verified',name,flush=True)
(out/'experiment-provenance.json').write_text(json.dumps(provenance,indent=2))
results={str(w):{n:[] for n in ['baseline',*variants]} for w in [0,1]};processes={w:{n:[] for n in names} for w,names in results.items()}
for repeat in range(3):
 for work in [0,1]:
  names=list(results[str(work)]);names=names[repeat:]+names[:repeat]
  if repeat%2:names.reverse()
  for name in names:
   raw=subprocess.check_output([str(out/name),'measure',str(work),'1024'],text=True,timeout=60);(out/f'experiment-{name}-work{work}-{repeat}.jsonl').write_text(raw);rows=[json.loads(l) for l in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==expected[work]['checksum']*r['iterations'] for r in rows[1:]);v=[r['ns']/r['iterations'] for r in rows[1:]];results[str(work)][name]+=v;processes[str(work)][name].append(statistics.median(v))
summary={w:{n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'process_medians':processes[w][n],'reduction_percent':100*(1-statistics.median(v)/statistics.median(values['baseline']))} for n,v in values.items()} for w,values in results.items()}
(out/'experiment-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
