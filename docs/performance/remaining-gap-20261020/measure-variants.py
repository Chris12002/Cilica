from pathlib import Path
import subprocess,json,statistics,hashlib
out=Path(__file__).resolve().parent
expected=json.loads((out/'baseline-verify.json').read_text())
# Compile source-level experiments and their combined counterpart before timing.
ns={'__file__':str(out/'variants.py')};exec((out/'variants.py').read_text().split('\nvariants={',1)[0],ns)
combined=ns['types'](ns['inline']((out/'hash_flat.cpp').read_text()));(out/'all_inline_types_hash_flat.cpp').write_text(combined)
metadata={}
for name in ['hash_snapshot','hash_flat','all_inline_types_hash_flat']:
 command=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(out/(name+'.cpp')),'-o',str(out/name)]
 r=subprocess.run(command,capture_output=True,text=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 raw=subprocess.check_output([str(out/name),'verify','0'],text=True,timeout=120);(out/(name+'-verify.json')).write_text(raw);assert json.loads(raw)==expected
 metadata[name]={'command':command,'source_sha256':hashlib.sha256((out/(name+'.cpp')).read_bytes()).hexdigest(),'binary_sha256':hashlib.sha256((out/name).read_bytes()).hexdigest(),'max_native_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines())}
 print('verified',name,flush=True)
(out/'hash-variant-provenance.json').write_text(json.dumps(metadata,indent=2))
names=['baseline','borrow_inline','types','all_inline','borrow_inline_types','all_inline_types','hash_snapshot','hash_flat','all_inline_types_hash_flat']
results={n:[] for n in names}
for repeat in range(3):
 order=names[repeat*3:]+names[:repeat*3]
 if repeat==1:order.reverse()
 for name in order:
  raw=subprocess.check_output([str(out/name),'measure','0','256'],text=True,timeout=60);(out/f'variant-{name}-{repeat}.jsonl').write_text(raw)
  rows=[json.loads(x) for x in raw.splitlines()];assert len(rows)==8
  assert all(r['checksum']==6146*r['iterations'] and r['iterations']==256 for r in rows[1:])
  results[name]+=[r['ns']/r['iterations'] for r in rows[1:]]
 print('timed round',repeat,flush=True)
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v)} for n,v in results.items()}
for n in names:summary[n]['speedup_vs_base']=summary['baseline']['median_ns']/summary[n]['median_ns']
(out/'variant-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
