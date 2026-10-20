from pathlib import Path
import subprocess,json,statistics,hashlib,sys
out=Path(__file__).resolve().parent;root=Path.cwd();name=out.name.split('-')[0]
baseline=Path(sys.argv[1]).resolve();cpp=out/(name+'.cpp');exe=out/name
cmd=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage',str(cpp),'-o',str(exe)]
r=subprocess.run(cmd,text=True,capture_output=True,timeout=240);(out/'release-build.log').write_text(r.stdout+r.stderr);r.check_returncode()
summary={};commands={}
for work in [0,1]:
 expected=json.loads((out.parent/'v111-profile-20261020'/('baseline-verify.json' if work==0 else 'dotnet-line-verify.json')).read_text())
 for label,binary in [('before',baseline),(name,exe)]:
  raw=subprocess.check_output([str(binary),'verify',str(work)],text=True,timeout=120);assert json.loads(raw)==expected;(out/f'{label}-verify-work{work}.json').write_text(raw)
 commands[work]={'before':[str(baseline),'measure',str(work),'1024'],name:[str(exe),'measure',str(work),'1024']}
 results={n:[] for n in commands[work]};processes={n:[] for n in results}
 for repeat in range(3):
  names=list(results)
  if repeat%2:names.reverse()
  for n in names:
   raw=subprocess.check_output(commands[work][n],text=True,timeout=60);(out/f'timing-work{work}-{n}-{repeat}.jsonl').write_text(raw);rows=[json.loads(l) for l in raw.splitlines()];assert len(rows)==8 and all(r['checksum']==(6146 if work==0 else 3122)*r['iterations'] for r in rows[1:]);v=[r['ns']/r['iterations'] for r in rows[1:]];results[n]+=v;processes[n].append(statistics.median(v))
 summary[work]={n:{'median_ns':statistics.median(v),'min_ns':min(v),'max_ns':max(v),'samples':len(v),'process_medians':processes[n],'reduction_percent':100*(1-statistics.median(v)/statistics.median(results['before']))} for n,v in results.items()}
(out/'summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
inputs=Path('/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs/dll')
prov={'base_revision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'command':cmd,'timing_commands':commands,'cpp_sha256':sha(cpp),'exe_sha256':sha(exe),'baseline_sha256':sha(baseline),'bytes':exe.stat().st_size,'max_frame_bytes':max(int(l.split('\t')[-2]) for l in (out/(name+'.su')).read_text().splitlines()),'inputs':{p.name:sha(p) for p in inputs.glob('DiffPlex.Benchmark.*') if p.suffix in ['.dll','.pdb']},'compiler_sources':{str(p):sha(p) for p in (root/'src/Cilica.Compiler').glob('*.cs')},'core_files':{str(p.relative_to(out)):sha(p) for p in (out/(name+'-core')).rglob('*.dll')}}
(out/'provenance.json').write_text(json.dumps(prov,indent=2))
