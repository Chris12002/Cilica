from pathlib import Path
exec(compile((Path(__file__).parent / 'experiment-functions.py').read_text(), 'experiment-functions.py', 'exec'))
for name in ['baseline', 'string_pool']:
    s = (out / 'counted.cpp').read_text()
    if name == 'string_pool': s = string_pool(s)
    s = 'static unsigned long long gc_calls=0,gc_live=0,gc_dead=0,peak_objects=0,peak_bytes=0,field_calls=0,slab_news=0;\n' + s
    s = replace(s, 'static MarkReport collect(Context& context, MarkLimits limits = {}) noexcept {',
        'static MarkReport collect(Context& context, MarkLimits limits = {}) noexcept { ++gc_calls;')
    s = replace(s, 'if (report.status != MarkStatus::complete) return report;\n    Object** link',
        'if (report.status != MarkStatus::complete) return report;\n    gc_live+=report.reachable;gc_dead+=report.unreachable;\n    Object** link')
    s = replace(s, 'static bool allocation_room(Context& context, std::size_t bytes, bool& attempted) noexcept {',
        '''static bool allocation_room(Context& context, std::size_t bytes, bool& attempted) noexcept {
    peak_objects=std::max(peak_objects,static_cast<unsigned long long>(context.instance.count));
    peak_bytes=std::max(peak_bytes,static_cast<unsigned long long>(context.instance.bytes));''')
    s = replace(s, 'return value->type->accessors[index](value);',
        '++field_calls; return value->type->accessors[index](value);')
    s = replace(s, 'for(auto& n:method_counts)n=0;',
        'gc_calls=gc_live=gc_dead=peak_objects=peak_bytes=field_calls=slab_news=0;for(auto& n:method_counts)n=0;')
    if name == 'string_pool':
        s = replace(s, 'auto* slab = new (std::nothrow) StringSlab{};', '++slab_news; auto* slab = new (std::nothrow) StringSlab{};')
    line = 'std::fprintf(stderr,"metrics gc_calls=%llu gc_live=%llu gc_dead=%llu peak_objects=%llu peak_bytes=%llu field_calls=%llu string_size=%zu slab_news=%llu\\n",gc_calls,gc_live,gc_dead,peak_objects,peak_bytes,field_calls,sizeof(StringObject),slab_news);\n'
    if name == 'string_pool':
        line += 'std::fprintf(stderr,"pool slabs=%zu reserved_native_bytes=%zu max_native_bytes=%zu\\n",heap.string_slab_count,heap.string_slab_count*sizeof(StringSlab),16*sizeof(StringSlab));\n'
    s = replace(s, 'std::fprintf(stderr,"extra', line + 'std::fprintf(stderr,"extra')
    cpp = out / (name + '-metrics.cpp'); cpp.write_text(s)
    exe = out / (name + '-metrics')
    cmd = ['clang++', '-std=c++20', '-O2', '-DNDEBUG', '-fno-exceptions', '-fno-rtti', str(cpp), '-o', str(exe)]
    subprocess.run(cmd, check=True, timeout=240)
    r = subprocess.run([str(exe), 'cold', '0'], capture_output=True, text=True, check=True, timeout=60)
    assert json.loads(r.stdout)['checksum'] == 6146 * 256
    (out / (name + '-metrics.txt')).write_text(r.stderr)
    print(name, [l for l in r.stderr.splitlines() if l.startswith(('metrics', 'pool'))], flush=True)
