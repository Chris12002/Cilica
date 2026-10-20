from pathlib import Path
import re,subprocess,json,statistics,hashlib
out=Path(__file__).resolve().parent;base=(out/'baseline.cpp').read_text();expected=json.loads((out/'baseline-verify.json').read_text())
# Load diagnostic transformations without rerunning their benchmark driver.
exec((out/'experiments.py').read_text().split("variants={'reads':")[0])
def borrow_gate(s):
 old='struct Frame {\n    Frame* previous = nullptr;';assert s.count(old)==1;s=s.replace(old,old+'\n    mutable bool has_borrows = false;')
 old='f.previous = c.top; f.slots = roots; f.count = n; c.top = &f; ++c.depth;';assert s.count(old)==1;s=s.replace(old,'f.has_borrows = false; '+old)
 old='if (context.borrows != nullptr) retire_borrows(context, frame);';assert s.count(old)==1;s=s.replace(old,'if (frame.has_borrows) retire_borrows(context, frame);')
 old='        c.borrows = &frame;';assert s.count(old)==1;s=s.replace(old,'        roots.has_borrows = true;\n'+old)
 return s
def string_fast(s):
 for n in ['allocate_string','string_from_character']:
  s,c=re.subn(r'\[\[maybe_unused, gnu::noinline\]\] static ([^\n]+ '+n+r'\()',r'[[maybe_unused, gnu::always_inline]] static \1',s);assert c==1
 return s
def gc_fuse(s):
 # Reuse marks cleared after every completed/aborted traversal; clear survivors while sweeping.
 old='''    for (Object* value = heap.head; value != nullptr; value = value->next) {
        value->marked = false; value->mark_next = nullptr;
    }
    auto result = visit_roots''';assert s.count(old)==1;s=s.replace(old,'    auto result = visit_roots')
 old='if (value->marked) { link = &value->next; continue; }';assert s.count(old)==1;s=s.replace(old,'if (value->marked) { value->marked = false; value->mark_next = nullptr; link = &value->next; continue; }')
 old='''        value->type->destroy(value);
    }
    return report;''';assert s.count(old)==1;s=s.replace(old,'''        value->type->destroy(value);
    }
    state.prepared = false; // Sweep has cleared all surviving scratch.
    return report;''')
 return s
variants={'borrow_gate':borrow_gate(base),'string_fast':string_fast(base),'gc_fuse':gc_fuse(base),'reads_array':array_inline(reads(base)),'reads_borrow':borrow_gate(reads(base))}
provenance={}
for name,s in variants.items():
 cpp=out/(name+'.cpp');cpp.write_text(s);exe=out/name;cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(cpp),'-o',str(exe)]
 r=subprocess.run(cmd,text=True,capture_output=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode();raw=subprocess.check_output([str(exe),'verify','0'],text=True,timeout=120);assert json.loads(raw)==expected
 provenance[name]={'command':cmd,'cpp_sha256':hashlib.sha256(cpp.read_bytes()).hexdigest(),'bytes':exe.stat().st_size,'max_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines())};print('verified',name,flush=True)
(out/'followup-provenance.json').write_text(json.dumps(provenance,indent=2))
results={n:[] for n in ['baseline','reads',*variants]};processes={n:[] for n in results}
for repeat in range(3):
 names=list(results);names=names[repeat:]+names[:repeat]
 if repeat%2:names.reverse()
 for name in names:
  raw=subprocess.check_output([str(out/name),'measure','0','1024'],text=True,timeout=60);(out/f'followup-{name}-{repeat}.jsonl').write_text(raw);rows=[json.loads(l) for l in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:]);v=[r['ns']/r['iterations'] for r in rows[1:]];results[name]+=v;processes[name].append(statistics.median(v))
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'process_medians':processes[n],'reduction_percent':100*(1-statistics.median(v)/statistics.median(results['baseline']))} for n,v in results.items()}
(out/'followup-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
