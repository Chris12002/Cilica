from pathlib import Path
import subprocess,json,time,hashlib,traceback
out=Path('/evidence');started=time.monotonic();steps=[]
def run(name,args,stdin=None):
    steps.append(dict(name=name,argv=args))
    with (out/(name+'.log')).open('w') as log:
        p=subprocess.run(args,stdout=log,stderr=subprocess.STDOUT,input=stdin,text=True,timeout=120)
    steps[-1]['exit_code']=p.returncode
    if p.returncode:raise RuntimeError(name)
try:
    for tool in ['clang++-18','nm','addr2line']:run(tool+'-version',[tool,'--version'])
    for mode in ['scopes','sample','boundary-baseline','boundary-index']:
        src='/work/'+mode+'.cpp';exe='/work/'+mode
        run('transform-'+mode,['python3','/approved/residual.py','/approved/native-bench.cpp',src,mode])
        flags=['clang++-18','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-g','-fno-omit-frame-pointer']
        if mode=='sample':flags+=['-no-pie','-ldl']
        if mode.startswith('boundary'):flags+=['-fsanitize=address,undefined','-Wframe-larger-than=2048']
        run('compile-'+mode,flags+[src,'-o',exe])
        (out/(mode+'-hashes.json')).write_text(json.dumps({p:hashlib.sha256(Path(p).read_bytes()).hexdigest() for p in [src,exe]}))
        command='residual' if mode=='scopes' else 'sample' if mode=='sample' else 'boundary'
        run('run-'+mode,[exe,command,'0'])
        if mode=='sample':
            pcs=sorted(set(line.split()[0] for line in (out/'run-sample.log').read_text().splitlines()[1:]))
            run('symbols',['nm','-n','-S','-C',exe])
            run('lines',['addr2line','-a','-f','-i','-C','-e',exe],stdin='\n'.join(pcs)+'\n')
    result=dict(status='passed',elapsed_seconds=time.monotonic()-started,steps=steps)
except Exception as ex:
    (out/'runner-error.txt').write_text(traceback.format_exc());result=dict(status='failed',error=str(ex),steps=steps)
(out/'result.json').write_text(json.dumps(result,indent=2))
while True:time.sleep(10)
