from pathlib import Path
import subprocess,json,statistics,hashlib
out=Path(__file__).resolve().parent;expected=json.loads((out/'baseline-verify.json').read_text())
def bigger(s):
 for a,b in [('std::size_t limit = 1048576, object_limit = 4096;','std::size_t limit = 4194304, object_limit = 16384;'),('std::size_t objects = 4096;','std::size_t objects = 16384;'),('std::size_t edges = 262144;','std::size_t edges = 1048576;')]:
  assert s.count(a)==1;s=s.replace(a,b)
 return s
provenance={}
for name,parent in [('heap4x','baseline'),('cache_heap4x','empty_cache')]:
 s=bigger((out/(parent+'.cpp')).read_text());p=out/(name+'.cpp');p.write_text(s)
 cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti',str(p),'-o',str(out/name)]
 r=subprocess.run(cmd,capture_output=True,text=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 raw=subprocess.check_output([str(out/name),'verify','0'],text=True,timeout=120);assert json.loads(raw)==expected;(out/(name+'-verify.json')).write_text(raw)
 provenance[name]={'cpp_sha256':hashlib.sha256(s.encode()).hexdigest(),'command':cmd};print('verified',name,flush=True)
(out/'heap-provenance.json').write_text(json.dumps(provenance,indent=2))
names=['baseline','heap4x','empty_cache','cache_heap4x'];timings={n:[] for n in names}
for repeat in range(3):
 for n in (names if repeat!=1 else names[::-1]):
  raw=subprocess.check_output([str(out/n),'measure','0','256'],text=True,timeout=60);(out/f'heap-{n}-{repeat}.jsonl').write_text(raw)
  rows=[json.loads(l) for l in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:]);timings[n]+=[r['ns']/r['iterations'] for r in rows[1:]]
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v)} for n,v in timings.items()};(out/'heap-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2),flush=True)
# Untimed counts, including observed managed peak just before allocation (not RSS).
for name in ['baseline','heap4x']:
 s=(out/'counted.cpp').read_text()
 if name=='heap4x':s=bigger(s)
 s='static unsigned long long gc_calls=0,gc_live=0,gc_dead=0,peak_objects=0,peak_bytes=0;\n'+s
 a='static MarkReport collect(Context& context, MarkLimits limits = {}) noexcept {';assert s.count(a)==1;s=s.replace(a,a+'++gc_calls;')
 a='if (report.status != MarkStatus::complete) return report;\n    Object** link';assert s.count(a)==1;s=s.replace(a,'if (report.status != MarkStatus::complete) return report;\n    gc_live+=report.reachable;gc_dead+=report.unreachable;\n    Object** link')
 a='static bool allocation_room(Context& context, std::size_t bytes, bool& attempted) noexcept {';assert s.count(a)==1;s=s.replace(a,a+'\n    peak_objects=std::max(peak_objects,static_cast<unsigned long long>(context.instance.count));peak_bytes=std::max(peak_bytes,static_cast<unsigned long long>(context.instance.bytes));')
 a='for(auto& n:method_counts)n=0;';assert s.count(a)==1;s=s.replace(a,'gc_calls=gc_live=gc_dead=peak_objects=peak_bytes=0;'+a)
 a='std::fprintf(stderr,"extra';s=s.replace(a,'std::fprintf(stderr,"GC calls=%llu live=%llu dead=%llu peak_objects=%llu peak_bytes=%llu\\n",gc_calls,gc_live,gc_dead,peak_objects,peak_bytes);\n'+a)
 p=out/(name+'-gc.cpp');p.write_text(s);exe=out/(name+'-gc')
 subprocess.run(['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti',str(p),'-o',str(exe)],check=True,timeout=240)
 r=subprocess.run([str(exe),'cold','0'],capture_output=True,text=True,check=True,timeout=60);assert json.loads(r.stdout)['checksum']==6146*256
 (out/(name+'-gc.txt')).write_text(r.stderr);print(name,[l for l in r.stderr.splitlines() if l.startswith('GC')],flush=True)
