from pathlib import Path
import re,subprocess,json
out=Path(__file__).resolve().parent;base=(out/'counted.cpp').read_text();summary={}
for name in ['baseline','larger_heap']:
 s=base
 if name=='larger_heap':s=s.replace('std::size_t limit = 1048576, object_limit = 4096;', 'std::size_t limit = 4194304, object_limit = 16384;').replace('std::size_t objects = 4096;', 'std::size_t objects = 16384;').replace('string_pool_limit = 16u * sizeof(StringSlab)','string_pool_limit = 64u * sizeof(StringSlab)')
 s='static unsigned long long gc_calls=0,gc_live=0,gc_dead=0,peak_objects=0,peak_bytes=0;\n'+s
 def replace(old,new):
  global s
  assert s.count(old)==1,(old[:80],s.count(old));s=s.replace(old,new)
 replace('static MarkReport collect(Context& context, MarkLimits limits = {}) noexcept {','static MarkReport collect(Context& context, MarkLimits limits = {}) noexcept { ++gc_calls;')
 replace('if (report.status != MarkStatus::complete) return report;\n    Object** link','if (report.status != MarkStatus::complete) return report;\n    gc_live+=report.reachable;gc_dead+=report.unreachable;\n    Object** link')
 replace('static bool allocation_room(Context& context, std::size_t bytes, bool& attempted) noexcept {','static bool allocation_room(Context& context, std::size_t bytes, bool& attempted) noexcept {\npeak_objects=std::max(peak_objects,static_cast<unsigned long long>(context.instance.count));peak_bytes=std::max(peak_bytes,static_cast<unsigned long long>(context.instance.bytes));')
 replace('for(auto& n:method_counts)n=0;','gc_calls=gc_live=gc_dead=peak_objects=peak_bytes=0;for(auto& n:method_counts)n=0;')
 replace('std::fprintf(stderr,"extra','std::fprintf(stderr,"metrics gc_calls=%llu gc_live=%llu gc_dead=%llu peak_objects=%llu peak_bytes=%llu pool_reserved_bytes=%zu pool_limit_bytes=%zu\\n",gc_calls,gc_live,gc_dead,peak_objects,peak_bytes,heap.string_pool_bytes,heap.string_pool_limit);\nstd::fprintf(stderr,"extra')
 cpp=out/(name+'-metrics.cpp');cpp.write_text(s);exe=out/(name+'-metrics');subprocess.run(['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti',str(cpp),'-o',str(exe)],check=True,timeout=240)
 r=subprocess.run([str(exe),'cold','0'],capture_output=True,text=True,check=True,timeout=90);assert json.loads(r.stdout)['checksum']==6146*256;(out/(name+'-metrics.txt')).write_text(r.stderr)
 line=next(l for l in r.stderr.splitlines() if l.startswith('metrics '));summary[name]={k:int(v) for k,v in re.findall(r'(\w+)=(\d+)',line)};print(name,line,flush=True)
(out/'metrics-summary.json').write_text(json.dumps(summary,indent=2))
