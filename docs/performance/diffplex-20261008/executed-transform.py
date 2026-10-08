"""Instrument or experimentally index one pinned emitted translation unit.

Never removes validation. The index is an experiment for runtime-owned mutation
paths, not production support for independently corrupted allocation inventories.
"""
from pathlib import Path
import argparse


def replace_once(text, old, new):
    assert text.count(old) == 1, (old[:80], text.count(old))
    return text.replace(old, new)


def instrument(text):
    text = '#include <chrono>\n' + text
    text = replace_once(text, 'struct Instance {', '''
struct PerfCounters {
    unsigned long long calls[3]{}, nodes[3]{}, ns[3]{}, scopes[3]{};
    unsigned phase = 0;
};
static PerfCounters perf;
struct PerfScope {
    unsigned phase, previous;
    std::chrono::steady_clock::time_point start;
    explicit PerfScope(unsigned p) : phase(p), previous(perf.phase), start(std::chrono::steady_clock::now()) {
        perf.phase=p; ++perf.scopes[p];
    }
    ~PerfScope() { perf.ns[phase] += std::chrono::duration_cast<std::chrono::nanoseconds>(std::chrono::steady_clock::now()-start).count(); perf.phase=previous; }
};
struct Instance {''')
    text = replace_once(text, 'bool owns(const Object* value) const noexcept {',
                        'bool owns(const Object* value) const noexcept {\n ++perf.calls[perf.phase];')
    text = replace_once(text,
        'for (const Object* p = head; p != nullptr; p = p->next) if (p == value) return true;',
        'for (const Object* p = head; p != nullptr; p = p->next) { ++perf.nodes[perf.phase]; if (p == value) return true; }')
    text = replace_once(text, 'static bool valid_roots(const Context& context) noexcept {',
                        'static bool valid_roots(const Context& context) noexcept { PerfScope timing(1);')
    text = replace_once(text, 'static MarkReport collect(Context& context, MarkLimits limits = {}) noexcept {',
                        'static MarkReport collect(Context& context, MarkLimits limits = {}) noexcept { PerfScope timing(2);')
    point = '    if(std::strcmp(argv[1],"verify")==0) {'
    text = replace_once(text, point, '''
    if(std::strcmp(argv[1],"profile")==0) {
        bool reset = argc>3 && std::strcmp(argv[3],"collect-each")==0;
        for(int i=0;i<(reset?8:20);++i) {
            perf={}; auto before=heap.count; auto start=clock::now(); auto checksum=call(c,work,1);
            auto ns=std::chrono::duration_cast<std::chrono::nanoseconds>(clock::now()-start).count();
            std::printf("{\\"sample\\":%d,\\"ns\\":%lld,\\"checksum\\":%d,\\"objects_before\\":%zu,\\"objects_after\\":%zu,\\"bytes_after\\":%zu,\\"calls\\":[%llu,%llu,%llu],\\"nodes\\":[%llu,%llu,%llu],\\"root_ns\\":%llu,\\"gc_ns\\":%llu,\\"root_scopes\\":%llu,\\"collections\\":%llu}",i,(long long)ns,checksum,before,heap.count,heap.bytes,perf.calls[0],perf.calls[1],perf.calls[2],perf.nodes[0],perf.nodes[1],perf.nodes[2],perf.ns[1],perf.ns[2],perf.scopes[1],perf.scopes[2]);
            std::printf("\\n");
            if(reset) { auto report=collect(c); if(report.status!=MarkStatus::complete)return 3; std::printf("{\\"survivors\\":%zu,\\"survivor_bytes\\":%zu}\\n",heap.count,heap.bytes); }
        }
        return 0;
    }
''' + point)
    return text


INDEX = '''
    // Experimental fixed-capacity exact pointer index. No unknown pointer read.
    static constexpr std::size_t index_size = 8192;
    const Object* ownership_index[index_size]{};
    bool index_complete = true;
    static std::size_t address_hash(const Object* p) noexcept {
        auto n = reinterpret_cast<std::uintptr_t>(p) >> 4;
        n ^= n >> 23; n *= std::uintptr_t{0x2127599bf4325c37ULL}; n ^= n >> 47;
        return n & (index_size-1);
    }
    void remember(const Object* p) noexcept {
        auto h = address_hash(p);
        for(std::size_t n=0;n<index_size;++n,h=(h+1)&(index_size-1)) {
            if(ownership_index[h]==nullptr || ownership_index[h]==p) { ownership_index[h]=p; return; }
        }
        index_complete=false;
    }
    void rebuild_index() noexcept {
        for(auto& p:ownership_index)p=nullptr;
        index_complete=true;
        for(const Object* p=head;p!=nullptr;p=p->next)remember(p);
    }
    bool owns(const Object* value) const noexcept {
        if(value==nullptr)return true;
        if(!index_complete) {
            for(const Object* p=head;p!=nullptr;p=p->next)if(p==value)return true;
            return false;
        }
        auto h=address_hash(value);
        for(std::size_t n=0;n<index_size;++n,h=(h+1)&(index_size-1)) {
            if(ownership_index[h]==value)return true;
            if(ownership_index[h]==nullptr)return false;
        }
        return false;
    }
'''


def indexed(text):
    start=text.index('    bool owns(const Object* value) const noexcept {')
    end=text.index('    ~Instance() noexcept {', start)
    text=text[:start]+INDEX+text[end:]
    for charge,expected in [('charged',2),('sizeof(T)',1)]:
        old='heap.head = &value->header; heap.bytes += '+charge+'; ++heap.count;'
        assert text.count(old)==expected
        text=text.replace(old,old+' heap.remember(heap.head);')
    text=replace_once(text, '''        value->type->destroy(value);
    }
    return report;
}''', '''        value->type->destroy(value);
    }
    heap.rebuild_index();
    return report;
}''')
    return '#define CILICA_PERF_INDEX 1\n'+text


def noop(text):
    text=replace_once(text, '    Instance heap;Context c(heap);',
        '    if(std::strcmp(argv[1],"noop")==0) { std::puts("0"); return 0; }\n    Instance heap;Context c(heap);')
    point='    if(std::strcmp(argv[1],"verify")==0) {'
    return replace_once(text,point,'''
    if(std::strcmp(argv[1],"micro")==0) {
        // Independent inventory lookup reducer: no DiffPlex or managed work.
        Object nodes[4096]{}; Object foreign{};
        for(int size : {1,64,512,2048,4096}) {
            heap.head=nullptr; heap.count=0;
            for(int i=0;i<size;++i) { nodes[i].next=heap.head; heap.head=&nodes[i]; ++heap.count; }
#ifdef CILICA_PERF_INDEX
            heap.rebuild_index();
#endif
            if(!heap.owns(nullptr) || heap.owns(&foreign) || heap.owns(reinterpret_cast<Object*>(std::uintptr_t{1})))std::abort();
            for(int repeat=0;repeat<3;++repeat) {
                unsigned hits=0; auto start=clock::now();
                for(unsigned i=0;i<32768;++i)hits+=heap.owns(&nodes[(i*17u)%unsigned(size)])?1u:0u;
                auto ns=std::chrono::duration_cast<std::chrono::nanoseconds>(clock::now()-start).count();
                if(hits!=32768)std::abort();
                std::printf("{\\"objects\\":%d,\\"queries\\":32768,\\"hits\\":%u,\\"ns\\":%lld}\\n",size,hits,(long long)ns);
            }
            // Remove half, ensure former members and foreign pointers reject.
            heap.head=&nodes[size/2]; nodes[size/2].next=nullptr; heap.count=1;
#ifdef CILICA_PERF_INDEX
            heap.rebuild_index();
#endif
            for(int i=0;i<size;++i)if(heap.owns(&nodes[i])!=(i==size/2))std::abort();
        }
        heap.head=nullptr; heap.count=0; return 0;
    }
'''+point)


if __name__ == '__main__':
    p=argparse.ArgumentParser();p.add_argument('source',type=Path);p.add_argument('output',type=Path);p.add_argument('mode',choices=['clean','profile','index']);a=p.parse_args()
    text=noop(a.source.read_text())
    if a.mode=='profile':text=instrument(text)
    if a.mode=='index':text=indexed(text)
    a.output.write_text(text)
