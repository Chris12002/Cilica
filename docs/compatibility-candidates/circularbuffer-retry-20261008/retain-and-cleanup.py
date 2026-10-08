from pathlib import Path
import json,hashlib,shutil,subprocess,tarfile
r=Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica');b=r/'artifacts/circularbuffer-retry-20261008';c=Path('/tmp/cilica-circularbuffer-retry-20261008');dest=r/'docs/compatibility-candidates/circularbuffer-retry-20261008';dest.mkdir()
manifest=json.loads((b/'input-provenance.json').read_text());proposal=json.loads((r/'docs/compatibility-candidates/circularbuffer-retry-proposal.json').read_text());result=json.loads((b/'evidence/result.json').read_text());assert result['status']=='passed'; log=(b/'evidence/differential.log').read_text(); assert 'O0 completed all 4531 observations.' in log and 'O2 completed all 4531 observations.' in log and 'retained 72 source/executable hash pairs' in log
receipts=list((b/'evidence/native-hashes').glob('*.json')); assert len(receipts)==72
for opt in ('O0','O2'):
 assert sum(json.loads(p.read_text())['optimization']==opt for p in receipts)==36
for p in receipts:
 j=json.loads(p.read_text()); assert j['sanitizers']=='address,undefined'
 for key in ('source_sha256','executable_sha256'):
  assert len(j[key])==64 and all(x in '0123456789abcdef' for x in j[key])
assert (b/'evidence/candidate-assembly.sha256').read_text()==(r/'docs/compatibility-candidates/circularbuffer-execution-20261008/candidate-assembly.sha256').read_text()
for name,sha in json.loads((b/'evidence-sha256.json').read_text()).items():
 assert hashlib.sha256((b/'evidence'/name).read_bytes()).hexdigest()==sha
for p in b.iterdir():
 if p.is_file():shutil.copy2(p,dest/p.name)
for p in (b/'evidence').rglob('*'):
 if p.is_file():out=dest/p.relative_to(b/'evidence');out.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,out)
# Verify the entire disposable input tree before deleting anything.
expected={}
with tarfile.open(c/'cilica.tar') as t:
 for m in t:
  if m.isfile():expected['cilica/'+m.name]=hashlib.sha256(t.extractfile(m).read()).hexdigest()
for path,sha in manifest['owned_overlay_sha256'].items():expected['cilica/'+path]=sha
expected['cilica/tests/Cilica.Metadata.Tests/ApprovedCircularBufferExecutionTests.cs']=manifest['owned_overlay_sha256']['tools/CompatibilityCandidates/circularbuffer-validation/ApprovedCircularBufferExecutionTests.cs']
for p in manifest['packages']:
 name,version=p['package'].lower().split('/');expected['offline-feed/'+name+'.'+version+'.nupkg']=p['sha256']
for p in manifest['candidate_files']:expected['input/'+p['path']]=p['sha256']
for name,reference in [('input-provenance.json','input-provenance.json'),('runner.py','runner.py'),('Dockerfile','Input.Dockerfile')]:expected[name]=hashlib.sha256((b/reference).read_bytes()).hexdigest()
actual={str(p.relative_to(c/'inputs')):hashlib.sha256(p.read_bytes()).hexdigest() for p in (c/'inputs').rglob('*') if p.is_file()}
assert expected==actual and not any(p.is_symlink() for p in c.rglob('*'))
assert hashlib.sha256((c/'cilica.tar').read_bytes()).hexdigest()==manifest['archive_sha256']
assert json.loads((c/'docker-config/config.json').read_text())=={}
assert {str(p.relative_to(c)) for p in c.rglob('*') if p.is_file() and not p.is_relative_to(c/'inputs')}=={'cilica.tar','docker-config/config.json','image-id'}
image=json.loads((b/'new-image-id.json').read_text())['id'];assert (c/'image-id').read_text().strip()==image
D=['docker','--config',str(c/'docker-config'),'--host','unix:///Users/christian/.docker/run/docker.sock'];tag='cilica-circularbuffer-retry-20261008:input'
assert json.loads(subprocess.check_output(D+['image','inspect',tag]))[0]['Id']==image
assert subprocess.run(D+['inspect','cilica-circularbuffer-retry-20261008'],capture_output=True).returncode!=0
removed=subprocess.check_output(D+['image','rm',tag],text=True)
assert json.loads(subprocess.check_output(D+['image','inspect',proposal['tooling_image']]))[0]['Id']==proposal['tooling_image']
shutil.rmtree(c)
cleanup={'container':'removed by watchdog after evidence export; absence independently verified before image cleanup','input_image':image,'image_removal':removed,'staging':str(c),'staging_removed':True,'verified_input_files':len(actual),'tooling_retained':proposal['tooling_image'],'preserved':'Frozen pins, Unlicense, owned adapter/driver, full logs, candidate assembly SHA256, all 72 generated-source/executable SHA256 pairs and complete passing evidence; original failed trial remains unchanged; no Cilica or peer source removed.'}
(b/'cleanup.json').write_text(json.dumps(cleanup,indent=2)+'\n');shutil.copy2(b/'cleanup.json',dest/'cleanup.json')
hashes={str(p.relative_to(dest)):hashlib.sha256(p.read_bytes()).hexdigest() for p in dest.rglob('*') if p.is_file()};(dest/'receipt-sha256.json').write_text(json.dumps(hashes,indent=2)+'\n')
print(json.dumps(cleanup,indent=2))
