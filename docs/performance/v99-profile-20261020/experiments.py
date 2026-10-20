from pathlib import Path
import subprocess,json,hashlib,statistics,re
out=Path(__file__).resolve().parent;base=(out/'baseline.cpp').read_text();expected=json.loads((out/'baseline-verify.json').read_text())
def arrays(s):
 # Only generated method bodies: runtime GC, borrow validation and public checks retain full storage validation.
 pos=s.index('struct cilica_m_');prefix=s[:pos];body=s[pos:]
 helper='[[maybe_unused]] static bool execution_array_storage(Object* value) noexcept { return !CILICA_VERIFY_INTERNAL_REFERENCES || valid_array_storage(value); }\n'
 assert body.count('valid_array_storage(')>10
 return prefix+helper+body.replace('valid_array_storage(', 'execution_array_storage(')
def shortstrings(s):
 # A real embedded scalar preserves C++ provenance and fresh string identity. This changes object size/GC pressure too.
 a='    const std::uint16_t* units = nullptr;';assert s.count(a)==1;s=s.replace(a,a+'\n    std::uint16_t inline_unit = 0;')
 a='    delete[] text->units; delete text;';assert s.count(a)==1;s=s.replace(a,'    if (text->units != &text->inline_unit) delete[] text->units; delete text;')
 a='auto* units = length == 0 ? nullptr : new (std::nothrow) std::uint16_t[length];';assert s.count(a)==1;s=s.replace(a,'auto* units = length == 0 ? nullptr : length == 1 ? &value->inline_unit : new (std::nothrow) std::uint16_t[length];')
 return s
def charwrappers(s):
 # Diagnostic fast paths keep caller roots, managed depth and carrier validation. No result caching.
 for tok,code in [('060002a2','''
    if (context.depth > 0 && context.depth < 62 && context.pending == 0 && context.exception == nullptr) {
        auto location=arguments[0].location;location.read_only=true;
        std::uint32_t character=0;
        if (valid_location_argument(context,location,StorageKind::integer,nullptr,true,LocationNarrow::UInt16)
            && load_location_integer(context,location,character)==LocationStatus::complete) {
            Frame frame;RootScope roots(context,frame,nullptr,0);LocationArgument next{};next.bits=character&0xffffu;
            return cilica_m_655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1_060002a3(context,&next);
        }
    }
'''),('060002a3','''
    if (context.depth > 0 && context.depth < 63 && context.pending == 0 && context.exception == nullptr) {
        Frame frame;RootScope roots(context,frame,nullptr,0);
        auto* result=string_from_character(context,t_655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1_0200008a_descriptor,arguments[0].bits&0xffffu);
        if(result==nullptr){context.pending=context.pending==0?5u:context.pending;context.image="655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1";context.method=0x060002a3u;context.il=1u;}
        return result;
    }
''')]:
  pattern=r'(\[\[[^\n]*\]\] static Object\* cilica_m_655\w*_'+tok+r'\([^\n]+\) noexcept \{\n)'
  s,n=re.subn(pattern,lambda m:m[1]+code,s);assert n==1,(tok,n)
 return s
def cache(s):
 a='if (++count > context.instance.count || !traceable_reference(context.instance, value)\n            || value->type->array_kind == 0 || entry->length != 0) { context.pending = 6;';assert s.count(a)==1
 return s.replace(a,a.replace('traceable_reference','managed_reference'))
variants={'array_diagnostics':arrays(base),'short_strings':shortstrings(base),'char_wrappers':charwrappers(base),'character_combined':shortstrings(charwrappers(base)),'cache_check_ablation':cache(base),'combined':arrays(shortstrings(charwrappers(base)))}
provenance={'diagnostic_only':True,'revision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'variants':{}}
for name,s in variants.items():
 p=out/(name+'.cpp');reuse=p.exists() and p.read_text()==s and (out/(name+'-verify.json')).exists();p.write_text(s)
 cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(p),'-o',str(out/name)]
 if not reuse:
  r=subprocess.run(cmd,capture_output=True,text=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 raw=subprocess.check_output([str(out/name),'verify','0'],text=True,timeout=120);assert json.loads(raw)==expected;(out/(name+'-verify.json')).write_text(raw)
 provenance['variants'][name]={'command':cmd,'cpp_sha256':hashlib.sha256(s.encode()).hexdigest(),'binary_sha256':hashlib.sha256((out/name).read_bytes()).hexdigest(),'max_native_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines())};print('verified',name,flush=True)
(out/'experiment-provenance.json').write_text(json.dumps(provenance,indent=2))
names=['baseline',*variants];timings={n:[] for n in names}
for repeat in range(3):
 order=names[repeat:]+names[:repeat]
 if repeat==1:order.reverse()
 for n in order:
  raw=subprocess.check_output([str(out/n),'measure','0','256'],text=True,timeout=60);(out/f'experiment-{n}-{repeat}.jsonl').write_text(raw)
  rows=[json.loads(x) for x in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:]);timings[n]+=[r['ns']/r['iterations'] for r in rows[1:]]
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'speedup':statistics.median(timings['baseline'])/statistics.median(v)} for n,v in timings.items()}
(out/'experiment-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
