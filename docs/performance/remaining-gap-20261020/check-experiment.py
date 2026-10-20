from pathlib import Path
import subprocess,json
out=Path(__file__).resolve().parent
name='combined_native_frames';s=(out/(name+'.cpp')).read_text();s=s.replace('Instance heap;Context c(heap);','Instance heap;heap.collect_before_allocation=true;Context c(heap);')
p=out/'diagnostic-check.cpp';p.write_text(s)
cmd=['clang++','-std=c++20','-O1','-g','-DCILICA_DIRECT_CONTROL_FLOW=1','-fno-exceptions','-fno-rtti','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(out/'diagnostic-check')]
r=subprocess.run(cmd,text=True,capture_output=True,timeout=240);(out/'diagnostic-check-build.log').write_text(r.stdout+r.stderr);r.check_returncode()
r=subprocess.run([str(out/'diagnostic-check'),'verify','0'],text=True,capture_output=True,timeout=180);(out/'diagnostic-check-output.json').write_text(r.stdout);(out/'diagnostic-check.log').write_text(r.stderr);r.check_returncode();assert json.loads(r.stdout)==json.loads((out/'baseline-verify.json').read_text())
(out/'diagnostic-check-result.json').write_text(json.dumps({'variant':name,'command':cmd,'forced_gc':True,'all_523_values_match':True,'sanitizer_diagnostics':r.stderr,'scope':'DiffPlex only; no stack-budget/recursion or general semantics acceptance'},indent=2));print('PASS',flush=True)
