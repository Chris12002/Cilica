from pathlib import Path
import subprocess,json,hashlib,shutil,tarfile,urllib.request
r=Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica');b=r/'artifacts/circularbuffer-approved-20261008';b.mkdir()
previous=r/'artifacts/crc32-approved-20261008'
pin='87fb7e5f6f3752cf684c90601c1efa4e59af132f';overlay='3625157';candidate='9ebb911dfe27a62c2793d0b02c642f79bbbf524b'
c=Path('/tmp/cilica-circularbuffer-approved-20261008');c.mkdir();(c/'docker-config').mkdir();(c/'docker-config/config.json').write_text('{}\n');inputs=c/'inputs';inputs.mkdir();archive=c/'cilica.tar'
subprocess.run(['git','archive','--format=tar','--output',str(archive),pin],cwd=r,check=True);(inputs/'cilica').mkdir()
with tarfile.open(archive) as t:
 for m in t.getmembers():assert (m.isfile() or m.isdir()) and not Path(m.name).is_absolute() and '..' not in Path(m.name).parts
 t.extractall(inputs/'cilica')
proposal=json.loads(subprocess.check_output(['git','show',overlay+':docs/compatibility-candidates/circularbuffer-execution-proposal.json'],cwd=r))
for path,sha in proposal['owned_overlay_sha256'].items():
 data=subprocess.check_output(['git','show',overlay+':'+path],cwd=r);assert hashlib.sha256(data).hexdigest()==sha
 dest=inputs/'cilica'/path;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
shutil.copy(inputs/'cilica/tools/CompatibilityCandidates/circularbuffer-validation/ApprovedCircularBufferExecutionTests.cs',inputs/'cilica/tests/Cilica.Metadata.Tests/ApprovedCircularBufferExecutionTests.cs')
original=json.loads((inputs/'cilica/tools/CompatibilityCandidates/circularbuffer-validation/offline-packages.json').read_text())
manifest={'cilica_commit':pin,'overlay_commit':overlay,'owned_overlay_sha256':proposal['owned_overlay_sha256'],'candidate_commit':candidate,'candidate_files':proposal['candidate_files'],'packages':original['packages'],'archive_sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'approval':'Direct user approval: Ok use Circular buffer. Frozen proposal 3625157, correctness only.'}
feed=inputs/'offline-feed';feed.mkdir()
for p in manifest['packages']:
 name,version=p['package'].lower().split('/');filename=name+'.'+version+'.nupkg';source=Path('/tmp/cilica-diffplex-retry-20261008')/filename
 if not source.exists():source=Path('/Users/christian/.nuget/packages')/name/version/filename
 data=source.read_bytes();assert hashlib.sha256(data).hexdigest()==p['sha256'];(feed/filename).write_bytes(data)
(inputs/'input').mkdir()
for f in proposal['candidate_files']:
 with urllib.request.urlopen('https://raw.githubusercontent.com/joaoportela/CircularBuffer-CSharp/'+candidate+'/'+f['path'],timeout=45) as response:data=response.read()
 assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==f['git_blob_sha1']
 target=inputs/'input'/f['path'];target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data);f['sha256']=hashlib.sha256(data).hexdigest()
shutil.copy(inputs/'input/LICENSE',b/'CircularBuffer-LICENSE');(b/'input-provenance.json').write_text(json.dumps(manifest,indent=2)+'\n');shutil.copy(b/'input-provenance.json',inputs/'input-provenance.json')
runner=(previous/'runner.py').read_text().replace('crc32-net','circularbuffer').replace('crc32-validation','circularbuffer-validation').replace('26c5a818a5c7a3d6a622c92d3cd08dba586c263c',candidate).replace('Crc32.SelectedRoots','CircularBuffer.SelectedRoots').replace('ApprovedCrc32ExecutionTests','ApprovedCircularBufferExecutionTests')
runner='\n'.join(line for line in runner.splitlines() if "run('stage'" not in line and "run('verify'" not in line)+'\n'
runner=runner.replace("    for name in ['SelectedRoots.csproj','Probe.cs']:",'''    provenance=json.loads(pathlib.Path('/approved/input-provenance.json').read_text())
    for item in provenance['candidate_files']:
        data=(pathlib.Path('/work/input')/item['path']).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\\0'+data).hexdigest()==item['git_blob_sha1']
        assert hashlib.sha256(data).hexdigest()==item['sha256']
    (out/'verify.log').write_text('Both pinned candidate blobs verified before any build.\\n')
    for name in ['SelectedRoots.csproj','Probe.cs']:''')
runner=runner.replace('-p:CandidateSource=/work/cilica/artifacts/compatibility-candidates/circularbuffer-'+candidate,'-p:CandidateSource=/work/input').replace("'-culture','invariant'","'-parallel','none','-culture','invariant'")
(b/'runner.py').write_text(runner);shutil.copy(b/'runner.py',inputs/'runner.py')
for name in ['create.py','supervise.py']:
 text=(previous/name).read_text().replace('cilica-crc32-approved-20261008','cilica-circularbuffer-approved-20261008').replace('CILICA_APPROVED_CRC32_PIN','CILICA_APPROVED_CIRCULARBUFFER_PIN').replace('26c5a818a5c7a3d6a622c92d3cd08dba586c263c',candidate).replace('CILICA_CRC32_ASSEMBLY','CILICA_CIRCULARBUFFER_ASSEMBLY').replace('Crc32.SelectedRoots','CircularBuffer.SelectedRoots')
 if name=='create.py':text=text.replace("for k,v in env.items():", "env['CILICA_NATIVE_EVIDENCE_DIRECTORY']='/evidence/native-hashes'\nfor k,v in env.items():")
 (b/name).write_text(text)
d=['docker','--config',str(c/'docker-config'),'--host','unix:///Users/christian/.docker/run/docker.sock'];tool=proposal['tooling_image'];image=json.loads(subprocess.check_output(d+['image','inspect',tool]))[0];assert image['Id']==tool;(b/'tooling-image.json').write_text(json.dumps(image,indent=2)+'\n')
(inputs/'Dockerfile').write_text('FROM '+tool+'\nCOPY cilica/ /approved/cilica/\nCOPY offline-feed/ /approved/offline-feed/\nCOPY input/ /approved/input/\nCOPY input-provenance.json /approved/input-provenance.json\nCOPY runner.py /runner.py\nUSER 10001:10001\nENTRYPOINT ["python3", "-B", "/runner.py"]\n');shutil.copy(inputs/'Dockerfile',b/'Input.Dockerfile');shutil.copy(__file__,b/'prepare.py')
# Compile owned Python as syntax data only; no upstream build hooks.
for f in [b/'runner.py',b/'create.py',b/'supervise.py']:compile(f.read_text(),str(f),'exec')
print('Frozen export, hashed overlays, two candidate blobs and offline packages verified.',flush=True)
with (b/'input-image-build.log').open('w') as log:subprocess.run(d+['build','--network','none','--iidfile',str(c/'image-id'),'-t','cilica-circularbuffer-approved-20261008:input',str(inputs)],stdout=log,stderr=subprocess.STDOUT,check=True,timeout=180)
(b/'new-image-id.json').write_text(json.dumps({'id':(c/'image-id').read_text().strip()})+'\n')
subprocess.run(['python3',str(b/'create.py')],check=True,timeout=30)
subprocess.run(['python3',str(b/'supervise.py')],check=True,timeout=1810)
