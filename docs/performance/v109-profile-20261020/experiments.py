"""Generated-code diagnostics only. No compiler/library edits."""
from pathlib import Path
import re,subprocess,json,statistics,hashlib
out=Path(__file__).resolve().parent;base=(out/'baseline.cpp').read_text();expected=json.loads((out/'baseline-verify.json').read_text());edits={}
def reads(s):
 names=['string_length','string_code_unit','array_length','load_integer_element','load_wide_element','load_reference_element']
 for name in names:
  s,n=re.subn(r'\[\[maybe_unused, gnu::noinline\]\] static ([^\n]+ '+name+r'\()',r'[[maybe_unused, gnu::always_inline]] static \1',s);assert n==1,(name,n)
 return s
def depth(s):
 # Explicit depth is not observable inside the proven nonallocating graph.
 # Preserve exact per-path limit checks, and leave wrapper/framed depth unchanged.
 s=re.sub(r'(_kernel\(context),',r'\1, context.depth,',s)
 s,n=re.subn(r'(CILICA_TYPED_KERNEL static bool [^\n]+_kernel\(\[\[maybe_unused\]\] Context& context)',r'\1, [[maybe_unused]] std::size_t kernel_depth',s);assert n==78
 pattern=r'(CILICA_TYPED_KERNEL static bool [^\n]+ noexcept \{\n)([\s\S]*?\n}\n)'
 def body(m):
  old='    if (context.depth >= 64u) return false;\n    TypedCallDepth depth(context);'
  assert old in m[2]
  b=m[2].replace(old,'    if (kernel_depth >= 64u) return false;').replace('_kernel(context, context.depth,','_kernel(context, kernel_depth + 1u,')
  return m[1]+b
 s,n=re.subn(pattern,body,s);assert n==39;return s
def array_inline(s):
 old='[[maybe_unused]] static bool valid_array_storage(';assert s.count(old)==1;return s.replace(old,'[[maybe_unused, gnu::always_inline]] static bool valid_array_storage(')
def compact(s):
 pattern=r'(?:\[\[maybe_unused\]\] )?static constexpr Descriptor (\w+_descriptor) = (\{[^\n]*\});'
 matches=list(re.finditer(pattern,s));assert len(matches)>30
 names=[m[1] for m in matches];index={n:i for i,n in enumerate(names)};n=len(names)
 s=s[:matches[0].start()]+f'extern const Descriptor compact_descriptors[{n}];\n'+s[matches[0].start():]
 def alias(m):return f'[[maybe_unused]] static constexpr const Descriptor& {m[1]} = compact_descriptors[{index[m[1]]}];'
 s=re.sub(pattern,alias,s)
 last='[[maybe_unused]] static constexpr const Descriptor& '+names[-1]+' = compact_descriptors['+str(n-1)+'];'
 s=s.replace(last,last+'\nconst Descriptor compact_descriptors['+str(n)+'] = {\n'+',\n'.join(m[2] for m in matches)+'\n};\n')
 sets={m[2]:re.findall(r'&(\w+_descriptor)',m[3]) for m in re.finditer(r'static constexpr std::array<const Descriptor\*, (\d+)> (\w+)_types = \{([^\n]*)\};',s) if int(m[1])>=8}
 assert len(sets)==6
 for name,members in sets.items():
  assert all(x in index for x in members)
  start=s.index('[[maybe_unused, gnu::noinline]] static bool '+name+'(const Descriptor* type) noexcept {');end=s.index('\n}\n',start)+3
  bits=', '.join('true' if d in members else 'false' for d in names)
  b=f'''[[maybe_unused, gnu::always_inline]] static bool {name}(const Descriptor* type) noexcept {{
    const auto offset = reinterpret_cast<std::uintptr_t>(type) - reinterpret_cast<std::uintptr_t>(compact_descriptors);
    if (offset >= sizeof(compact_descriptors) || offset % sizeof(Descriptor) != 0) return false;
    static constexpr bool accepted[{n}] = {{{bits}}};
    return accepted[offset / sizeof(Descriptor)];
}}
'''
  s=s[:start]+b+s[end:]
  old='[[maybe_unused, gnu::noinline]] static bool '+name+'(const Descriptor*) noexcept;'
  assert old in s;s=s.replace(old,old.replace('gnu::noinline','gnu::always_inline'))
 edits['compact_descriptors']=n;return s
variants={'reads':reads(base),'depth_value':depth(base),'array_inline':array_inline(base),'compact':compact(base),'reads_depth':depth(reads(base))}
provenance={}
for name,s in variants.items():
 cpp=out/(name+'.cpp');cpp.write_text(s);exe=out/name
 cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(cpp),'-o',str(exe)]
 r=subprocess.run(cmd,text=True,capture_output=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 raw=subprocess.check_output([str(exe),'verify','0'],text=True,timeout=120);assert json.loads(raw)==expected;(out/(name+'-verify.json')).write_text(raw)
 provenance[name]={'command':cmd,'cpp_sha256':hashlib.sha256(cpp.read_bytes()).hexdigest(),'bytes':exe.stat().st_size,'max_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines())};print('verified',name,flush=True)
(out/'experiment-provenance.json').write_text(json.dumps({'variants':provenance,'edits':edits},indent=2))
results={n:[] for n in ['baseline',*variants]};processes={n:[] for n in results}
for repeat in range(3):
 names=list(results);names=names[repeat:]+names[:repeat]
 if repeat%2:names.reverse()
 for name in names:
  raw=subprocess.check_output([str(out/name),'measure','0','1024'],text=True,timeout=60);(out/f'experiment-{name}-{repeat}.jsonl').write_text(raw);rows=[json.loads(l) for l in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:]);v=[r['ns']/r['iterations'] for r in rows[1:]];results[name]+=v;processes[name].append(statistics.median(v))
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'process_medians':processes[n],'reduction_percent':100*(1-statistics.median(v)/statistics.median(results['baseline']))} for n,v in results.items()}
(out/'experiment-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
