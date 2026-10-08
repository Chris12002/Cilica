"""Bounded offline correctness run. No clean timing in this recipe."""
from pathlib import Path
import hashlib,json,os,subprocess,time,traceback
out=Path('/evidence');steps=[];started=time.monotonic()
env=os.environ.copy();env.update(ASAN_OPTIONS='detect_leaks=1:halt_on_error=1',UBSAN_OPTIONS='halt_on_error=1:print_stacktrace=1')
def run(name,args,expected_failure=False):
    row=dict(name=name,argv=args);steps.append(row)
    with (out/(name+'.stdout')).open('w') as stdout,(out/(name+'.stderr')).open('w') as stderr:
        p=subprocess.run(args,stdout=stdout,stderr=stderr,env=env,timeout=180)
    row['exit_code']=p.returncode
    if expected_failure:
        assert p.returncode!=0 and ('runtime error:' in (out/(name+'.stderr')).read_text() or 'AddressSanitizer' in (out/(name+'.stderr')).read_text())
    else:
        assert p.returncode==0,name
        assert (out/(name+'.stderr')).stat().st_size==0,name+' stderr'
try:
    for mode in ['tests','forced','baseline-tail']:
        src=Path('/work/'+mode+'.cpp')
        run('transform-'+mode,['python3','-B','/approved/prefix_diagnostics.py','/approved/native-bench.cpp',str(src),mode])
        for opt in ['0','2']:
            name=mode+'-O'+opt;exe='/work/'+name
            flags=['clang++-18','-std=c++20','-O'+opt,'-g','-fno-exceptions','-fno-rtti','-Wall','-Wextra','-Werror','-Wframe-larger-than=2048','-fstack-usage','-fsanitize=address,undefined','-fno-omit-frame-pointer']
            run('compile-'+name,flags+[str(src),'-o',exe])
            (out/(name+'-hashes.json')).write_text(json.dumps({p:hashlib.sha256(Path(p).read_bytes()).hexdigest() for p in [str(src),exe]}))
            stack=Path(exe+'.su'); lines=stack.read_text().splitlines(); sizes=[int(line.split('\t')[-2]) for line in lines]
            assert max(sizes)<=2048,(name,max(sizes))
            (out/(name+'-stack.json')).write_text(json.dumps(dict(max_bytes=max(sizes),functions=len(sizes))))
            if mode=='forced':
                run('verify-'+name,[exe,'verify','0'])
                assert json.loads((out/('verify-'+name+'.stdout')).read_text())==json.loads(Path('/approved/expected-transcript.json').read_text())
            else:
                run('run-'+name,[exe,'tests' if mode=='tests' else 'tail','0'])
                run('unreadable-miss-'+name,[exe,'bad-tail','0'],expected_failure=True)
    result=dict(status='passed',elapsed_seconds=time.monotonic()-started,steps=steps)
except Exception as ex:
    (out/'runner-error.txt').write_text(traceback.format_exc());result=dict(status='failed',error=str(ex),steps=steps)
(out/'result.json').write_text(json.dumps(result,indent=2))
while True:time.sleep(10)
