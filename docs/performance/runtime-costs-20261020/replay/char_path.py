from pathlib import Path
import re,subprocess,json,statistics,hashlib
p=Path(__file__).resolve().parent
s=(p/'CFG_BT.cpp').read_text()
core='10a99465bbb826b4dda27ab513e4e156da93ea25030d80f2cefed778b24bb5c3'
pat=rf'(\[\[maybe_unused\]\] static Object\* cilica_m_{core}_060002a2\([^\n]+\) noexcept \{{\n)([\s\S]*?\n    return f->result.reference;)\n\}}\n'
m=re.search(pat,s);assert m
body=m[2];assert 'BorrowScope borrowed' not in body
start=body.index('    using namespace ')
body=body[:start]+f'''    const std::uint16_t unit = static_cast<std::uint16_t>(arguments[0].bits);
    return allocate_string(context, t_{core}_0200008a_descriptor, &unit, 1, nullptr, 0);'''
s=s[:m.start(2)]+body+s[m.end(2):]
path=p/'CFG_BT_char.cpp';path.write_text(s)
cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti',str(path),'-o',str(p/'CFG_BT_char')]
r=subprocess.run(cmd,capture_output=True,text=True,timeout=240);(p/'CFG_BT_char-build.log').write_text(r.stdout+r.stderr);r.check_returncode()
raw=subprocess.check_output([str(p/'CFG_BT_char'),'verify','0'],text=True,timeout=60)
(p/'CFG_BT_char-verify.json').write_text(raw);assert json.loads(raw)==json.loads((p/'base-verify.json').read_text())
(p/'char-provenance.json').write_text(json.dumps({'diagnostic_only':True,'command':cmd,'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'binary_sha256':hashlib.sha256((p/'CFG_BT_char').read_bytes()).hexdigest(),'change':'Replace only static Char.ToString body after framed entry with direct existing string allocation; removes char[] and int[] temporary allocations; OOM/fault origins and all UTF16 not accepted'},indent=2))
results={n:[] for n in ['base','CFG_BT','CFG_BT_char']}
for i in range(3):
 order=list(results);order=order[i:]+order[:i]
 for name in order:
  raw=subprocess.check_output([str(p/name),'measure','0','256'],text=True,timeout=60)
  (p/f'char-timing-{name}-{i}.jsonl').write_text(raw)
  rows=[json.loads(x) for x in raw.splitlines()];assert len(rows)==8
  for r in rows[1:]:
   assert r['iterations']==256 and r['checksum']==6146*256
   results[name].append(r['ns']/256)
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v)} for n,v in results.items()}
for n in summary:summary[n]['speedup_vs_base']=summary['base']['median_ns']/summary[n]['median_ns']
(p/'char-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
