from pathlib import Path
import subprocess,json,statistics,os,hashlib
out=Path(__file__).resolve().parent;expected=json.loads((out.parent/'v111-profile-20261020/baseline-verify.json').read_text())
base=(out/'v114.cpp').read_text().replace('int main(int argc,char** argv)','int benchmark_main(int argc,char** argv)')
end=base.rfind('}');base=base[:end]+'return 0;\n'+base[end:]
base+='''
#include <pthread.h>
#include <cassert>

[[gnu::noinline]] static void collector_invariants() {
    for (unsigned pass=0; pass<32; ++pass) {
        Instance heap; Context c(heap); const std::uint16_t unit=65;
        Object* root=allocate_string(c,*exception_string_type(),&unit,1,nullptr,0);
        Frame frame; RootScope roots(c,frame,&root,1);
        Object* dead=allocate_string(c,*exception_string_type(),&unit,1,nullptr,0);
        assert(root && dead && heap.count==2 && c.pending==0);
        auto clean=[&]() {
            assert(!heap.marking && heap.membership_size==0);
            for (auto* p=heap.head;p;p=p->next) assert(!p->marked && p->mark_next==nullptr);
        };
        auto stopped=[](void*,Object*,bool) noexcept { return false; };
        assert(mark_reachable(c,{},stopped,nullptr).status==MarkStatus::stopped); clean();
        assert(mark_reachable(c,{4096,0}).status==MarkStatus::limit); clean();
        assert(collect(c,{1,262144}).status==MarkStatus::limit); clean();
        auto* saved=root; root=reinterpret_cast<Object*>(std::uintptr_t{1});
        assert(collect(c).status==MarkStatus::invalid); root=saved; clean();
        auto* owner=dead->owner; dead->owner=nullptr;
        assert(collect(c).status==MarkStatus::invalid); dead->owner=owner; clean();
        auto busy=[](void* opaque,Object*,bool) noexcept {
            auto& context=*static_cast<Context*>(opaque);
            assert(context.instance.membership_size==0);
            assert(collect(context).status==MarkStatus::busy);
            return true;
        };
        assert(mark_reachable(c,{},busy,&c).status==MarkStatus::complete); clean();
        auto report=collect(c); assert(report.status==MarkStatus::complete && report.reachable==1 && report.unreachable==1 && heap.count==1); clean();
        root=nullptr; assert(collect(c).status==MarkStatus::complete && heap.count==0); clean();
        assert(collect(c).status==MarkStatus::complete && heap.count==0); clean();
    }
}

struct RunArguments { int argc; char** argv; int status; };
static void* run(void* p) {
#if defined(__APPLE__)
 assert(pthread_get_stacksize_np(pthread_self()) >= 512u*1024u);
 assert(pthread_get_stacksize_np(pthread_self()) <= 576u*1024u);
#endif
 auto* arguments=static_cast<RunArguments*>(p);
 collector_invariants();
 arguments->status=benchmark_main(arguments->argc,arguments->argv);return nullptr;
}
int main(int argc,char** argv) {
 RunArguments arguments{argc,argv,99};pthread_attr_t attr;pthread_t thread;
 assert(pthread_attr_init(&attr)==0);assert(pthread_attr_setstacksize(&attr,512u*1024u)==0);
 assert(pthread_create(&thread,&attr,run,&arguments)==0);assert(pthread_attr_destroy(&attr)==0);
 assert(pthread_join(thread,nullptr)==0);return arguments.status;
}
'''
results={}
for name,flags in [('release',['-O2','-Wframe-larger-than=4096']),('optimized_sanitized',['-O1','-Wframe-larger-than=16384','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-DCILICA_VERIFY_INTERNAL_REFERENCES=0','-DCILICA_VERIFY_BORROW_LIFETIMES=0']),('sanitized',['-O1','-Wframe-larger-than=16384','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-DCILICA_VERIFY_INTERNAL_REFERENCES=1','-DCILICA_VERIFY_BORROW_LIFETIMES=1'])]:
 s=base if name=='release' else base.replace('Instance heap;Context c(heap);','Instance heap;heap.collect_before_allocation=true;Context c(heap);')
 cpp=out/(name+'-acceptance.cpp');cpp.write_text(s);exe=out/(name+'-acceptance')
 cmd=['clang++','-std=c++20','-pthread','-DCILICA_TYPED_INTERNALS=1','-DCILICA_DIRECT_CONTROL_FLOW=1','-fno-exceptions','-fno-rtti','-fstack-usage','-Wall','-Wextra','-Werror',*flags,str(cpp),'-o',str(exe)]
 r=subprocess.run(cmd,text=True,capture_output=True,timeout=240);(out/(name+'-acceptance-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 for work in [0,1]:
  expected_work=expected if work==0 else json.loads((out.parent/'v111-profile-20261020/dotnet-line-verify.json').read_text())
  r=subprocess.run([str(exe),'verify',str(work)],text=True,capture_output=True,timeout=180);(out/(name+f'-acceptance-work{work}.json')).write_text(r.stdout);(out/(name+f'-acceptance-work{work}.log')).write_text(r.stderr);r.check_returncode();assert json.loads(r.stdout)==expected_work and r.stderr==''
 results[name]={'command':cmd,'matches_all_values_both_workloads':True,'collector_invariant_iterations_per_run':32,'forced_gc':name!='release','cpp_sha256':hashlib.sha256(cpp.read_bytes()).hexdigest(),'max_stack_usage':max(int(l.split('\t')[-2]) for l in (out/(name+'-acceptance.su')).read_text().splitlines())};print('verified',name,flush=True)
(out/'acceptance-results.json').write_text(json.dumps(results,indent=2))
