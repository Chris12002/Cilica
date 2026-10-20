from pathlib import Path
import subprocess,json,statistics,hashlib
out=Path(__file__).resolve().parent;expected=json.loads((out/'baseline-verify.json').read_text())
# Diagnostic only: move typed storage onto native stack, retain accounting/root lifetimes.
# Does not establish bounded native-stack/recursion contracts.
def stack(s):
 a='template<class T> struct Storage {\n';assert s.count(a)==1
 s=s.replace(a,a+'    alignas(T) std::byte native_storage[sizeof(T)];\n',1)
 a='unpoison_storage(c.execution + mark + padding, sizeof(T));';assert s.count(a)==1
 s=s.replace(a,'unpoison_storage(native_storage, sizeof(T));',1)
 a='value = ::new (static_cast<void*>(c.execution + mark + padding)) T;';assert s.count(a)==1
 return s.replace(a,'value = ::new (static_cast<void*>(native_storage)) T;',1)
provenance={}
for name,parent in [('native_frames','baseline'),('combined_native_frames','all_inline_types_hash_flat')]:
 s=stack((out/(parent+'.cpp')).read_text());(out/(name+'.cpp')).write_text(s)
 cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(out/(name+'.cpp')),'-o',str(out/name)]
 r=subprocess.run(cmd,capture_output=True,text=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 raw=subprocess.check_output([str(out/name),'verify','0'],text=True,timeout=120);assert json.loads(raw)==expected;(out/(name+'-verify.json')).write_text(raw)
 provenance[name]={'command':cmd,'source_sha256':hashlib.sha256(s.encode()).hexdigest(),'binary_sha256':hashlib.sha256((out/name).read_bytes()).hexdigest(),'max_native_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines())};print('verified',name,flush=True)
(out/'frame-provenance.json').write_text(json.dumps(provenance,indent=2))
names=['baseline','all_inline_types_hash_flat','native_frames','combined_native_frames'];results={n:[] for n in names}
for repeat in range(3):
 order=names[repeat:]+names[:repeat]
 if repeat==1:order.reverse()
 for name in order:
  raw=subprocess.check_output([str(out/name),'measure','0','256'],text=True,timeout=60);(out/f'frame-{name}-{repeat}.jsonl').write_text(raw)
  rows=[json.loads(x) for x in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:])
  results[name]+=[r['ns']/r['iterations'] for r in rows[1:]]
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v)} for n,v in results.items()}
for n in names:summary[n]['speedup_vs_base']=summary['baseline']['median_ns']/summary[n]['median_ns']
(out/'frame-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
