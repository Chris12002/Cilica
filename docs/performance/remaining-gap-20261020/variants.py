from pathlib import Path
import re,subprocess,json,hashlib,statistics
out=Path(__file__).resolve().parent
base=(out/'baseline.cpp').read_text();expected=json.loads((out/'baseline-verify.json').read_text())
def inline(s):return s.replace(', gnu::noinline','').replace('gnu::noinline, ','').replace('[[gnu::noinline]] ','')
locations=(out/'source/src/Cilica.Compiler/ObjectCppLocations.cs').read_text().split('internal const string Text = """\n',1)[1].split('\n""";',1)[0]
assert base.count(locations)==1
# Same collision-checked address membership experiment as the preceding investigation.
ns={};exec((out/'descriptor-transform.py').read_text(),ns)
types=ns['types']
variants={'borrow_inline':base.replace(locations,inline(locations)),'types':types(base),'all_inline':inline(base),'borrow_inline_types':types(base.replace(locations,inline(locations))),'all_inline_types':types(inline(base))}
provenance={'revision':'8b552e4','diagnostic_only':True,'notes':{'borrow_inline':'Permit inlining only in the Location runtime component; all checks retained','types':'Replace descriptor sets >=8 with collision-checked native address hash membership; no descriptor dereference','all_inline':'Remove noinline attributes throughout emitted runtime; all checks and managed frames retained'},'commands':{},'hashes':{}}
for name,s in variants.items():
 path=out/(name+'.cpp');path.write_text(s)
 cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(path),'-o',str(out/name)]
 r=subprocess.run(cmd,capture_output=True,text=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 raw=subprocess.check_output([str(out/name),'verify','0'],text=True,timeout=120);(out/(name+'-verify.json')).write_text(raw);assert json.loads(raw)==expected
 provenance['commands'][name]=cmd;provenance['hashes'][name]={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [path,out/name]}
 rows=[l.split('\t') for l in (out/(name+'.su')).read_text().splitlines()];provenance[name+'_max_frame']=max(int(r[-2]) for r in rows)
 print('verified',name,flush=True)
(out/'variant-provenance.json').write_text(json.dumps(provenance,indent=2))
# Timing is performed later, after source-level variants have also finished compiling.
