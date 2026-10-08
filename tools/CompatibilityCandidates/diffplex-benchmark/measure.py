"""Owned Linux-only comparison driver. Execute only in the approved offline guest."""
from pathlib import Path
import os,subprocess,time,json,statistics,hashlib,math,threading
out=Path('/evidence'); rows=[]; started=time.monotonic()
env=os.environ.copy();env.update(DOTNET_TieredCompilation='0',DOTNET_ReadyToRun='0',DOTNET_gcServer='0',DOTNET_PROCESSOR_COUNT='2')
commands={'dotnet':['dotnet','/work/dotnet-host/DotNetHost.dll'],'cilica':['/work/out/native-bench']}
def run(runtime,mode,work,iterations=None,label=''):
    name=f'{runtime}-{mode}-{work}{label}';args=commands[runtime]+[mode,str(work)]+([] if iterations is None else [str(iterations)])
    stdout=out/(name+'.jsonl');stderr=out/(name+'.stderr');t=time.perf_counter()
    with stdout.open('wb') as so,stderr.open('wb') as se:
        p=subprocess.Popen(args,stdout=so,stderr=se,env=env)
        budget=min(180,1500-(time.monotonic()-started))
        if budget<=0:p.kill();p.wait();raise TimeoutError(name)
        timed_out=[]
        def stop():
            timed_out.append(True)
            try:p.kill()
            except ProcessLookupError:pass
        watchdog=threading.Timer(budget,stop);watchdog.start()
        try:pid,status,usage=os.wait4(p.pid,0)
        finally:watchdog.cancel()
        if timed_out:raise TimeoutError(name)
        p.returncode=os.waitstatus_to_exitcode(status)
    row={'name':name,'runtime':runtime,'mode':mode,'workload':work,'iterations':iterations,'wall_seconds':time.perf_counter()-t,'user_seconds':usage.ru_utime,'system_seconds':usage.ru_stime,'peak_rss_kib':usage.ru_maxrss,'exit_code':p.returncode,'records':[json.loads(x) for x in stdout.read_text().splitlines()]}
    rows.append(row);(out/'measurement-progress.json').write_text(json.dumps(rows,indent=2))
    if p.returncode!=0:raise RuntimeError(name+' failed: '+stderr.read_text()[-2000:])
    if stderr.stat().st_size:raise RuntimeError(name+' unexpected stderr: '+stderr.read_text()[-2000:])
    return row
metadata={'platform':list(os.uname()),'cpuinfo':Path('/proc/cpuinfo').read_text(),'cpu_max':Path('/sys/fs/cgroup/cpu.max').read_text(),'memory_max':Path('/sys/fs/cgroup/memory.max').read_text(),'settings':{k:env[k] for k in ['DOTNET_TieredCompilation','DOTNET_ReadyToRun','DOTNET_gcServer','DOTNET_PROCESSOR_COUNT']},'input_sha256_utf16le':[]}
for work,size in [(0,128),(1,1024)]:
    a=''.join('\n' if work==1 and i%16==15 else chr(ord('a')+(i+(i>>4))%26) for i in range(size));b=list(a);b[31 if work==0 else 130]='X';b[95 if work==0 else 770]='Y';b=''.join(b)
    metadata['input_sha256_utf16le'].append({'workload':work,'units_each':size,'old':hashlib.sha256(a.encode('utf-16le')).hexdigest(),'new':hashlib.sha256(b.encode('utf-16le')).hexdigest()})
(out/'benchmark-environment.json').write_text(json.dumps(metadata,indent=2));(out/'cpu-stat-before.txt').write_text(Path('/sys/fs/cgroup/cpu.stat').read_text())
for work in [0,1]:
    verified=[run(r,'verify',work) for r in commands]
    assert verified[0]['records']==verified[1]['records'],'Full output mismatch'
    (out/f'output-{work}.sha256').write_text(hashlib.sha256(json.dumps(verified[0]['records'],separators=(',',':')).encode()).hexdigest()+'\n')
    expected=verified[0]['records'][0]['checksum']
    for repeat in range(3):
        for runtime in (['dotnet','cilica'] if repeat%2==0 else ['cilica','dotnet']):
            row=run(runtime,'cold',work,label=f'-{repeat}');assert row['records'][0]['checksum']==expected
    calibration=[run(r,'measure',work,1,label='-calibration') for r in commands]
    slow=max(statistics.median(x['ns'] for x in row['records'] if 'sample' in x) for row in calibration)
    iterations=max(1,min(2048,math.ceil(250_000_000/slow)))
    checksum=((expected*iterations+2**31)%2**32)-2**31
    for runtime in (['dotnet','cilica'] if work==0 else ['cilica','dotnet']):
        row=run(runtime,'measure',work,iterations)
        assert all(x['checksum']==checksum for x in row['records'] if 'sample' in x),'Timed output mismatch'
summary=[]
for work in [0,1]:
    case={'workload':work}
    for runtime in commands:
        row=next(x for x in rows if x['name']==f'{runtime}-measure-{work}')
        samples=[x['ns']/x['iterations'] for x in row['records'] if 'sample' in x]
        cold=[x for x in rows if x['runtime']==runtime and x['mode']=='cold' and x['workload']==work]
        case[runtime]={'ns_per_operation':samples,'median_ns':statistics.median(samples),'min_ns':min(samples),'max_ns':max(samples),'iterations_per_sample':row['iterations'],'peak_rss_kib':row['peak_rss_kib'],'cold_first_call_ns':[x['records'][0]['ns'] for x in cold],'cold_process_wall_ms':[x['wall_seconds']*1000 for x in cold],'cold_process_peak_rss_kib':[x['peak_rss_kib'] for x in cold]}
    case['cilica_over_dotnet_ratio']=case['cilica']['median_ns']/case['dotnet']['median_ns'];summary.append(case)
(out/'measurement-results.json').write_text(json.dumps({'summary':summary,'runs':rows},indent=2));(out/'cpu-stat-after.txt').write_text(Path('/sys/fs/cgroup/cpu.stat').read_text())
print(json.dumps(summary,indent=2))
