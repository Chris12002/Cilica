from pathlib import Path
import subprocess,json,statistics,os,hashlib
out=Path(__file__).resolve().parent;expected=json.loads((out/'reconciled-verify.json').read_text())
base=(out/'final.cpp').read_text().replace('int main(int argc,char** argv)','int benchmark_main(int argc,char** argv)')
end=base.rfind('}');base=base[:end]+'return 0;\n'+base[end:]
base+='''
#include <pthread.h>
#include <cassert>
struct RunArguments { int argc; char** argv; int status; };
static void* run(void* p) {
#if defined(__APPLE__)
 assert(pthread_get_stacksize_np(pthread_self()) >= 512u*1024u);
 assert(pthread_get_stacksize_np(pthread_self()) <= 576u*1024u);
#endif
 auto* arguments=static_cast<RunArguments*>(p);
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
for name,flags in [('release',['-O2','-Wframe-larger-than=4096']),('sanitized',['-O1','-Wframe-larger-than=16384','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-DCILICA_VERIFY_INTERNAL_REFERENCES=1','-DCILICA_VERIFY_BORROW_LIFETIMES=1'])]:
 s=base if name=='release' else base.replace('Instance heap;Context c(heap);','Instance heap;heap.collect_before_allocation=true;Context c(heap);')
 cpp=out/(name+'-acceptance.cpp');cpp.write_text(s);exe=out/(name+'-acceptance')
 cmd=['clang++','-std=c++20','-pthread','-DCILICA_TYPED_INTERNALS=1','-DCILICA_DIRECT_CONTROL_FLOW=1','-fno-exceptions','-fno-rtti','-fstack-usage','-Wall','-Wextra','-Werror',*flags,str(cpp),'-o',str(exe)]
 r=subprocess.run(cmd,text=True,capture_output=True,timeout=240);(out/(name+'-acceptance-build.log')).write_text(r.stdout+r.stderr);r.check_returncode()
 r=subprocess.run([str(exe),'verify','0'],text=True,capture_output=True,timeout=180);(out/(name+'-acceptance.json')).write_text(r.stdout);(out/(name+'-acceptance.log')).write_text(r.stderr);r.check_returncode();assert json.loads(r.stdout)==expected and r.stderr==''
 results[name]={'command':cmd,'matches_all_values':True,'forced_gc':name=='sanitized','cpp_sha256':hashlib.sha256(cpp.read_bytes()).hexdigest(),'max_stack_usage':max(int(l.split('\t')[-2]) for l in (out/(name+'-acceptance.su')).read_text().splitlines())};print('verified',name,flush=True)
(out/'acceptance-results.json').write_text(json.dumps(results,indent=2))
host='/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs/host/DotNetHost.dll'
env={k:v for k,v in os.environ.items() if not k.startswith(('DOTNET_Tier','COMPlus_Tier','DOTNET_Jit','COMPlus_Jit','DOTNET_ReadyToRun','COMPlus_ReadyToRun'))};env.update(DOTNET_TieredCompilation='0',DOTNET_ReadyToRun='0')
values=[]
for n in range(3):
 raw=subprocess.check_output(['dotnet',host,'measure','0','16384'],text=True,env=env,timeout=60);(out/f'net-final-{n}.jsonl').write_text(raw);rows=[json.loads(l) for l in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:]);values+=[r['ns']/r['iterations'] for r in rows[1:]]
(out/'net-final-summary.json').write_text(json.dumps({'median_ns':statistics.median(values),'min_ns':min(values),'max_ns':max(values),'samples':len(values),'environment':{'DOTNET_TieredCompilation':'0','DOTNET_ReadyToRun':'0'}},indent=2))
print('net median',statistics.median(values),flush=True)
