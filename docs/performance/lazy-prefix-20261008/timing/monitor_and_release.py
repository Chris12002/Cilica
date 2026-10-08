"""Bounded host load record; records process names and CPU time, never arguments."""
from pathlib import Path
import json,subprocess,time
b=Path(__file__).resolve().parent
d=['docker','--config',str(b/'docker-config'),'--host','unix:///Users/christian/.docker/run/docker.sock']
name='cilica-prefix-timing-20261008'
def snapshot():
    raw=subprocess.check_output(['ps','-axo','pid=,time=,comm='],text=True)
    rows={}
    for line in raw.splitlines():
        parts=line.strip().split(None,2)
        if len(parts)!=3:continue
        pid,cpu,command=parts;value=0
        for p in cpu.split(':'):value=value*60+float(p)
        rows[int(pid)]=(value,Path(command).name)
    return rows
previous=snapshot();last=time.monotonic();started=last
with (b/'host-load.jsonl').open('w') as out:
    out.write(json.dumps(dict(event='before-release',unix_ns=time.time_ns()))+'\n');out.flush()
    subprocess.run(d+['exec',name,'touch','/work/run-clean'],check=True,timeout=10)
    while time.monotonic()-started<240:
        time.sleep(2);current=snapshot();now=time.monotonic();elapsed=now-last
        loads=[]
        for pid,(cpu,command) in current.items():
            delta=max(0,cpu-previous.get(pid,(cpu,command))[0]);percent=100*delta/elapsed
            if percent>=5 or any(t in command.lower() for t in ['clang','dotnet','csc','compiler']):loads.append(dict(pid=pid,name=command,cpu_percent=round(percent,2)))
        out.write(json.dumps(dict(unix_ns=time.time_ns(),interval_seconds=elapsed,processes=sorted(loads,key=lambda p:p['cpu_percent'],reverse=True)))+'\n');out.flush()
        previous=current;last=now
        result=subprocess.run(d+['exec',name,'test','-f','/evidence/result.json'],capture_output=True,timeout=10)
        if result.returncode==0:break
        if result.returncode!=1:break # Supervisor may already have removed the completed container.
    out.write(json.dumps(dict(event='monitor-end',unix_ns=time.time_ns()))+'\n')
