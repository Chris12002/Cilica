from pathlib import Path
import re,subprocess,json,hashlib,statistics,os
OUT=Path(__file__).resolve().parent
BASE=(OUT/'baseline.cpp').read_text()
EXPECTED=json.loads(Path('artifacts/leaf-getter-performance-20261020/native/expected.json').read_text())
def exact(s,a,b,n=1):
 assert s.count(a)==n,(a[:80],s.count(a),n)
 return s.replace(a,b)
def dispatch(s):
 s=s.replace(', gnu::noinline','').replace('gnu::noinline, ','').replace('[[gnu::noinline]] ','')
 pattern=r'static constexpr std::uint32_t \(\*table\[\]\)\(Context&, (\w+)\*\) noexcept = \{([^}]+)\};'
 def table(m):
  cases=''.join(f'case {i}u: return {name.strip()}(context, f);\n' for i,name in enumerate(m[2].split(',')))
  return f'static std::uint32_t dispatch_step(std::uint32_t step, Context& context, {m[1]}* f) noexcept {{ switch(step) {{ {cases} default: return frame_done; }} }}'
 s,n=re.subn(pattern,table,s);assert n>100
 s=s.replace('::table[step](context, f)','::dispatch_step(step, context, f)')
 s,n=re.subn(r'static std::uint32_t (s[0-9]+|dispatch_step)\(',r'[[gnu::always_inline]] static inline std::uint32_t \1(',s);assert n>100
 return s
def borrows(s):
 s=exact(s,'    BorrowFrame* borrows = nullptr;','    BorrowFrame* borrows = nullptr;\n    std::array<BorrowFrame*, 64> borrow_index{};')
 s=exact(s,'if (!active_root_frame(c, &roots) || roots.count > frame_root_limit','if (c.depth == 0 || c.depth > 64 || c.top != &roots || roots.count > frame_root_limit')
 s=exact(s,'const auto token = issue_borrow_token();\n        if (token == 0)', 'const auto serial = issue_borrow_token();\n        const auto token = serial <= (std::numeric_limits<std::uint64_t>::max() >> 6) ? (serial << 6) | (c.depth - 1) : 0;\n        if (serial == 0 || token == 0)')
 s=exact(s,'c.borrows = &frame; status = LocationStatus::complete;', 'c.borrow_index[token & 63] = &frame;\n        c.borrows = &frame; status = LocationStatus::complete;')
 s=exact(s,'~BorrowScope() noexcept { if (status == LocationStatus::complete) context.borrows = frame.previous; }','~BorrowScope() noexcept { if (status == LocationStatus::complete) { context.borrow_index[frame.token & 63] = nullptr; context.borrows = frame.previous; } }')
 old='''    const BorrowFrame* found = nullptr;
    std::size_t count = 0;
    for (const BorrowFrame* frame = context.borrows; frame != nullptr; frame = frame->previous) {
        if (++count > 64) return nullptr;
        if (frame->token == token) found = frame;
    }
    if (found == nullptr || found->roots == nullptr || !active_root_frame(context, found->roots)) return nullptr;'''
 new='''    const BorrowFrame* found = context.borrow_index[token & 63];
    if (found == nullptr || found->token != token || found->roots == nullptr) return nullptr;'''
 s=exact(s,old,new)
 return s
def types(s):
 sizes={m[2]:int(m[1]) for m in re.finditer(r'static constexpr std::array<const Descriptor\*, (\d+)> (\w+)_types =',s)}
 pattern=r'    for \(const auto\* receiver : (\w+)_types\) if \(receiver == type\) return true;\n    return false;'
 def indexed(m):
  n=sizes[m[1]]
  if n<8:return m[0]
  cap=1
  while cap<n*2:cap*=2
  return f'''    if (type == nullptr) return false;
    static const auto slots = [] {{
        std::array<const Descriptor*, {cap}> result{{}};
        for (auto* p : {m[1]}_types) {{
            auto a = reinterpret_cast<std::uintptr_t>(p);
            std::size_t i = ((a >> 3) ^ (a >> 17)) & {cap-1};
            while (result[i] != nullptr) i = (i + 1) & {cap-1};
            result[i] = p;
        }}
        return result;
    }}();
    auto a = reinterpret_cast<std::uintptr_t>(type);
    std::size_t i = ((a >> 3) ^ (a >> 17)) & {cap-1};
    while (slots[i] != nullptr) {{ if (slots[i] == type) return true; i = (i + 1) & {cap-1}; }}
    return false;'''
 s,n=re.subn(pattern,indexed,s);assert n>10
 return s
variants={}
for flags in range(8):
 name='base' if flags==0 else ''.join(c for bit,c in [(1,'D'),(2,'B'),(4,'T')] if flags&bit)
 source=BASE
 if flags&2:source=borrows(source)
 if flags&4:source=types(source)
 if flags&1:source=dispatch(source)
 variants[name]=source
provenance={'purpose':'Diagnostic factorial experiment, not production acceptance','revision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'compiler':subprocess.check_output(['clang++','--version'],text=True),'commands':{},'hashes':{},'notes':{'D':'whole method inlining and switch dispatch; all noinline removed','B':'per-context generation-checked borrow index; generated immediate-top registration only; retired roots rejected; no native harness acceptance','T':'static address hash for descriptor sets >=8; no descriptor dereference before membership'},'expected':EXPECTED}
for name,source in variants.items():
 path=OUT/(name+'.cpp');path.write_text(source)
 cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti',str(path),'-o',str(OUT/name)]
 provenance['commands'][name]=cmd
 r=subprocess.run(cmd,capture_output=True,text=True,timeout=240);(OUT/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 raw=subprocess.check_output([str(OUT/name),'verify','0'],text=True,timeout=60)
 (OUT/(name+'-verify.json')).write_text(raw);assert json.loads(raw)==EXPECTED
 provenance['hashes'][name]={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [path,OUT/name]}
 print(name,'compiled; 523 observations matched',flush=True)
(OUT/'factorial-provenance.json').write_text(json.dumps(provenance,indent=2))
# Native timing and .NET comparators are sequential, after all compilation.
host='/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs/host/DotNetHost.dll'
commands={name:[str(OUT/name),'measure','0','256'] for name in variants}
commands.update({'net_no_tiering':['dotnet',host,'measure','0','16384'],'net_default':['dotnet',host,'measure','0','16384']})
results={name:[] for name in commands}
for repeat in range(3):
 order=list(commands)
 # Rotating order distributes first/last positions; reverse second round.
 order=order[repeat*3:]+order[:repeat*3]
 if repeat==1:order.reverse()
 for name in order:
  env={k:v for k,v in os.environ.items() if not k.startswith(('DOTNET_Tier','COMPlus_Tier','DOTNET_Jit','COMPlus_Jit','DOTNET_ReadyToRun','COMPlus_ReadyToRun'))}
  if name=='net_no_tiering':env.update(DOTNET_TieredCompilation='0',DOTNET_ReadyToRun='0')
  raw=subprocess.check_output(commands[name],text=True,env=env,timeout=60)
  (OUT/f'timing-{name}-{repeat}.jsonl').write_text(raw)
  rows=[json.loads(line) for line in raw.splitlines()];assert len(rows)==8
  for row in rows[1:]:
   assert row['checksum']==6146*row['iterations']
   results[name].append(row['ns']/row['iterations'])
  print('timed',repeat,name,flush=True)
summary={name:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v)} for name,v in results.items()}
for name in summary:summary[name]['speedup_vs_base']=summary['base']['median_ns']/summary[name]['median_ns']
(OUT/'factorial-summary.json').write_text(json.dumps(summary,indent=2))
print(json.dumps(summary,indent=2))
