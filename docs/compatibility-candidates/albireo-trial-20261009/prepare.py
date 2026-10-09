from pathlib import Path
import subprocess,json,hashlib,shutil,tarfile,urllib.request
repo=Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica');base=Path('/Users/christian/Documents/Codex/2026-10-08/task/albireo-approved-20261009');base.mkdir(exist_ok=True)
c=Path('/tmp/cilica-albireo-approved-20261009-r1');c.mkdir();(c/'docker-config').mkdir();(c/'docker-config/config.json').write_text('{}\n');inputs=c/'inputs';inputs.mkdir()
proposal=json.loads((repo/'docs/compatibility-candidates/albireo-decode-proposal.json').read_text());pin=proposal['cilica_commit'];archive=c/'cilica.tar'
subprocess.run(['git','archive','--format=tar','--output',str(archive),pin],cwd=repo,check=True);(inputs/'cilica').mkdir()
with tarfile.open(archive) as t:
 for m in t.getmembers():assert (m.isfile() or m.isdir()) and not Path(m.name).is_absolute() and '..' not in Path(m.name).parts
 t.extractall(inputs/'cilica')
for path,sha in proposal['owned_overlay_sha256'].items():
 data=(repo/path).read_bytes();assert hashlib.sha256(data).hexdigest()==sha,path
 dest=inputs/'cilica'/path;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
shutil.copyfile(inputs/'cilica/tools/CompatibilityCandidates/albireo-validation/ApprovedAlbireoExecutionTests.cs',inputs/'cilica/tests/Cilica.Metadata.Tests/ApprovedAlbireoExecutionTests.cs')
packages=json.loads((repo/'tools/CompatibilityCandidates/albireo-validation/offline-packages.json').read_text())['packages'];feed=inputs/'offline-feed';feed.mkdir()
for p in packages:
 name,version=p['package'].lower().split('/');filename=name+'.'+version+'.nupkg';source=Path('/Users/christian/.nuget/packages')/name/version/filename
 if not source.exists():source=Path('/tmp/cilica-diffplex-retry-20261008')/filename
 data=source.read_bytes();assert hashlib.sha256(data).hexdigest()==p['sha256'],filename;(feed/filename).write_bytes(data)
source_rows=[]
for f in proposal['candidate_files']:
 url='https://raw.githubusercontent.com/kappa7194/base32/'+proposal['candidate_commit']+'/'+f['path']
 with urllib.request.urlopen(url,timeout=45) as response:data=response.read(1048576)
 assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==f['git_blob_sha1'],f['path']
 dest=inputs/'input'/f['path'];dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data);source_rows.append(dict(f,sha256=hashlib.sha256(data).hexdigest(),bytes=len(data)))
shutil.copyfile(inputs/'input/LICENSE.txt',base/'LICENSE.Albireo.txt')
manifest={'cilica_commit':pin,'candidate_commit':proposal['candidate_commit'],'candidate_files':source_rows,'owned_overlay_sha256':proposal['owned_overlay_sha256'],'packages':packages,'archive_sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'adapter_commit':'dbfcca2','approval':'Parent relayed exact human yes to isolated and normal-Mac Unity stages; receipt approval.txt. No other candidate or future execution approved.'}
(base/'input-provenance.json').write_text(json.dumps(manifest,indent=2)+'\n');shutil.copyfile(base/'input-provenance.json',inputs/'input-provenance.json');shutil.copyfile(repo/'docs/compatibility-candidates/albireo-decode-proposal.json',base/'frozen-isolated-proposal.json');shutil.copyfile(repo/'docs/compatibility-candidates/albireo-unity-plan.json',base/'frozen-unity-plan.json')
shutil.copyfile(__file__,base/'prepare.py');print(base);print('Verified two source blobs, five overlays and',len(packages),'cached packages. No candidate execution.')
