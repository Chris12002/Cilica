"""Separate clean native comparison; release only after diagnostics and quiet window."""
from pathlib import Path
import hashlib,json,subprocess,time,traceback,sys
sys.path.insert(0,'/approved')
from lazy_prefix import transform,SOURCE_SHA
from transform import replace_once
import measure
out=Path('/evidence');started=time.monotonic();steps=[]
def run(name,args):
    steps.append(dict(name=name,argv=args))
    with (out/(name+'.log')).open('w') as log:p=subprocess.run(args,stdout=log,stderr=subprocess.STDOUT,timeout=180)
    steps[-1]['exit_code']=p.returncode
    assert p.returncode==0,name
try:
    source=Path('/approved/native-bench.cpp');assert hashlib.sha256(source.read_bytes()).hexdigest()==SOURCE_SHA
    for mode in ['baseline','prefix']:
        text=source.read_text()
        if mode=='prefix':text=transform(text)
        text=replace_once(text,'    Instance heap;Context c(heap);','    if(std::strcmp(argv[1],"noop")==0) { std::puts("0");return 0; }\n    Instance heap;Context c(heap);')
        if mode=='prefix':
            needle='heap.bytes,heap.count,c.peak);'
            text=replace_once(text,needle,needle+'\n        std::printf("{\\"scratch_bytes\\":%zu,\\"scratch_peak\\":%zu,\\"managed_plus_scratch_bytes\\":%zu,\\"managed_plus_scratch_peak\\":%zu}\\n",heap.ownership_bytes,heap.ownership_peak,heap.total_memory(),heap.total_memory_peak);')
        src=Path('/work/'+mode+'.cpp');src.write_text(text);exe='/work/'+mode
        flags=['clang++-18','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-Wall','-Wextra','-Werror','-Wframe-larger-than=2048','-fstack-usage']
        run('compile-'+mode,flags+[str(src),'-o',exe])
        (out/(mode+'-hashes.json')).write_text(json.dumps({p:hashlib.sha256(Path(p).read_bytes()).hexdigest() for p in [str(src),exe]}))
        lines=Path(exe+'.su').read_text().splitlines();maximum=max(int(s.split('\t')[-2]) for s in lines);assert maximum<=2048
        (out/(mode+'-stack.json')).write_text(json.dumps(dict(max_bytes=maximum)))
    measure.COMMANDS={'baseline':['/work/baseline'],'prefix':['/work/prefix']}
    expected=json.loads(Path('/approved/expected-transcript.json').read_text())
    for runtime in measure.COMMANDS:assert measure.run(runtime,'verify',timeout=300)['records']==[expected]
    (out/'ready-for-clean.json').write_text(json.dumps(dict(elapsed_seconds=time.monotonic()-started)))
    while not Path('/work/run-clean').exists():
        if time.monotonic()-started>1400:raise TimeoutError('Quiet-window release absent')
        time.sleep(1)
    results=[]
    (out/'cpu-stat-before.txt').write_text(Path('/sys/fs/cgroup/cpu.stat').read_text())
    for repeat in range(3):
        for runtime in (['baseline','prefix'] if repeat%2==0 else ['prefix','baseline']):
            for mode in ['noop','cold','measure']:
                results.append(measure.run(runtime,mode,16 if mode=='measure' else None,suffix='-'+str(repeat),timeout=180))
                (out/'clean-progress.json').write_text(json.dumps(results,indent=2))
    (out/'clean-results.json').write_text(json.dumps(dict(summary=measure.summarize(results),runs=results),indent=2))
    (out/'cpu-stat-after.txt').write_text(Path('/sys/fs/cgroup/cpu.stat').read_text())
    result=dict(status='passed',elapsed_seconds=time.monotonic()-started,steps=steps)
except Exception as ex:
    (out/'runner-error.txt').write_text(traceback.format_exc());result=dict(status='failed',error=str(ex),steps=steps)
(out/'result.json').write_text(json.dumps(result,indent=2))
while True:time.sleep(10)
