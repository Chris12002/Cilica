"""Diagnostic generated-C++ experiments; compiler and library remain unchanged."""
from pathlib import Path
import re,subprocess,json,statistics,hashlib
out=Path(__file__).resolve().parent;base=(out/'baseline.cpp').read_text();expected=json.loads((out/'baseline-verify.json').read_text());edits={}
def kernels(s):
 s,n=re.subn(r'\[\[maybe_unused\]\] static bool (cilica_m_\w+_kernel\()',r'[[maybe_unused, gnu::always_inline]] static bool \1',s);assert n>20;edits['kernels']=n;return s
def predicates(s):
 s,n=re.subn(r'\[\[maybe_unused, gnu::noinline\]\] static bool (t_\w+_(?:accepts|implements)\()',r'[[maybe_unused, gnu::always_inline]] static bool \1',s);assert n>10;edits['predicates']=n;return s
def short_string(s):
 old='[[maybe_unused, gnu::noinline]] static Object* allocate_string(';assert s.count(old)==1;return s.replace(old,'[[maybe_unused, gnu::always_inline]] static Object* allocate_string(')
def array_direct(s):
 # Resolve packet calls to the exact generated array Count/Get targets. Keep the
 # original dispatch pointer selection, runtime checks and typed entry validation.
 pattern=r'(\[\[maybe_unused\]\] static (std::int32_t|Object\*) (cilica_m_\w+_0600007[bc]_g_\w+)\(Context& context, \[\[maybe_unused\]\] const LocationArgument\* arguments\) noexcept \{\n    return [^\n]+_typed\(context[^\n]+\);\n\})'
 matches=[m for m in re.finditer(pattern,s) if "_g_aeba" in m[3]];assert len(matches)==2,len(matches)
 for m in matches:
  target=m[3];member='int32' if m[2]=='std::int32_t' else 'reference';args='f->arguments[0].reference'+('' if member=='int32' else ', f->arguments[1].bits')
  old=f'f->dispatch_{member}(context, f->arguments.data())'
  new=f'(f->dispatch_{member} == &{target} ? {target}_typed(context, {args}) : {old})'
  assert s.count(old)>0;s=s.replace(old,new)
 return s
variants={'kernels':kernels(base),'predicates':predicates(base),'array_direct':array_direct(base),'short_string':short_string(base),'kernels_array':array_direct(kernels(base))}
provenance={}
for name,s in variants.items():
 cpp=out/(name+'.cpp');cpp.write_text(s);exe=out/name
 cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(cpp),'-o',str(exe)]
 r=subprocess.run(cmd,text=True,capture_output=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 raw=subprocess.check_output([str(exe),'verify','0'],text=True,timeout=120);assert json.loads(raw)==expected;(out/(name+'-verify.json')).write_text(raw)
 provenance[name]={'command':cmd,'cpp_sha256':hashlib.sha256(cpp.read_bytes()).hexdigest(),'bytes':exe.stat().st_size,'max_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines())}
 print('built and verified',name,flush=True)
(out/'experiment-provenance.json').write_text(json.dumps({'variants':provenance,'edits':edits},indent=2))
results={n:[] for n in ['baseline',*variants]}
for repeat in range(3):
 names=list(results);names=names if repeat!=1 else names[::-1]
 for name in names:
  raw=subprocess.check_output([str(out/name),'measure','0','256'],text=True,timeout=60);(out/f'experiment-{name}-{repeat}.jsonl').write_text(raw)
  rows=[json.loads(l) for l in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:]);results[name]+=[r['ns']/r['iterations'] for r in rows[1:]]
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'reduction_percent':100*(1-statistics.median(v)/statistics.median(results['baseline']))} for n,v in results.items()}
(out/'experiment-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
