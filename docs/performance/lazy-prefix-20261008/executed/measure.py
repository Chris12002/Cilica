"""Small non-gating regression suite, only inside the approved offline guest.

Compute excludes launch, serialization and teardown. End-to-end and no-op process
measurements include those costs. RSS and managed counters are separate measures.
"""
from pathlib import Path
import json, os, statistics, subprocess, threading, time

OUT=Path('/evidence')
ENV=os.environ.copy()
ENV.update(DOTNET_TieredCompilation='0',DOTNET_ReadyToRun='0',DOTNET_gcServer='0',DOTNET_PROCESSOR_COUNT='2')
COMMANDS={'baseline':['/work/out/clean'], 'index':['/work/out/index'],
          'dotnet':['dotnet','/work/dotnet-host/DotNetHost.dll']}


def run(runtime, mode, iterations=None, suffix='', timeout=120):
    args=COMMANDS[runtime]+[mode,'0']+([] if iterations is None else [str(iterations)])
    name=f'{runtime}-{mode}{suffix}'
    started=time.perf_counter_ns();started_unix_ns=time.time_ns()
    with (OUT/(name+'.jsonl')).open('wb') as stdout,(OUT/(name+'.stderr')).open('wb') as stderr:
        p=subprocess.Popen(args,stdout=stdout,stderr=stderr,env=ENV)
        timer=threading.Timer(timeout,p.kill);timer.start()
        try: _,status,usage=os.wait4(p.pid,0)
        finally:timer.cancel()
        p.returncode=os.waitstatus_to_exitcode(status)
    wall=time.perf_counter_ns()-started
    assert p.returncode==0,(name,p.returncode)
    assert (OUT/(name+'.stderr')).stat().st_size==0,name
    records=[json.loads(s) for s in (OUT/(name+'.jsonl')).read_text().splitlines()]
    row=dict(name=name,runtime=runtime,mode=mode,argv=args,wall_ns=wall,started_unix_ns=started_unix_ns,
             user_seconds=usage.ru_utime,system_seconds=usage.ru_stime,
             peak_rss_kib=usage.ru_maxrss,records=records)
    if mode in ('cold','measure'):
        for r in records:
            if 'checksum' in r: assert r['checksum']==6146*r.get('iterations',1),(name,r)
    return row


def summarize(results):
    summary={}
    for runtime in COMMANDS:
        rows=[r for r in results if r['runtime']==runtime]
        samples=[v['ns']/v['iterations'] for r in rows if r['mode']=='measure' for v in r['records'] if 'sample' in v]
        summary[runtime]={'compute_ns_per_op':samples,'median_ns':statistics.median(samples),
            'min_ns':min(samples),'max_ns':max(samples),
            'noop_process_ns':[r['wall_ns'] for r in rows if r['mode']=='noop'],
            'first_call_ns':[r['records'][0]['ns'] for r in rows if r['mode']=='cold'],
            'cold_end_to_end_ns':[r['wall_ns'] for r in rows if r['mode']=='cold'],
            'measure_peak_rss_kib':[r['peak_rss_kib'] for r in rows if r['mode']=='measure']}
    return summary


def main():
    # Baseline transcript was fully verified in the immutable accepted trial.
    # Recheck the experiment and current .NET against that exact 523-value result.
    expected=json.loads((OUT/'expected-transcript.json').read_text())
    for r in ('dotnet','index'):
        assert run(r,'verify')['records']==[expected]
    results=[]
    (OUT/'cpu-stat-clean-before.txt').write_text(Path('/sys/fs/cgroup/cpu.stat').read_text())
    for repeat in range(3):
        order=['baseline','index','dotnet'] if repeat%2==0 else ['dotnet','index','baseline']
        for runtime in order:
            for mode in ('noop','cold'):
                results.append(run(runtime,mode,suffix=f'-{repeat}'))
            # Same native batch counts and warmup rule before/after. .NET batches
            # are longer to avoid sub-resolution timing; never gate on ratios.
            count=16 if runtime!='dotnet' else 32768
            results.append(run(runtime,'measure',count,suffix=f'-{repeat}'))
            (OUT/'clean-progress.json').write_text(json.dumps(results,indent=2))
    summary=summarize(results)
    (OUT/'clean-results.json').write_text(json.dumps({'summary':summary,'runs':results},indent=2))
    (OUT/'cpu-stat-clean-after.txt').write_text(Path('/sys/fs/cgroup/cpu.stat').read_text())


if __name__=='__main__':main()
