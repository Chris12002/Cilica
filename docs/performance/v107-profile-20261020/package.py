from pathlib import Path
import json,hashlib,subprocess,shutil
out=Path(__file__).resolve().parent;dest=Path('docs/performance/v107-profile-20261020');dest.mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
inputs=Path('/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/inputs')
paths=[out/'baseline.cpp',out/'baseline',out/'profile',out/'bounded_combined.cpp',out/'bounded_combined',out/'baseline-core/Cilica.Core.dll',out/'baseline-core/build-options.json',inputs/'dll/DiffPlex.Benchmark.dll',inputs/'dll/DiffPlex.Benchmark.pdb',inputs/'host/DotNetHost.dll',out/'preserved-primary-emitter.patch',*out.glob('sample-*.txt'),out/'baseline-native.txt',out/'small_only-native.txt',out/'bounded_combined-native.txt']
primary=Path('/Users/christian/Cilica');primarydiff=subprocess.check_output(['git','-C',str(primary),'diff','--','src/Cilica.Compiler/ObjectCppEmitter.cs'])
assert primarydiff==(out/'preserved-primary-emitter.patch').read_bytes(),'Primary checkout changed during investigation'
assert not subprocess.check_output(['git','diff','HEAD','--','src/Cilica.Compiler','library/Cilica.Core'])
assert (out/'baseline.cpp').read_bytes()==(primary/'artifacts/local-output-performance-20261020/outputs.cpp').read_bytes()
identity={'revision':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'branch':subprocess.check_output(['git','branch','--show-current'],text=True).strip(),'compiler_and_core_unchanged':True,'fresh_cpp_matches_accepted_v107':True,'primary_emitter_rollback_preserved':True,'primary_emitter_sha256':sha(primary/'src/Cilica.Compiler/ObjectCppEmitter.cs'),'compiler_files':{str(p):sha(p) for p in Path('src/Cilica.Compiler').glob('*.cs')},'files':{str(p):sha(p) for p in paths},'clang':subprocess.check_output(['clang++','--version'],text=True).strip()}
(out/'identity.json').write_text(json.dumps(identity,indent=2));(out/'raw-timings.json').write_text(json.dumps({p.name:[json.loads(l) for l in p.read_text().splitlines()] for p in sorted(out.glob('*.jsonl')) if p.name.startswith(('timing-','experiment-','followup-','discriminate-','final-'))},indent=2))
for p in out.glob('*.py'):shutil.copy2(p,dest/p.name)
for name in ['identity.json','profile-provenance.json','timing-summary.json','profile-aggregate.json','profile-summary-0.json','profile-summary-1.json','experiment-provenance.json','experiment-summary.json','followup-provenance.json','followup-summary.json','discriminate-provenance.json','discriminate-summary.json','final-summary.json','final-provenance.json','acceptance-results.json','counts-summary.json','method-counts.json','metrics-summary.json','native-summary.json','reference-excerpts.json','reference-sources.json','rea-selected.json','raw-timings.json','build.log']:
 shutil.copy2(out/name,dest/name)
for name in ['build-options.json','library-build.json']:shutil.copy2(out/'baseline-core'/name,dest/('core-'+name))
print('Packaged',len(list(dest.iterdir())),'files; compiler/core clean; primary rollback unchanged')
