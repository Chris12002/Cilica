"""Diagnostic correctness run, deliberately separate from clean timing."""
from pathlib import Path
import hashlib,json,os,subprocess,time

out=Path('/evidence');src=Path('/work/out/index.cpp').read_text()
old='Instance heap;Context c(heap);'
assert src.count(old)==1
src=src.replace(old,'Instance heap; heap.collect_before_allocation=true; Context c(heap);')
p=Path('/work/out/index-forced.cpp');p.write_text(src)
flags=['clang++-18','-std=c++20','-O1','-g','-fno-exceptions','-fno-rtti','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(p),'-o','/work/out/index-forced']
started=time.monotonic()
with (out/'index-sanitizer-build.log').open('w') as log:
    subprocess.run(flags,stdout=log,stderr=subprocess.STDOUT,check=True,timeout=120)
env=os.environ.copy();env.update(ASAN_OPTIONS='detect_leaks=1:halt_on_error=1',UBSAN_OPTIONS='halt_on_error=1:print_stacktrace=1')
with (out/'index-sanitizer-verify.jsonl').open('w') as stdout,(out/'index-sanitizer-verify.stderr').open('w') as stderr:
    subprocess.run(['/work/out/index-forced','verify','0'],stdout=stdout,stderr=stderr,env=env,check=True,timeout=120)
assert (out/'index-sanitizer-verify.stderr').stat().st_size==0
assert json.loads((out/'index-sanitizer-verify.jsonl').read_text())==json.loads((out/'expected-transcript.json').read_text())
(out/'index-sanitizer-result.json').write_text(json.dumps({'status':'passed','observations':523,'flags':flags,'forced_gc':True,'elapsed_seconds':time.monotonic()-started,'source_sha256':hashlib.sha256(p.read_bytes()).hexdigest()},indent=2))
