from pathlib import Path
import sys,json,shutil
sys.path.insert(0,str(Path('tools/Performance').resolve()))
import allocation_verification as b
out=Path('artifacts/execution-slices-final-20261020').resolve();out.mkdir(exist_ok=False)
a=Path('artifacts/direct-control-flow-20261020/native').resolve();z=Path('artifacts/character-construction-20261020/native').resolve()
for source in [a,z]: b.verify_artifacts(source); assert json.loads((source/'validation.json').read_text())['checksum']==6146
for name,source in [('baseline',a),('candidate',z)]:
 for suffix in ['', '.cpp']:shutil.copyfile(source/(name+suffix),out/(name+suffix))
 (out/name).chmod(0o755)
shutil.copyfile(z/'expected.json',out/'expected.json')
metadata={'method':'Final paired comparison reuses exact independently emitted and validated release binaries from first and last slice; no code rewriting or recompilation', 'baseline_provenance':str(a/'provenance.json'),'candidate_provenance':str(z/'provenance.json'), 'hashes':{p.name:b.digest(p) for p in out.iterdir()}}
(out/'provenance.json').write_text(json.dumps(metadata,indent=2)+'\n')
(out/'validation.json').write_text(json.dumps({'checksum':6146,'transcripts_previously_checked':2,'observations_each':523,'source_validations':[str(a/'validation.json'),str(z/'validation.json')]})+'\n')
b.measure(out,iterations=256)
