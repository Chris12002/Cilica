"""Experimental per-valid_roots prefix memo; never a second ownership authority.

Transforms the pinned benchmark only. Ordinary owns, marking and allocation
publication remain authoritative linked-list operations. Not production code.
"""
from pathlib import Path
import argparse
import hashlib
from transform import replace_once

SOURCE_SHA = '51d73790b77e9bd83d330785bcc12f975153840a9e8d37aec9462719083d26d7'

MEMBERS = r'''
    // Separate experimental native scratch budget; managed limits are unchanged.
    static constexpr std::size_t ownership_slot_cap = 8192;
    static constexpr std::size_t ownership_byte_cap = 65536;
    const Object** ownership_scratch = nullptr;
    std::size_t ownership_capacity = 0, ownership_bytes = 0, ownership_peak = 0;
    std::size_t ownership_limit = ownership_byte_cap, total_memory_peak = 0;
    bool ownership_active = false;
#ifdef CILICA_PREFIX_TEST
    bool ownership_fail_allocation = false;
    void (*ownership_allocate_hook)(Instance&) noexcept = nullptr;
#endif
    std::size_t total_memory() const noexcept {
        const auto max = std::numeric_limits<std::size_t>::max();
        return bytes > max - ownership_bytes ? max : bytes + ownership_bytes;
    }
    void note_memory() noexcept {
        if (total_memory() > total_memory_peak) total_memory_peak = total_memory();
    }
    void release_ownership_scratch() noexcept {
        delete[] ownership_scratch;
        ownership_scratch = nullptr; ownership_capacity = 0; ownership_bytes = 0;
    }
    std::size_t desired_ownership_capacity() const noexcept {
        // Clamp before arithmetic, including a malicious or overflowing limit/count.
        auto slots = ownership_limit / sizeof(const Object*);
        if (slots > ownership_slot_cap) slots = ownership_slot_cap;
        if (slots > ownership_byte_cap / sizeof(const Object*)) slots = ownership_byte_cap / sizeof(const Object*);
        std::size_t cap = 1;
        while (cap <= slots / 2) cap *= 2;
        if (slots == 0 || head == nullptr) cap = 0;
        std::size_t desired = 1;
        while (desired < cap && desired / 2 < count) desired *= 2;
        if (desired < cap) cap = desired;
        return cap;
    }
    void prepare_ownership_scratch() noexcept {
        auto cap = desired_ownership_capacity();
        if (ownership_capacity != cap) {
            // Free first: old and new capacity never overlap the scratch budget.
            release_ownership_scratch();
            if (cap != 0) {
#ifdef CILICA_PREFIX_TEST
                if (ownership_allocate_hook != nullptr) ownership_allocate_hook(*this);
                if (ownership_fail_allocation) return;
                cap = desired_ownership_capacity(); // Revalidate after test allocator callback.
                if (cap == 0) return;
#endif
                ownership_scratch = new (std::nothrow) const Object*[cap];
                if (ownership_scratch != nullptr) {
                    ownership_capacity = cap; ownership_bytes = cap * sizeof(const Object*);
                    if (ownership_bytes > ownership_peak) ownership_peak = ownership_bytes;
                    note_memory();
                }
            }
        }
        for (std::size_t i = 0; i < ownership_capacity; ++i) ownership_scratch[i] = nullptr;
    }
'''

BATCH = r'''
// No membership result escapes one synchronous root-validation call. The list is
// immutable during this scope. A later call starts with an empty prefix again.
struct OwnershipBatch {
    Instance& heap;
    const bool exclusive;
    const Object* paused = nullptr;
    bool begun = false, complete = false, fallback = false;
    explicit OwnershipBatch(Instance& value) noexcept
        : heap(value), exclusive(!value.ownership_active) {
        if (exclusive) {
            heap.ownership_active = true; // Guard BEFORE any allocator callback.
            heap.prepare_ownership_scratch();
        }
        fallback = !exclusive || heap.ownership_capacity == 0;
    }
    OwnershipBatch(const OwnershipBatch&) = delete;
    OwnershipBatch& operator=(const OwnershipBatch&) = delete;
    ~OwnershipBatch() noexcept { if (exclusive) heap.ownership_active = false; }
    static std::size_t hash(const Object* p) noexcept {
        // Width-independent unsigned shifts; valid on both 32- and 64-bit uintptr_t.
        auto n = reinterpret_cast<std::uintptr_t>(p) >> 3;
        n ^= n >> 11; n *= std::uintptr_t{2654435761u}; n ^= n >> 13;
        return static_cast<std::size_t>(n);
    }
    // Returns either the matching slot, an empty slot, or capacity if full.
    std::size_t slot(const Object* p) const noexcept {
        const auto cap = heap.ownership_capacity;
        auto h = hash(p) & (cap - 1);
        for (std::size_t n = 0; n < cap; ++n, h = (h + 1) & (cap - 1))
            if (heap.ownership_scratch[h] == p || heap.ownership_scratch[h] == nullptr) return h;
        return cap;
    }
    bool search(const Object* target) const noexcept {
        // Brent cycle detection: no lookahead loads. Compare current to target
        // before examining its link. A missing cyclic root rejects rather than
        // reproducing the baseline's nontermination.
        const Object* p = heap.head;
        const Object* anchor = p;
        std::size_t length = 0, power = 1;
        while (p != nullptr) {
            if (p == target) return true;
            if (length == power) {
                anchor = p; length = 0;
                if (power <= std::numeric_limits<std::size_t>::max() / 2) power *= 2;
            }
            p = p->next;
            if (p == target && p != nullptr) return true;
            if (p == anchor) return false;
            if (length == std::numeric_limits<std::size_t>::max()) return false;
            ++length;
        }
        return false;
    }
    bool contains(const Object* target) noexcept {
        if (target == nullptr) return true;
        if (fallback) return search(target);
        auto query = slot(target);
        if (query != heap.ownership_capacity && heap.ownership_scratch[query] == target) return true;
        if (complete) return false;
        // Full table restarts without consuming the saved frontier link.
        if (query == heap.ownership_capacity) { fallback = true; return search(target); }
        const Object* p = begun ? paused->next : heap.head;
        begun = true;
        while (p != nullptr) {
            auto place = slot(p);
            if (place == heap.ownership_capacity) { fallback = true; return search(target); }
            if (heap.ownership_scratch[place] == p) return false; // Cyclic miss.
            heap.ownership_scratch[place] = p;
            paused = p;
            if (p == target) return true; // Do not read p->next here.
            p = p->next;
        }
        complete = true;
        return false;
    }
};
'''


def transform(text):
    text = replace_once(text, '    ~Instance() noexcept {', MEMBERS + '\n    ~Instance() noexcept {\n        release_ownership_scratch();')
    start = text.index('[[maybe_unused]] static bool valid_roots(const Context& context) noexcept {')
    end = text.index('\n[[maybe_unused]] static bool collect_for_allocation', start)
    original = text[start:end]
    updated = original.replace('noexcept {', 'noexcept {\n    OwnershipBatch batch(context.instance);', 1).replace('context.instance.owns(', 'batch.contains(')
    text = text[:start] + BATCH + updated + text[end:]
    text = replace_once(text, '        value->type->destroy(value);\n    }\n    return report;\n}',
                        '        value->type->destroy(value);\n    }\n    heap.release_ownership_scratch();\n    return report;\n}')
    # Accounting observes actual managed and scratch capacity; no membership hook.
    for charge, expected in [('charged', 2), ('sizeof(T)', 1)]:
        old = 'heap.head = &value->header; heap.bytes += ' + charge + '; ++heap.count;'
        assert text.count(old) == expected
        text = text.replace(old, old + ' heap.note_memory();')
    for charge, expected in [('charged', 2), ('bytes', 1)]:
        old = 'context.instance.bytes += ' + charge + ';'
        assert text.count(old) == expected
        text = text.replace(old, old + ' context.instance.note_memory();')
    return text


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('source', type=Path); parser.add_argument('output', type=Path)
    args = parser.parse_args()
    assert hashlib.sha256(args.source.read_bytes()).hexdigest() == SOURCE_SHA
    args.output.write_text(transform(args.source.read_text()))
