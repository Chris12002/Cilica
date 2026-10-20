from pathlib import Path
import subprocess,json,hashlib,statistics,re
out=Path(__file__).resolve().parent;base=(out/'baseline.cpp').read_text();expected=json.loads((out/'baseline-verify.json').read_text())
core='655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1';app='652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8'
def arrays(s):
 pos=s.index('struct cilica_m_');prefix=s[:pos];body=s[pos:]
 helper='[[maybe_unused]] static bool execution_array_storage(Object* value) noexcept { return !CILICA_VERIFY_INTERNAL_REFERENCES || valid_array_storage(value); }\n'
 assert body.count('valid_array_storage(')>10
 return prefix+helper+body.replace('valid_array_storage(', 'execution_array_storage(')
def cache(s):
 a='if (++count > context.instance.count || !traceable_reference(context.instance, value)\n            || value->type->array_kind == 0 || entry->length != 0) { context.pending = 6;';assert s.count(a)==1
 return s.replace(a,a.replace('traceable_reference','managed_reference'))
def resolved_once(s):
 a='''        std::uint32_t character = 0;
        if (valid_location_argument(context, location, StorageKind::integer, nullptr, true, LocationNarrow::UInt16)
            && load_location_integer(context, location, character) == LocationStatus::complete) {'''
 assert s.count(a)==1
 return s.replace(a,'''        const auto storage = location_storage(context, location, false);
        if (storage.status == LocationStatus::complete && storage.kind == StorageKind::integer
            && storage.accepts == nullptr && location.narrow == LocationNarrow::UInt16) {
            const auto character = *static_cast<const std::uint32_t*>(storage.address) & 0xffffu;''')
def charvalue(s):
 start=s.index('namespace cilica_m_'+app+'_06000021_steps');end=s.index('\nnamespace cilica_m_',start+12)
 body=s[start:end]
 a='frame_location(context, f->borrow_token, LocationOrigin::frame_integer, 1, false, LocationNarrow::UInt16)';assert body.count(a)==2
 body=body.replace(a,'LocationResult{LocationStatus::complete, {}}')
 call='cilica_m_'+core+'_060002a2(context, f->arguments.data())';assert body.count(call)==2
 body=body.replace(call,'character_value_bridge(context, f->integers[1], f->borrow_token)')
 # Keep the instance method depth and real root frame, plus a full fallback.
 helper='''[[maybe_unused]] static Object* character_value_bridge(Context& context, std::uint32_t character, std::uint64_t token) noexcept {
    if (context.depth != 0u && context.depth < 63u && context.pending == 0u && context.exception == nullptr) {
        Frame frame; RootScope roots(context, frame, nullptr, 0); LocationArgument next{}; next.bits=character&0xffffu;
        return cilica_m_CORE_060002a3(context,&next);
    }
    LocationArgument next{}; const auto location=frame_location(context,token,LocationOrigin::frame_integer,1,false,LocationNarrow::UInt16);
    if(location.status!=LocationStatus::complete){context.pending=6u;return nullptr;}
    next.location=location.value;return cilica_m_CORE_060002a2(context,&next);
}
'''.replace('CORE',core)
 return s[:start]+helper+body+s[end:]
def descriptors(s):
 # Upper-bound ablation ONLY in no-allocation kernels. No claim of a general proof.
 pattern=r'(\[\[maybe_unused\]\] static bool cilica_m_\w+_kernel\([^\n]+\) noexcept \{\n)'
 spans=[];count=0
 for m in re.finditer(pattern,s):
  depth=1;i=m.end()
  while depth:
   if s[i]=='{':depth+=1
   elif s[i]=='}':depth-=1
   i+=1
  body=s[m.end():i-1];body,n=re.subn(r't_[A-Za-z0-9_]+_accepts\([^()]*\)','true',body);count+=n;spans.append((m.end(),i-1,body))
 assert count>50,count
 for a,b,body in reversed(spans):s=s[:a]+body+s[b:]
 return s
variants={'array_diagnostics':arrays(base),'empty_cache':cache(base),'char_resolve_once':resolved_once(base),'char_value_bridge':charvalue(base),'kernel_type_ablation':descriptors(base),'checked_candidates':charvalue(cache(arrays(base))),'all_diagnostic':descriptors(charvalue(cache(arrays(base))))}
provenance={'diagnostic_only':True,'revision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'variants':{}}
for name,s in variants.items():
 p=out/(name+'.cpp');p.write_text(s)
 cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(p),'-o',str(out/name)]
 r=subprocess.run(cmd,capture_output=True,text=True,timeout=240);(out/(name+'-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 raw=subprocess.check_output([str(out/name),'verify','0'],text=True,timeout=120);assert json.loads(raw)==expected,name;(out/(name+'-verify.json')).write_text(raw)
 provenance['variants'][name]={'command':cmd,'cpp_sha256':hashlib.sha256(s.encode()).hexdigest(),'binary_sha256':hashlib.sha256((out/name).read_bytes()).hexdigest(),'max_native_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines())};print('verified',name,flush=True)
(out/'experiment-provenance.json').write_text(json.dumps(provenance,indent=2))
names=['baseline',*variants];timings={n:[] for n in names}
for repeat in range(3):
 order=names[repeat:]+names[:repeat]
 if repeat==1:order.reverse()
 for n in order:
  raw=subprocess.check_output([str(out/n),'measure','0','256'],text=True,timeout=60);(out/f'experiment-{n}-{repeat}.jsonl').write_text(raw)
  rows=[json.loads(x) for x in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:]);timings[n]+=[r['ns']/r['iterations'] for r in rows[1:]]
summary={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'time_reduction':1-statistics.median(v)/statistics.median(timings['baseline'])} for n,v in timings.items()}
(out/'experiment-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
