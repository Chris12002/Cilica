from pathlib import Path
import subprocess,json,hashlib,shutil,tarfile,urllib.request
r=Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica');b=r/'artifacts/crc32-approved-20261008';b.mkdir(exist_ok=True)
previous=r/'artifacts/diffplex-int32-20261008'
pin='c64312e6993a5bce4078cb23961e737cd1df8e24';overlay='6068708';candidate='26c5a818a5c7a3d6a622c92d3cd08dba586c263c'
c=Path('/tmp/cilica-crc32-approved-20261008');c.mkdir();(c/'docker-config').mkdir();(c/'docker-config/config.json').write_text('{}\n');inputs=c/'inputs';inputs.mkdir();archive=c/'cilica.tar'
subprocess.run(['git','archive','--format=tar','--output',str(archive),pin],cwd=r,check=True);(inputs/'cilica').mkdir()
with tarfile.open(archive) as t:
 for m in t.getmembers():assert (m.isfile() or m.isdir()) and not Path(m.name).is_absolute() and '..' not in Path(m.name).parts
 t.extractall(inputs/'cilica')
proposal=json.loads((r/'docs/compatibility-candidates/crc32-execution-proposal.json').read_text())
for path,sha in proposal['owned_overlay_sha256'].items():
 data=subprocess.check_output(['git','show',overlay+':'+path],cwd=r);assert hashlib.sha256(data).hexdigest()==sha
 dest=inputs/'cilica'/path;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
shutil.copy(inputs/'cilica/tools/CompatibilityCandidates/crc32-validation/ApprovedCrc32ExecutionTests.cs',inputs/'cilica/tests/Cilica.Metadata.Tests/ApprovedCrc32ExecutionTests.cs')
original=json.loads((r/'artifacts/diffplex-approved-20261008/input-provenance.json').read_text());item=next(x for x in json.loads((r/'docs/compatibility-candidates/batch-008.json').read_text())['candidates'] if x['id']=='crc32-net')
manifest={'cilica_commit':pin,'overlay_commit':overlay,'owned_overlay_sha256':proposal['owned_overlay_sha256'],'candidate_commit':candidate,'candidate_files':item['files'],'packages':original['packages'],'archive_sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'approval':'User approval relayed by parent; exact bounded proposal 6068708; correctness only'}
feed=inputs/'offline-feed';feed.mkdir()
for p in manifest['packages']:
 name,version=p['package'].lower().split('/');filename=name+'.'+version+'.nupkg';source=Path('/tmp/cilica-diffplex-retry-20261008')/filename
 if not source.exists():source=Path('/Users/christian/.nuget/packages')/name/version/filename
 data=source.read_bytes();assert hashlib.sha256(data).hexdigest()==p['sha256'];(feed/filename).write_bytes(data)
contents={}
for f in item['files']:
 with urllib.request.urlopen('https://raw.githubusercontent.com/force-net/Crc32.NET/'+candidate+'/'+f['path'],timeout=45) as response:data=response.read()
 assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==f['git_blob_sha1'];contents[f['path']]=data.decode()
(inputs/'input').mkdir();(inputs/'input/reviewed.json').write_text(json.dumps(contents,ensure_ascii=True));(b/'Crc32-LICENSE').write_text(contents['LICENSE']);(b/'input-provenance.json').write_text(json.dumps(manifest,indent=2)+'\n');shutil.copy(b/'input-provenance.json',inputs/'input-provenance.json')
runner=(previous/'runner.py').read_text().replace('diffplex','crc32-net').replace('batch-006','batch-008').replace('crc32-net-validation','crc32-validation').replace('crc32-net-f500e73f28e28813f942f1f16dec5a34b40e48e3','crc32-net-'+candidate).replace('DiffPlex.SelectedRoots','Crc32.SelectedRoots').replace('ApprovedDiffPlexExecutionTests','ApprovedCrc32ExecutionTests')
runner='\n'.join(line for line in runner.splitlines() if "run('focused-regressions'" not in line)+'\n'
(b/'runner.py').write_text(runner);shutil.copy(b/'runner.py',inputs/'runner.py')
for name in ['create.py','supervise.py']:
 text=(previous/name).read_text().replace('cilica-diffplex-int32-20261008','cilica-crc32-approved-20261008').replace("str(b/'docker-config')","'/tmp/cilica-crc32-approved-20261008/docker-config'").replace("str(base/'docker-config')","'/tmp/cilica-crc32-approved-20261008/docker-config'").replace('CILICA_APPROVED_DIFFPLEX_PIN','CILICA_APPROVED_CRC32_PIN').replace('f500e73f28e28813f942f1f16dec5a34b40e48e3',candidate).replace('CILICA_DIFFPLEX_ASSEMBLY','CILICA_CRC32_ASSEMBLY').replace('DiffPlex.SelectedRoots','Crc32.SelectedRoots')
 (b/name).write_text(text)
d=['docker','--config',str(c/'docker-config'),'--host','unix:///Users/christian/.docker/run/docker.sock'];tool='sha256:68b78e3c1c62c8ed6db8421d6f11cd0619829bd8b77ad1368cae9328e3d40f80';image=json.loads(subprocess.check_output(d+['image','inspect',tool]))[0];assert image['Id']==tool;(b/'tooling-image.json').write_text(json.dumps(image,indent=2)+'\n')
(inputs/'Dockerfile').write_text('FROM '+tool+'\nCOPY cilica/ /approved/cilica/\nCOPY offline-feed/ /approved/offline-feed/\nCOPY input/ /approved/input/\nCOPY input-provenance.json /approved/input-provenance.json\nCOPY runner.py /runner.py\nUSER 10001:10001\nENTRYPOINT ["python3", "-B", "/runner.py"]\n');shutil.copy(inputs/'Dockerfile',b/'Input.Dockerfile');shutil.copy(__file__,b/'prepare.py')
print('Frozen export, three overlays, ten candidate blobs and offline packages verified.')
with (b/'input-image-build.log').open('w') as log:subprocess.run(d+['build','--network','none','--iidfile',str(c/'image-id'),'-t','cilica-crc32-approved-20261008:input',str(inputs)],stdout=log,stderr=subprocess.STDOUT,check=True)
(b/'new-image-id.json').write_text(json.dumps({'id':(c/'image-id').read_text().strip()})+'\n')
subprocess.run(['python3',str(b/'create.py')],check=True)
subprocess.run(['python3',str(b/'supervise.py')],check=True)
