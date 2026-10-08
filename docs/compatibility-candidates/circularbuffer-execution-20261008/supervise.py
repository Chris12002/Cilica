"""Host watchdog for a precreated, inspected container; copies only regular evidence files."""
import hashlib,json,pathlib,subprocess,tarfile,time,threading
base=pathlib.Path(__file__).resolve().parent
D=['docker','--config','/tmp/cilica-circularbuffer-approved-20261008/docker-config','--host','unix:///Users/christian/.docker/run/docker.sock']
name='cilica-circularbuffer-approved-20261008'
def docker(args,timeout=20):return subprocess.run(D+args,capture_output=True,timeout=timeout,check=True).stdout
spec=json.loads(docker(['inspect',name]))[0];h=spec['HostConfig'];c=spec['Config']
assert h['NetworkMode']=='none' and h['ReadonlyRootfs'] and c['User']=='10001:10001'
assert h['NanoCpus']==2000000000 and h['Memory']==8589934592 and h['MemorySwap']==8589934592 and h['PidsLimit']==256
assert h['CapDrop']==['ALL'] and 'no-new-privileges' in h['SecurityOpt'] and h['LogConfig']['Type']=='none'
assert not h['Binds'] and not spec['Mounts'] and not h['Privileged']
assert set(h['Tmpfs'])=={'/work','/tmp','/evidence'}
assert h['Tmpfs']['/work'].startswith('rw,exec,nosuid,nodev,size=9g,') and h['Tmpfs']['/tmp'].startswith('rw,exec,nosuid,nodev,size=1g,')
assert h['Tmpfs']['/evidence'].startswith('rw,noexec,nosuid,nodev,size=1g,')
(base/'container-inspect.json').write_text(json.dumps(spec,indent=2)+'\n')
def hard_stop():
    subprocess.run(D+['rm','-f',name],capture_output=True,timeout=10)
started=time.monotonic();watchdog=threading.Timer(1790,hard_stop);watchdog.daemon=True;watchdog.start();docker(['start',name]); print('Verified isolation; started container.',flush=True)
try:
    while time.monotonic()-started<1740:
        check=subprocess.run(D+['exec',name,'test','-f','/evidence/result.json'],capture_output=True,timeout=10)
        if check.returncode==0:break
        time.sleep(5)
    else: print('Execution deadline reached; collecting available evidence.',flush=True)
    evidence=base/'evidence';evidence.mkdir(exist_ok=True)
    # Copy from the idle container through a bounded pipe: reject links, devices and traversal.
    process=subprocess.Popen(D+['exec',name,'tar','-C','/evidence','-cf','-','.'],stdout=subprocess.PIPE,stderr=subprocess.DEVNULL)
    total=0;count=0
    with tarfile.open(fileobj=process.stdout,mode='r|*') as archive:
        for member in archive:
            if time.monotonic()-started>=1790:raise TimeoutError('Evidence-copy deadline')
            path=pathlib.PurePosixPath(member.name)
            if member.isdir():continue
            if not member.isfile() or path.is_absolute() or '..' in path.parts:raise RuntimeError('Unsafe evidence entry')
            total+=member.size;count+=1
            if total>1073741824 or count>1000:raise RuntimeError('Evidence bound exceeded')
            dest=evidence/path
            dest.parent.mkdir(parents=True,exist_ok=True)
            with archive.extractfile(member) as source,dest.open('xb') as target:
                while chunk:=source.read(1048576):target.write(chunk)
    if count==0:raise RuntimeError('Evidence export unexpectedly empty')
    process.wait(timeout=5)
    if process.returncode:raise RuntimeError('Evidence copy failed')
    hashes={str(p.relative_to(evidence)):hashlib.sha256(p.read_bytes()).hexdigest() for p in evidence.rglob('*') if p.is_file()}
    (base/'evidence-sha256.json').write_text(json.dumps(hashes,indent=2)+'\n')
    print('Evidence retained: '+str(evidence),flush=True)
finally:
    subprocess.run(D+['rm','-f',name],capture_output=True,timeout=10)
    watchdog.cancel()
    print('Removed task container after %.1f seconds.'%(time.monotonic()-started),flush=True)
