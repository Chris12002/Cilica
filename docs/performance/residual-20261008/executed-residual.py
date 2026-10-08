"""Create separate indexed diagnostic binaries; not clean timing or production code."""
from pathlib import Path
import argparse,re
from transform import indexed,noop,replace_once

COUNTERS='''
struct ResidualCounters { unsigned long long root_ns=0,gc_ns=0,allocation_ns=0,root_calls=0,slots=0,nonnull=0,frames=0,frame_bytes=0,steps=0; };
static ResidualCounters residual;
struct ResidualTimer {
    unsigned long long& total; std::chrono::steady_clock::time_point start;
    explicit ResidualTimer(unsigned long long& n):total(n),start(std::chrono::steady_clock::now()){}
    ~ResidualTimer(){total+=std::chrono::duration_cast<std::chrono::nanoseconds>(std::chrono::steady_clock::now()-start).count();}
};
'''

def scopes(text):
    text='#include <chrono>\n'+text
    text=replace_once(text,'struct Instance {',COUNTERS+'\nstruct Instance {')
    text=replace_once(text,'static bool valid_roots(const Context& context) noexcept {',
        'static bool valid_roots(const Context& context) noexcept { ResidualTimer timer(residual.root_ns); ++residual.root_calls;')
    text=replace_once(text,'static MarkReport collect(Context& context, MarkLimits limits = {}) noexcept {',
        'static MarkReport collect(Context& context, MarkLimits limits = {}) noexcept { ResidualTimer timer(residual.gc_ns);')
    for prefix in ['template<class T> static Object* allocate(', '[[maybe_unused, gnu::noinline]] static Object* allocate_array(', '[[maybe_unused, gnu::noinline]] static Object* allocate_string(']:
        start=text.index(prefix);end=text.index(' noexcept {',start)+len(' noexcept {')
        text=text[:end]+' ResidualTimer timer(residual.allocation_ns);'+text[end:]
    text=replace_once(text,'if (!context.instance.owns(f->slots[i])) return false;',
        '{ ++residual.slots; residual.nonnull += f->slots[i]!=nullptr; if (!context.instance.owns(f->slots[i])) return false; }')
    text=replace_once(text,'explicit Storage(Context& c) noexcept : context(c), mark(c.used) {',
        'explicit Storage(Context& c) noexcept : context(c), mark(c.used) { ++residual.frames; residual.frame_bytes+=sizeof(T);')
    text=text.replace('while (step != frame_done) {','while (step != frame_done) { ++residual.steps;')
    point='    if(std::strcmp(argv[1],"verify")==0) {'
    return replace_once(text,point,'''
    if(std::strcmp(argv[1],"residual")==0) {
        (void)call(c,work,8);
        for(int i=0;i<21;++i) {
            residual={}; auto start=clock::now(); auto checksum=call(c,work,16);
            auto ns=std::chrono::duration_cast<std::chrono::nanoseconds>(clock::now()-start).count();
            if(checksum!=98336)std::abort();
            std::printf("{\\"sample\\":%d,\\"iterations\\":16,\\"ns\\":%lld,\\"root_ns\\":%llu,\\"gc_ns\\":%llu,\\"allocation_inclusive_ns\\":%llu,\\"root_calls\\":%llu,\\"root_slots\\":%llu,\\"nonnull_slots\\":%llu,\\"frames\\":%llu,\\"frame_bytes\\":%llu,\\"steps\\":%llu}\\n",i,(long long)ns,residual.root_ns,residual.gc_ns,residual.allocation_ns,residual.root_calls,residual.slots,residual.nonnull,residual.frames,residual.frame_bytes,residual.steps);
        }
        return 0;
    }
'''+point)


SAMPLER='''
#include <signal.h>
#include <sys/time.h>
#include <ucontext.h>
#include <dlfcn.h>
static volatile sig_atomic_t sample_count=0;
static std::uintptr_t sample_pcs[65536]{};
static void sample_signal(int, siginfo_t*, void* opaque) {
    auto* u=static_cast<ucontext_t*>(opaque);
    sig_atomic_t i=sample_count;
    if(i<65536) { sample_pcs[i]=u->uc_mcontext.pc; sample_count=i+1; }
}
'''

def sampled(text):
    # Linux ARM64 diagnostic target is explicit. No claim for other platforms.
    text='#include <cstdint>\n'+SAMPLER+text
    point='    if(std::strcmp(argv[1],"verify")==0) {'
    return replace_once(text,point,'''
    if(std::strcmp(argv[1],"sample")==0) {
        (void)call(c,work,8);
        struct sigaction sa{}; sa.sa_sigaction=sample_signal;sa.sa_flags=SA_SIGINFO;sigemptyset(&sa.sa_mask);
        if(sigaction(SIGPROF,&sa,nullptr)!=0)return 4;
        itimerval timer{};timer.it_interval.tv_usec=1000;timer.it_value=timer.it_interval;
        if(setitimer(ITIMER_PROF,&timer,nullptr)!=0)return 5;
        auto checksum=call(c,work,16384);
        timer={};if(setitimer(ITIMER_PROF,&timer,nullptr)!=0)return 6;
        if(checksum!=100696064)return 7;
        std::printf("{\\"iterations\\":16384,\\"checksum\\":%d,\\"samples\\":%d}\\n",checksum,int(sample_count));
        for(sig_atomic_t i=0;i<sample_count;++i) {
            Dl_info info{};dladdr(reinterpret_cast<void*>(sample_pcs[i]),&info);
            std::printf("%llx %s %s\\n",(unsigned long long)sample_pcs[i],info.dli_fname?info.dli_fname:"?",info.dli_sname?info.dli_sname:"?");
        }
        return 0;
    }
'''+point)


BOUNDARY='''
    if(std::strcmp(argv[1],"boundary")==0) {
        std::printf("{\\"instance_size\\":%zu,\\"initial_charged_bytes\\":%zu,\\"managed_limit\\":%zu}\\n",sizeof(Instance),heap.bytes,heap.limit);
        (void)call(c,work,1);
        Object* orphan=heap.head; auto bytes=heap.bytes;auto count=heap.count;
        heap.head=orphan->next;heap.bytes-=orphan->allocation_bytes;--heap.count;
        Frame frame;RootScope roots(c,frame,&orphan,1);
        auto report=mark_reachable(c);
        std::printf("{\\"detached_root_mark_status\\":%d,\\"orphan_marked\\":%d,\\"owns_orphan\\":%d}\\n",int(report.status),int(orphan->marked),int(heap.owns(orphan)));
        // Restore before teardown: the test deliberately never frees an orphan.
        heap.head=orphan;heap.bytes=bytes;heap.count=count;orphan->marked=false;orphan->mark_next=nullptr;
        return 0;
    }
'''

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('source',type=Path);p.add_argument('output',type=Path);p.add_argument('mode',choices=['scopes','sample','boundary-baseline','boundary-index']);a=p.parse_args()
    text=a.source.read_text() if a.mode.startswith('boundary-') else noop(a.source.read_text())
    if a.mode!='boundary-baseline':text=indexed(text)
    if a.mode=='scopes':text=scopes(text)
    elif a.mode=='sample':text=sampled(text)
    else:text=replace_once(text,'    if(std::strcmp(argv[1],"verify")==0) {',BOUNDARY+'    if(std::strcmp(argv[1],"verify")==0) {')
    a.output.write_text(text)
