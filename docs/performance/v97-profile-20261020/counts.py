from pathlib import Path
import re,subprocess,json
out=Path(__file__).resolve().parent
s=(out/'baseline.cpp').read_text()
pattern=r'(\[\[[^\n]*\]\] static [^\n]+ (cilica_m_\w+)\([^\n]*\) noexcept \{\n)'
names=[]
def entry(m):
 i=len(names);names.append(m[2]);return m[1]+f'    ++method_counts[{i}];\n'
s,n=re.subn(pattern,entry,s);assert n>100
s='#include <cstdio>\nstatic unsigned long long method_counts['+str(n)+']{};\nstatic unsigned long long steps=0, frames=0, frame_bytes=0, type_calls=0, type_comparisons=0, active_visits=0, borrow_visits=0, arrays=0, strings=0;\n'+s
s=s.replace('value = ::new (static_cast<void*>(c.execution + mark + padding)) T;', '++frames; frame_bytes += sizeof(T); value = ::new (static_cast<void*>(c.execution + mark + padding)) T;')
s=s.replace('        f->exception_routed = false;', '        ++steps; f->exception_routed = false;')
s,n=re.subn(r'    for \(const auto\* receiver : (\w+)_types\) if \(receiver == type\) return true;',r'    ++type_calls; for (const auto* receiver : \1_types) if ((++type_comparisons, receiver == type)) return true;',s);assert n>10
s=s.replace('if (frame == expected) found = true;', '++active_visits; if (frame == expected) found = true;')
s=s.replace('if (frame->token == token) found = frame;', '++borrow_visits; if (frame->token == token) found = frame;')
s,n=re.subn(r'(static Object\* allocate_array\([^\n]+\) noexcept \{)',r'\1 ++arrays;',s);assert n==1
s,n=re.subn(r'(static Object\* allocate_string\([\s\S]*?\) noexcept \{)',r'\1 ++strings;',s);assert n==1
printout=''
for i,name in enumerate(names): printout+=f'if(method_counts[{i}])std::fprintf(stderr,"method {name} %llu\\n",method_counts[{i}]);\n'
printout+='std::fprintf(stderr,"totals steps=%llu frames=%llu frame_bytes=%llu type_calls=%llu type_comparisons=%llu active_visits=%llu borrow_visits=%llu arrays=%llu strings=%llu\\n",steps,frames,frame_bytes,type_calls,type_comparisons,active_visits,borrow_visits,arrays,strings);\n'
s=s.replace('auto ns=std::chrono::duration_cast<std::chrono::nanoseconds>(clock::now()-start).count();\n        std::printf',printout+'auto ns=std::chrono::duration_cast<std::chrono::nanoseconds>(clock::now()-start).count();\n        std::printf',1)
# Warm the persistent instance, then count 256 diffs separately from warmup.
reset='for(auto& n:method_counts)n=0;steps=frames=frame_bytes=type_calls=type_comparisons=active_visits=borrow_visits=arrays=strings=0;'
s=s.replace('auto start=clock::now();auto checksum=call(c,work,1);','(void)call(c,work,8);'+reset+'auto start=clock::now();auto checksum=call(c,work,256);')
(out/'counted.cpp').write_text(s)
# Compile/run only after all release timings finish.
subprocess.run(['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti',str(out/'counted.cpp'),'-o',str(out/'counted')],check=True,timeout=240)
r=subprocess.run([str(out/'counted'),'cold','0'],capture_output=True,text=True,check=True,timeout=60)
(out/'counts.txt').write_text(r.stderr);(out/'counted-output.json').write_text(r.stdout)
methods={m['token']:m for m in json.loads((out/'core-methods.json').read_text())['methods']}
rows=[]
for line in r.stderr.splitlines():
 if line.startswith('method '):
  _,symbol,count=line.split();token=re.search(r'_(060[0-9a-f]{5})',symbol)[1]
  m=methods.get(token) if '655cc451' in symbol else None
  rows.append({'symbol':symbol,'count':int(count),'name':(m['type']+'.'+m['name']) if m else token})
(out/'method-counts.json').write_text(json.dumps(sorted(rows,key=lambda x:-x['count']),indent=2))
print(r.stderr.splitlines()[-1]);print(json.dumps(sorted(rows,key=lambda x:-x['count'])[:20],indent=2))
