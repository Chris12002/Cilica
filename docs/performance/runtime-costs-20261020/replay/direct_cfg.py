from pathlib import Path
import re,subprocess,json,statistics,hashlib
out=Path(__file__).resolve().parent
s=(out/'BT.cpp').read_text()
s=s.replace(', gnu::noinline','').replace('gnu::noinline, ','').replace('[[gnu::noinline]] ','')
spaces={}
for m in re.finditer(r'namespace (cilica_m_\w+_steps) \{\n([\s\S]*?)static constexpr std::uint32_t \(\*table\[\]\)[^\n]+\n\}',s):
 name,body=m[1],m[2]
 labels={x[1]:int(x[2]) for x in re.finditer(r'static constexpr std::uint32_t (\w+) = (\d+)u;',body)}
 steps={int(x[1]):x[2] for x in re.finditer(r'static std::uint32_t s(\d+)\([^\n]+\) noexcept \{\n([\s\S]*?)\n\}',body)}
 assert steps and list(steps)==list(range(len(steps)))
 spaces[name]=(labels,steps)
pattern=r'    std::uint32_t step = 0;\n    while \(step != frame_done\) \{\n        f->exception_routed = false;\n        step = (cilica_m_\w+_steps)::table\[step\]\(context, f\);\n        if \(context.pending != 0u && !f->exception_routed\) step = \1::dispatch_exception\(context, f\);\n    \}'
static_edges=dynamic_edges=0
def driver(m):
 global static_edges,dynamic_edges
 name=m[1];labels,steps=spaces[name]
 def label(expr):
  if expr=='frame_done':return 'direct_done'
  if re.fullmatch(r'\d+u',expr):return 'direct_'+expr[:-1]
  if expr in labels:return 'direct_'+str(labels[expr])
  return None
 def ret(m):
  global static_edges,dynamic_edges
  expr=m[1].strip()
  check='if (context.pending != 0u && !f->exception_routed) { step = dispatch_exception(context, f); goto direct_dynamic; } '
  dest=label(expr)
  if dest:static_edges+=1;return '{ '+check+'goto '+dest+'; }'
  tern=re.fullmatch(r'(.+) \? (\w+) : (\w+)',expr)
  if tern and label(tern[2]) and label(tern[3]):
   static_edges+=2
   return '{ bool direct_taken = '+tern[1]+'; '+check+'if (direct_taken) goto '+label(tern[2])+'; else goto '+label(tern[3])+'; }'
  dynamic_edges+=1
  return '{ step = '+expr+'; '+check+'goto direct_dynamic; }'
 result='    using namespace '+name+';\n    std::uint32_t step = 0;\n    goto direct_0;\n'
 for i,body in steps.items():
  body=re.sub(r'\breturn ([^;]+);',ret,body)
  result+=f'direct_{i}: {{\n    f->exception_routed = false;\n'+body+'\n}\n'
 result+='direct_dynamic:\n    switch(step) {\n'
 for i in steps:result+=f'case {i}u: goto direct_{i};\n'
 result+='default: goto direct_done;\n}\ndirect_done:;'
 return result
s,n=re.subn(pattern,driver,s);assert n==len(spaces),(n,len(spaces))
path=out/'CFG_BT.cpp';path.write_text(s)
cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti',str(path),'-o',str(out/'CFG_BT')]
r=subprocess.run(cmd,capture_output=True,text=True,timeout=240);(out/'CFG_BT-build.log').write_text(r.stdout+r.stderr);r.check_returncode()
raw=subprocess.check_output([str(out/'CFG_BT'),'verify','0'],text=True,timeout=60)
(out/'CFG_BT-verify.json').write_text(raw);assert json.loads(raw)==json.loads((out/'base-verify.json').read_text())
(out/'CFG-provenance.json').write_text(json.dumps({'diagnostic_only':True,'methods':n,'static_edges':static_edges,'dynamic_edges':dynamic_edges,'command':cmd,'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'binary_sha256':hashlib.sha256((out/'CFG_BT').read_bytes()).hexdigest()},indent=2))
print('Direct CFG',n,'methods',static_edges,'static edges',dynamic_edges,'dynamic edges; 523 outputs matched',flush=True)
# Run measurement separately, after profiling finishes.
