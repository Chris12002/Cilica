from pathlib import Path
import re,json,subprocess,statistics,hashlib
out=Path(__file__).resolve().parent
base=(out/'baseline.cpp').read_text();expected={0:json.loads((out/'baseline-verify.json').read_text()),1:json.loads((out/'dotnet-line-verify.json').read_text())}
def wrapper_inline(s):
 kernels=set(re.findall(r'CILICA_TYPED_KERNEL static bool (cilica_m_\w+)_kernel\(',s));chosen=[]
 for name in sorted(kernels):
  declaration=re.search(r'\[\[maybe_unused\]\] static [^\n]* '+name+r'_typed\(([^\n]+)\) noexcept;',s)
  assert declaration,name
  if 'Location' in declaration[1]:continue
  s,n=re.subn(r'\[\[maybe_unused\]\]( static [^\n]* '+name+r'_typed\()',r'[[maybe_unused, gnu::always_inline]]\1',s)
  assert n==2,(name,n);chosen.append(name)
 return s,chosen
variants={n:wrapper_inline((out/file).read_text()) for n,file in [('wrapper_inline','baseline.cpp'),('combined_wrapper','combined.cpp')]};provenance={}
for name,(s,chosen) in variants.items():
 cpp=out/(name+'.cpp');cpp.write_text(s);exe=out/name;cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(cpp),'-o',str(exe)]
 r=subprocess.run(cmd,text=True,capture_output=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 for work in [0,1]:
  raw=subprocess.check_output([str(exe),'verify',str(work)],text=True,timeout=60);assert json.loads(raw)==expected[work];(out/(name+f'-verify-{work}.json')).write_text(raw)
 provenance[name]={'command':cmd,'cpp_sha256':hashlib.sha256(cpp.read_bytes()).hexdigest(),'bytes':exe.stat().st_size,'max_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines()),'selected_wrappers':chosen};print('verified',name,len(chosen),flush=True)
(out/'wrapper-provenance.json').write_text(json.dumps(provenance,indent=2))
results={str(w):{n:[] for n in ['baseline','combined',*variants]} for w in [0,1]}
for repeat in range(3):
 for work in [0,1]:
  names=list(results[str(work)]);names=names[repeat:]+names[:repeat]
  if repeat%2:names.reverse()
  for name in names:
   raw=subprocess.check_output([str(out/name),'measure',str(work),'1024'],text=True,timeout=60);(out/f'wrapper-{name}-work{work}-{repeat}.jsonl').write_text(raw);rows=[json.loads(l) for l in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==expected[work]['checksum']*r['iterations'] for r in rows[1:]);results[str(work)][name]+=[r['ns']/r['iterations'] for r in rows[1:]]
summary={w:{n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'reduction_percent':100*(1-statistics.median(v)/statistics.median(values['baseline']))} for n,v in values.items()} for w,values in results.items()}
(out/'wrapper-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
