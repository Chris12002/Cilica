"""Stage exact pins; downloads only previously reviewed hash-verified text. Does not execute candidates."""
from pathlib import Path
import subprocess,json,hashlib,tarfile,shutil,urllib.request,argparse
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);p.add_argument('--package-cache',type=Path,action='append',required=True);a=p.parse_args()
repo=Path(__file__).resolve().parents[3]
old=repo/'docs/compatibility-candidates/diffplex-performance-20261008/diffplex-benchmark-retry-20261008'
b=a.output.resolve();b.mkdir(parents=True,exist_ok=True);c=b/'inputs';c.mkdir(exist_ok=False)
pin='3d0bb53e94b5356cbbe59fae3c2ebca93af68fc8';harness='a581334811b8f303b0869079589e0e3ce528dfb3'
archive=b/'cilica.tar';subprocess.run(['git','archive','--format=tar','--output',str(archive),pin],cwd=repo,check=True);(c/'cilica').mkdir()
with tarfile.open(archive) as t:
 for m in t.getmembers():assert (m.isfile() or m.isdir()) and not Path(m.name).is_absolute() and '..' not in Path(m.name).parts
 t.extractall(c/'cilica')
paths=subprocess.check_output(['git','ls-tree','-r','--name-only',harness,'tools/CompatibilityCandidates/diffplex-benchmark','tests/Cilica.Metadata.Tests/ApprovedDiffPlexBenchmarkEmissionTests.cs'],cwd=repo,text=True).splitlines()
for path in paths:
 data=subprocess.check_output(['git','show',harness+':'+path],cwd=repo);dest=c/'cilica'/path;assert not dest.exists();dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
manifest=json.loads((old/'input-provenance.json').read_text());feed=c/'offline-feed';feed.mkdir()
for p in manifest['packages']:
 name,version=p['package'].lower().split('/');filename=name+'.'+version+'.nupkg';sources=[root/relative for root in a.package_cache for relative in [Path(filename),Path(name)/version/filename]]
 source=next((q for q in sources if q.exists()),None)
 if source is None:raise FileNotFoundError(filename)
 data=source.read_bytes();assert hashlib.sha256(data).hexdigest()==p['sha256'];(feed/filename).write_bytes(data)
contents={}
for f in manifest['candidate_files']:
 u='https://raw.githubusercontent.com/mmanela/diffplex/'+manifest['candidate_commit']+'/'+f['path']
 with urllib.request.urlopen(u,timeout=30) as response:data=response.read()
 assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==f['git_blob_sha1'];contents[f['path']]=data.decode()
(c/'input').mkdir();(c/'input/reviewed.json').write_text(json.dumps(contents,ensure_ascii=True))
manifest['profiling_tools_sha256']={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (repo/'tools/Performance/diffplex').glob('*.py')}
(c/'input-provenance.json').write_text(json.dumps(manifest,indent=2));shutil.copy(c/'input-provenance.json',b/'input-provenance.json')
shutil.copytree(repo/'tools/Performance/diffplex',c/'profiling',ignore=shutil.ignore_patterns('__pycache__'))
rows=json.loads((old/'measurement-progress.json').read_text())
expected=next(row['records'][0] for row in rows if row['name']=='dotnet-verify-0')
(c/'expected-transcript.json').write_text(json.dumps(expected)+'\n')
runner=(repo/'tools/Performance/diffplex/runner.py').read_text()
(c/'runner.py').write_text(runner);(b/'runner.py').write_text(runner)
tool='sha256:68b78e3c1c62c8ed6db8421d6f11cd0619829bd8b77ad1368cae9328e3d40f80'
(c/'Dockerfile').write_text('FROM '+tool+'\nCOPY cilica/ /approved/cilica/\nCOPY offline-feed/ /approved/offline-feed/\nCOPY input/ /approved/input/\nCOPY profiling/ /approved/profiling/\nCOPY expected-transcript.json /approved/expected-transcript.json\nCOPY input-provenance.json /approved/input-provenance.json\nCOPY runner.py /runner.py\nUSER 10001:10001\nENTRYPOINT ["python3", "-B", "/runner.py"]\n')
for name in ['create.py','supervise.py']:
 txt=(repo/'tools/Performance/diffplex'/name).read_text()
 (b/name).write_text(txt)
print('Prepared pinned local source, hash-verified candidate texts and offline packages.')
