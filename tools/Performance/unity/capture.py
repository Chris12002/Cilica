"""Copy generated C++ plus build records, with hashes and a 1 GiB export bound."""
import argparse,hashlib,json,pathlib,shutil
p=argparse.ArgumentParser();p.add_argument('--project',type=pathlib.Path,required=True);p.add_argument('--output',type=pathlib.Path,required=True);a=p.parse_args()
root=a.project.resolve();out=a.output.resolve();out.mkdir(parents=True,exist_ok=False)
rows=[];total=0
for sub in ['Library/Bee','Temp']:
 tree=root/sub
 if not tree.exists():continue
 for f in sorted(tree.rglob('*')):
  if not f.is_file() or f.is_symlink():continue
  if f.suffix.lower() not in {'.cpp','.h','.hpp','.rsp','.json','.txt','.log'}:continue
  if not f.resolve().is_relative_to(root):raise ValueError('Escaping source')
  size=f.stat().st_size
  if total+size>1024**3:raise RuntimeError('Export exceeds 1 GiB; partial output retained')
  dest=out/f.relative_to(root);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,dest);total+=size
  rows.append({'path':str(f.relative_to(root)),'bytes':size,'sha256':hashlib.sha256(dest.read_bytes()).hexdigest()})
cpp=[r for r in rows if r['path'].endswith('.cpp')]
hits=[]
for r in cpp:
 f=out/r['path']
 if b'BenchProbe' in f.read_bytes():hits.append(r['path'])
manifest={'files':rows,'bytes':total,'cpp_files':len(cpp),'benchmark_cpp':hits}
(out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
if not hits:raise SystemExit('No BenchProbe generated C++ found; capture is not a successful result')
print(json.dumps({k:v for k,v in manifest.items() if k!='files'}))
