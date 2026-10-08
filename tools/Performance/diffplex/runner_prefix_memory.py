"""Diagnostic RSS measurement; its sampled runs are NOT clean compute timings."""
from pathlib import Path
import hashlib,json,os,subprocess,time,traceback,sys
sys.path.insert(0,'/approved')
from lazy_prefix import transform,SOURCE_SHA
from transform import replace_once
out=Path('/evidence');started=time.monotonic();steps=[]
def run(name,args):
    steps.append(dict(name=name,argv=args))
    with (out/(name+'.log')).open('w') as log:p=subprocess.run(args,stdout=log,stderr=subprocess.STDOUT,timeout=180)
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
        hashes={p:hashlib.sha256(Path(p).read_bytes()).hexdigest() for p in [str(src),exe]}
        expected=json.loads(Path('/approved/'+mode+'-hashes.json').read_text());assert hashes==expected
        (out/(mode+'-hashes.json')).write_text(json.dumps(hashes))
    records=[]
    for mode in ['baseline','prefix']:
        exe='/work/'+mode;args=[exe,'measure','0','2'];seen=[]
        parent=Path('/proc/self/status').read_text()
        with (out/(mode+'-memory.stdout')).open('w') as stdout,(out/(mode+'-memory.stderr')).open('w') as stderr:
            child=subprocess.Popen(args,stdout=stdout,stderr=stderr)
            deadline=time.monotonic()+60
            while time.monotonic()<deadline:
                try:
                    actual=os.readlink('/proc/'+str(child.pid)+'/exe')
                    status=Path('/proc/'+str(child.pid)+'/status').read_text()
                    if actual==exe:
                        hwm=next((int(s.split()[1]) for s in status.splitlines() if s.startswith('VmHWM:')),None)
                        if hwm is not None:seen.append(hwm)
                except (FileNotFoundError,ProcessLookupError):pass
                pid,status,usage=os.wait4(child.pid,os.WNOHANG)
                if pid:
                    child.returncode=os.waitstatus_to_exitcode(status);break
                time.sleep(.002)
            else:
                child.kill();child.wait();raise TimeoutError(mode)
        assert child.returncode==0 and seen
        assert (out/(mode+'-memory.stderr')).stat().st_size==0
        for line in (out/(mode+'-memory.stdout')).read_text().splitlines():
            row=json.loads(line)
            if 'checksum' in row:assert row['checksum']==6146*2
        records.append(dict(runtime=mode,argv=args,parent_status=parent,wait4_peak_rss_kib=usage.ru_maxrss,
                            sampled_post_exec_vmhwm_kib=max(seen),samples=len(seen),scope='post-exec VmHWM sampled lower bound, separate diagnostic run'))
    result=dict(status='passed',elapsed_seconds=time.monotonic()-started,records=records,steps=steps)
except Exception as ex:
    (out/'runner-error.txt').write_text(traceback.format_exc());result=dict(status='failed',error=str(ex),steps=steps)
(out/'result.json').write_text(json.dumps(result,indent=2))
while True:time.sleep(10)
