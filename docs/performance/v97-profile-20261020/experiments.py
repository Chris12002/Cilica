from pathlib import Path
import subprocess,json,hashlib,statistics
out=Path(__file__).resolve().parent
base=(out/'baseline.cpp').read_text();expected=json.loads((out/'baseline-verify.json').read_text())
ns={};exec(Path('docs/performance/remaining-gap-20261020/descriptor-transform.py').read_text(),ns);types=ns['types']
def inline(s):return s.replace(', gnu::noinline','').replace('gnu::noinline, ','').replace('[[gnu::noinline]] ','')
def stack(s):
 for a,b in [('template<class T> struct Storage {\n','template<class T> struct Storage {\n    alignas(T) std::byte native_storage[sizeof(T)];\n'),('unpoison_storage(c.execution + mark + padding, sizeof(T));','unpoison_storage(native_storage, sizeof(T));'),('value = ::new (static_cast<void*>(c.execution + mark + padding)) T;','value = ::new (static_cast<void*>(native_storage)) T;')]:
  assert s.count(a)==1;s=s.replace(a,b,1)
 return s
# Cache-only diagnostic ablation; no GC or public-boundary check is changed.
a='if (++count > context.instance.count || !traceable_reference(context.instance, value)\n            || value->type->array_kind == 0 || entry->length != 0) { context.pending = 6;'
assert base.count(a)==1
variants={'types':types(base),'native_frames':stack(base),'types_native_frames':stack(types(base)),'all_inline':inline(base),'types_inline':inline(types(base)),'cache_check_ablation':base.replace(a,a.replace('traceable_reference','managed_reference'))}
provenance={'diagnostic_only':True,'revision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'variants':{}}
for name,s in variants.items():
 p=out/(name+'.cpp');p.write_text(s)
 cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(p),'-o',str(out/name)]
 r=subprocess.run(cmd,capture_output=True,text=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 raw=subprocess.check_output([str(out/name),'verify','0'],text=True,timeout=120);assert json.loads(raw)==expected;(out/(name+'-verify.json')).write_text(raw)
 provenance['variants'][name]={'command':cmd,'cpp_sha256':hashlib.sha256(s.encode()).hexdigest(),'binary_sha256':hashlib.sha256((out/name).read_bytes()).hexdigest(),'max_native_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines())};print('verified',name,flush=True)
(out/'experiment-provenance.json').write_text(json.dumps(provenance,indent=2))
names=['baseline',*variants];timings={n:[] for n in names}
for repeat in range(3):
 order=names[repeat:]+names[:repeat]
 if repeat==1:order.reverse()
 for n in order:
  raw=subprocess.check_output([str(out/n),'measure','0','256'],text=True,timeout=60);(out/f'experiment-{n}-{repeat}.jsonl').write_text(raw)
  rows=[json.loads(x) for x in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:]);timings[n]+=[r['ns']/r['iterations'] for r in rows[1:]]
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'speedup':statistics.median(timings['baseline'])/statistics.median(v)} for n,v in timings.items()}
(out/'experiment-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
