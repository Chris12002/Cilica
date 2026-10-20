from pathlib import Path
import re,subprocess,json,statistics,hashlib
out=Path(__file__).resolve().parent;base=(out/'baseline.cpp').read_text();expected=json.loads((out/'baseline-verify.json').read_text());small=[];broad=[]
for m in re.finditer(r'\[\[maybe_unused, gnu::noinline\]\] static bool (t_\w+_(?:accepts|implements))\(const Descriptor\* type\) noexcept \{([\s\S]*?)\n\}',base):
 (broad if 'DescriptorLookup<' in m[2] else small).append(m[1])
assert len(small)==70 and len(broad)==6
def inline(names):
 s=base
 for name in names:
  old='[[maybe_unused, gnu::noinline]] static bool '+name+'(';assert s.count(old)==2;s=s.replace(old,old.replace('gnu::noinline','gnu::always_inline'))
 return s
large=base.replace('std::size_t limit = 1048576, object_limit = 4096;', 'std::size_t limit = 4194304, object_limit = 16384;').replace('std::size_t objects = 4096;', 'std::size_t objects = 16384;').replace('string_pool_limit = 16u * sizeof(StringSlab)','string_pool_limit = 64u * sizeof(StringSlab)')
variants={'small_only':inline(small),'broad_only':inline(broad),'larger_heap':large};provenance={}
for name,s in variants.items():
 cpp=out/(name+'.cpp');cpp.write_text(s);exe=out/name
 cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(cpp),'-o',str(exe)]
 r=subprocess.run(cmd,text=True,capture_output=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 raw=subprocess.check_output([str(exe),'verify','0'],text=True,timeout=120);assert json.loads(raw)==expected
 provenance[name]={'command':cmd,'cpp_sha256':hashlib.sha256(cpp.read_bytes()).hexdigest(),'bytes':exe.stat().st_size,'max_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines())};print('verified',name,flush=True)
(out/'discriminate-provenance.json').write_text(json.dumps(provenance,indent=2))
results={n:[] for n in ['baseline','small_only','broad_only','predicates','kernels_predicates','larger_heap']};processes={n:[] for n in results}
for repeat in range(4):
 names=list(results);names=names[repeat:]+names[:repeat]
 if repeat%2:names.reverse()
 for name in names:
  raw=subprocess.check_output([str(out/name),'measure','0','1024'],text=True,timeout=60);(out/f'discriminate-{name}-{repeat}.jsonl').write_text(raw)
  rows=[json.loads(l) for l in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:]);v=[r['ns']/r['iterations'] for r in rows[1:]];results[name]+=v;processes[name].append(statistics.median(v))
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'process_medians':processes[n],'reduction_percent':100*(1-statistics.median(v)/statistics.median(results['baseline']))} for n,v in results.items()}
(out/'discriminate-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
