from pathlib import Path
exec(compile((Path(__file__).parent / 'experiment-functions.py').read_text(), 'experiment-functions.py', 'exec'))

core='655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1'
app='652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8'
generic='11a92e1f79aac0ad51161d12c4d589413c1a856820041502db8214b5ceb0caa3'
target=f'cilica_m_{core}_060004a0_g_{generic}'

def inline_fields(s):
    s=field_offsets(s)
    return replace(s, '[[maybe_unused, gnu::noinline]] static void* field_address',
        '[[maybe_unused]] static inline void* field_address')

def local_out(s):
    a=s.index(f'namespace cilica_m_{app}_06000011_steps')
    b=s.index('\nnamespace cilica_m_',a+20)
    body=s[a:b]
    expr='frame_location(context, f->borrow_token, LocationOrigin::frame_integer, 2, false, LocationNarrow::none)'
    assert body.count(expr)==2
    body=body.replace(expr, f'(local_lookup_ready(context, f->roots[29]) ? LocationResult{{LocationStatus::complete, {{}}}} : {expr})')
    call='f->integers[10] = std::bit_cast<std::uint32_t>(f->dispatch_int32(context, f->arguments.data()))'
    assert body.count(call)==2
    body=body.replace(call,'f->integers[10] = std::bit_cast<std::uint32_t>(local_lookup(context, f->roots[29], f->roots[30], f->integers[2], f->borrow_token, f->dispatch_int32))')
    # Benchmark-only exact target. Production must prove adjacent exclusive use,
    # stage aliases, and preserve fallback/fault order for arbitrary IR.
    helper=f'''
static bool local_lookup_ready(Context& context, Object* receiver) noexcept {{
    return CILICA_TYPED_INTERNALS && !CILICA_VERIFY_BORROW_LIFETIMES
        && !CILICA_VERIFY_INTERNAL_REFERENCES && context.depth != 0u && context.depth < 64u
        && context.pending == 0u && context.exception == nullptr && !context.instance.marking
        && receiver != nullptr && receiver->type == &t_{core}_020000d9_g_{generic}_descriptor;
}}
static std::int32_t local_lookup(Context& context, Object* receiver, Object* key,
    std::uint32_t& local, std::uint64_t token,
    std::int32_t (*callee)(Context&, const LocationArgument*) noexcept) noexcept {{
    if (local_lookup_ready(context, receiver) && callee == &{target}
        && internal_reference_owned(context.instance, key)
        && (key == nullptr || t_{core}_0200008a_accepts(key->type))) {{
        std::uint32_t staged = local, result = 0;
        if ({target}_kernel(context, receiver, key, &staged, result)) {{
            local = staged; return std::bit_cast<std::int32_t>(result);
        }}
    }}
    const auto location = frame_location(context, token, LocationOrigin::frame_integer, 2, false, LocationNarrow::none);
    if (location.status != LocationStatus::complete) {{ context.pending = 6u; return 0; }}
    LocationArgument args[3]{{}}; args[0].reference=receiver; args[1].reference=key; args[2].location=location.value;
    return callee(context,args);
}}
'''
    return s[:a]+helper+body+s[b:]

variants={'inline_fields':inline_fields(base), 'local_out':local_out(base),
    'pool_inline_out':local_out(inline_fields(string_pool(base)))}
# Reuse the compile/verify/rotate timing body; benchmark against the original and
# previous strongest candidate in the same timing batch.
runner=(out/'experiments.py').read_text().split("provenance = ",1)[1]
runner="provenance = "+runner
runner=runner.replace("names = ['baseline', *variants]", "names = ['baseline', 'string_pool', *variants]")
runner=runner.replace("'experiment-provenance.json'", "'followup-provenance.json'").replace("f'experiment-", "f'followup-").replace("'experiment-summary.json'", "'followup-summary.json'")
exec(compile(runner,'followup-runner','exec'))
