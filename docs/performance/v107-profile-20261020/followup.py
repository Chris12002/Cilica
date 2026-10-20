from pathlib import Path
import re,subprocess,json,statistics,hashlib
out=Path(__file__).resolve().parent;base=(out/'baseline.cpp').read_text();expected=json.loads((out/'baseline-verify.json').read_text())
def small(s):
 pattern=r'(\[\[maybe_unused, gnu::noinline\]\] static bool t_\w+_(?:accepts|implements)\(const Descriptor\* type\) noexcept \{)([\s\S]*?)(\n\})'
 count=0
 def edit(m):
  nonlocal count
  if '_lookup.contains' in m[2]:return m[0]
  count+=1;return m[0].replace('gnu::noinline','gnu::always_inline',1)
 s,n=re.subn(pattern,edit,s);assert n>10 and count>5,(n,count);print('small predicates',count,'of',n,flush=True);return s
variants={'small_predicates':small(base),'kernels_predicates':re.sub(r'\[\[maybe_unused\]\] static bool (cilica_m_\w+_kernel\()',r'[[maybe_unused, gnu::always_inline]] static bool \1',(out/'predicates.cpp').read_text())}
provenance={}
for name,s in variants.items():
 cpp=out/(name+'.cpp');cpp.write_text(s);exe=out/name
 cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(cpp),'-o',str(exe)]
 r=subprocess.run(cmd,text=True,capture_output=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 raw=subprocess.check_output([str(exe),'verify','0'],text=True,timeout=120);assert json.loads(raw)==expected
 provenance[name]={'command':cmd,'cpp_sha256':hashlib.sha256(cpp.read_bytes()).hexdigest(),'bytes':exe.stat().st_size,'max_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines())};print('verified',name,flush=True)
(out/'followup-provenance.json').write_text(json.dumps(provenance,indent=2))
results={n:[] for n in ['baseline','kernels','predicates','small_predicates','kernels_predicates']};processes={n:[] for n in results}
for repeat in range(5):
 names=list(results);names=names[repeat:]+names[:repeat]
 if repeat%2:names.reverse()
 for name in names:
  raw=subprocess.check_output([str(out/name),'measure','0','1024'],text=True,timeout=60);(out/f'followup-{name}-{repeat}.jsonl').write_text(raw)
  rows=[json.loads(l) for l in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:]);v=[r['ns']/r['iterations'] for r in rows[1:]];results[name]+=v;processes[name].append(statistics.median(v))
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'process_medians':processes[n],'reduction_percent':100*(1-statistics.median(v)/statistics.median(results['baseline']))} for n,v in results.items()}
(out/'followup-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
