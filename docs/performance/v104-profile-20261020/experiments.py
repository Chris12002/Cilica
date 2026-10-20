"""Generated-code experiments only; none are production compiler changes."""
from pathlib import Path
import subprocess, json, statistics, hashlib, re

out = Path(__file__).resolve().parent
base = (out / 'baseline.cpp').read_text()
expected = json.loads((out / 'baseline-verify.json').read_text())

def replace(s, old, new):
    assert s.count(old) == 1, (old[:100], s.count(old))
    return s.replace(old, new)

def field_offsets(s):
    return replace(s, 'return value->type->accessors[index](value);',
        'return reinterpret_cast<std::byte*>(value) + value->type->fields[index];')

def string_pool(s):
    # All StringObject headers use instance-owned slabs; buffers still use new[].
    # Managed quotas, collection, roots, identity and string contents stay intact.
    # Retained slab bytes are EXTRA native memory, deliberately measured separately.
    s = replace(s, '[[maybe_unused]] static void destroy_string(Object* value) noexcept {',
        '''struct StringSlab { StringSlab* next = nullptr; StringObject slots[256]{}; };
static void recycle_string(StringObject*) noexcept;
[[maybe_unused]] static void destroy_string(Object* value) noexcept {''')
    s = replace(s, '    delete text;', '    recycle_string(text);')
    s = replace(s, '    StringObject* literals = nullptr;',
        '''    StringSlab* string_slabs = nullptr;
    StringObject* free_strings = nullptr;
    std::size_t string_slab_count = 0;
    StringObject* literals = nullptr;''')
    s = replace(s, '        while (head != nullptr) { Object* p = head; head = p->next; p->type->destroy(p); }',
        '''        while (head != nullptr) { Object* p = head; head = p->next; p->type->destroy(p); }
        while (string_slabs != nullptr) { auto* slab = string_slabs; string_slabs = slab->next; delete slab; }''')
    marker = '// Only generated execution may use this shortcut.'
    s = replace(s, marker, '''static void recycle_string(StringObject* value) noexcept {
    auto* heap = value->header.owner;
    value->literal_next = heap->free_strings; heap->free_strings = value;
}
static StringObject* acquire_string(Instance& heap) noexcept {
    if (heap.free_strings == nullptr) {
        if (heap.string_slab_count == 16u) return nullptr;
        auto* slab = new (std::nothrow) StringSlab{};
        if (slab == nullptr) return nullptr;
        slab->next = heap.string_slabs; heap.string_slabs = slab; ++heap.string_slab_count;
        for (auto& slot : slab->slots) { slot.literal_next = heap.free_strings; heap.free_strings = &slot; }
    }
    auto* value = heap.free_strings; heap.free_strings = value->literal_next;
    *value = StringObject{}; value->header.owner = &heap; return value;
}
''' + marker)
    s = replace(s, 'auto* value = new (std::nothrow) StringObject{};',
        'auto* value = acquire_string(heap);')
    s = replace(s, 'if (length != 0 && units == nullptr) { delete value;',
        'if (length != 0 && units == nullptr) { recycle_string(value);')
    return s

def array_entry_ablation(s):
    # Upper bound: retain ownership/type/null/bounds, omit full storage-shape checks
    # only at typed array-interface entry. A future compiler proof is still needed.
    pattern = r'(\[\[maybe_unused\]\] static [^\n]+ cilica_m_\w+_0600007[bc]_g_\w+_typed\([^\n]+\) noexcept \{)([\s\S]*?)(\n\})'
    def edit(m):
        assert 'valid_array_storage(' in m[2]
        return m[1] + m[2].replace('!valid_array_storage(p0)', 'false') + m[3]
    s, count = re.subn(pattern, edit, s)
    assert count >= 2, count
    return s

variants = {
    'field_offsets': field_offsets(base),
    'string_pool': string_pool(base),
    'array_entry_ablation': array_entry_ablation(base),
    'pool_fields': field_offsets(string_pool(base)),
}
provenance = {'diagnostic_only': True, 'revision': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(), 'variants': {}}
for name, source in variants.items():
    p = out / (name + '.cpp'); p.write_text(source)
    cmd = ['clang++', '-std=c++20', '-O2', '-DNDEBUG', '-fno-exceptions', '-fno-rtti', '-fstack-usage', str(p), '-o', str(out / name)]
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=240)
    (out / (name + '-build.log')).write_text(r.stdout + r.stderr); r.check_returncode()
    raw = subprocess.check_output([str(out / name), 'verify', '0'], text=True, timeout=120)
    assert json.loads(raw) == expected, name
    (out / (name + '-verify.json')).write_text(raw)
    provenance['variants'][name] = {'command': cmd, 'cpp_sha256': hashlib.sha256(source.encode()).hexdigest(),
        'max_frame_bytes': max(int(l.split('\t')[-2]) for l in (out / (name + '.su')).read_text().splitlines())}
    print('verified', name, flush=True)
(out / 'experiment-provenance.json').write_text(json.dumps(provenance, indent=2))
# No compilation, sampling or instrumented runs overlap timing.
names = ['baseline', *variants]; timings = {n: [] for n in names}
for repeat in range(3):
    order = names[repeat:] + names[:repeat]
    if repeat == 1: order.reverse()
    for name in order:
        raw = subprocess.check_output([str(out / name), 'measure', '0', '256'], text=True, timeout=60)
        (out / f'experiment-{name}-{repeat}.jsonl').write_text(raw)
        rows = [json.loads(l) for l in raw.splitlines()]
        assert len(rows) == 8 and all(r['checksum'] == 6146 * r['iterations'] for r in rows[1:])
        timings[name] += [r['ns'] / r['iterations'] for r in rows[1:]]
summary = {n: {'median_ns': statistics.median(v), 'min_ns': min(v), 'max_ns': max(v), 'samples': len(v),
    'time_reduction': 1 - statistics.median(v) / statistics.median(timings['baseline'])} for n, v in timings.items()}
(out / 'experiment-summary.json').write_text(json.dumps(summary, indent=2))
print(json.dumps(summary, indent=2), flush=True)
