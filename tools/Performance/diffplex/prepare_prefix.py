"""Stage lazy-prefix experiments locally; no candidate execution or downloads."""
from pathlib import Path
import argparse,hashlib,json,shutil,subprocess,tarfile
from lazy_prefix import SOURCE_SHA
p=argparse.ArgumentParser();p.add_argument('mode',choices=['correctness','regressions','timing','memory']);p.add_argument('--output',type=Path,required=True);p.add_argument('--source',type=Path);p.add_argument('--timing-evidence',type=Path);p.add_argument('--package-cache',type=Path,action='append',default=[]);a=p.parse_args()
scripts=Path(__file__).resolve().parent;repo=scripts.parents[2];b=a.output.resolve();b.mkdir(parents=True,exist_ok=True);inputs=b/'inputs';inputs.mkdir();(b/'docker-config').mkdir(exist_ok=True)
runner={'correctness':'runner_prefix.py','regressions':'runner_prefix_regressions.py','timing':'runner_prefix_timing.py','memory':'runner_prefix_memory.py'}[a.mode]
for name in ['transform.py','lazy_prefix.py',runner]:shutil.copy(scripts/name,inputs/name)
if a.mode=='regressions':
    archive=b/'cilica.tar';subprocess.run(['git','archive','--format=tar','--output',str(archive),'3d0bb53e94b5356cbbe59fae3c2ebca93af68fc8'],cwd=repo,check=True)
    tree=inputs/'cilica';tree.mkdir()
    with tarfile.open(archive) as t:
        for m in t.getmembers():
            path=Path(m.name);assert (m.isfile() or m.isdir()) and not path.is_absolute() and '..' not in path.parts
        t.extractall(tree)
    manifest=json.loads((repo/'docs/compatibility-candidates/diffplex-performance-20261008/diffplex-benchmark-retry-20261008/input-provenance.json').read_text())
    feed=inputs/'offline-feed';feed.mkdir()
    for package in manifest['packages']:
        name,version=package['package'].lower().split('/');filename=name+'.'+version+'.nupkg'
        candidates=[root/rel for root in a.package_cache for rel in [Path(filename),Path(name)/version/filename]]
        source=next((q for q in candidates if q.exists()),None)
        if source is None:raise FileNotFoundError(filename)
        data=source.read_bytes();assert hashlib.sha256(data).hexdigest()==package['sha256'];(feed/filename).write_bytes(data)
else:
    if a.source is None:p.error('--source requires the retained pinned original native-bench.cpp')
    assert hashlib.sha256(a.source.read_bytes()).hexdigest()==SOURCE_SHA
    shutil.copy(a.source,inputs/'native-bench.cpp')
    shutil.copy(repo/'docs/performance/diffplex-20261008/expected-transcript.json',inputs/'expected-transcript.json')
    for name in (['prefix_cases.cpp','prefix_diagnostics.py'] if a.mode=='correctness' else ['measure.py'] if a.mode=='timing' else []):shutil.copy(scripts/name,inputs/name)
    if a.mode=='memory':
        if a.timing_evidence is None:p.error('--timing-evidence requires the original clean hashes')
        for name in ['baseline-hashes.json','prefix-hashes.json']:shutil.copy(a.timing_evidence/name,inputs/name)
for name in ['create.py','supervise.py']:
    (b/name).write_text((scripts/name).read_text().replace('cilica-performance-fingerprint-20261008','cilica-prefix-'+a.mode+'-20261008'))
(inputs/'Dockerfile').write_text('FROM sha256:68b78e3c1c62c8ed6db8421d6f11cd0619829bd8b77ad1368cae9328e3d40f80\nCOPY . /approved/\nUSER 10001:10001\nENTRYPOINT ["python3","-B","/approved/'+runner+'"]\n')
(b/'input-sha256.json').write_text(json.dumps({str(q.relative_to(inputs)):hashlib.sha256(q.read_bytes()).hexdigest() for q in inputs.rglob('*') if q.is_file()},indent=2))
print('Prepared',a.mode,'without execution:',b)
