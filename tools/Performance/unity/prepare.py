"""Stage reviewed DiffPlex inputs and a local Unity project template; no Unity launch."""
import argparse, hashlib, json, pathlib, shutil, subprocess, urllib.request, xml.etree.ElementTree as ET
p=argparse.ArgumentParser();p.add_argument('--work',type=pathlib.Path,required=True);p.add_argument('--package-cache',type=pathlib.Path,required=True);a=p.parse_args()
repo=pathlib.Path(__file__).resolve().parents[3];work=a.work.resolve();work.mkdir(parents=True,exist_ok=True)
if (work/'inputs').exists():raise SystemExit('Refusing to replace existing inputs; choose a fresh work directory.')
inputs=work/'inputs';inputs.mkdir();out=work/'evidence';out.mkdir(exist_ok=True)
old=repo/'docs/compatibility-candidates/diffplex-performance-20261008/diffplex-benchmark-retry-20261008'
provenance=json.loads((old/'input-provenance.json').read_text());pins={r['path']:r for r in provenance['candidate_files']}
harness='a581334811b8f303b0869079589e0e3ce528dfb3'
recipe=inputs/'recipe';recipe.mkdir();source=inputs/'candidate';source.mkdir()
owned={}
for name in ['SelectedRoots.csproj','BenchProbe.cs','DotNetHost.cs','DotNetHost.csproj']:
 rel='tools/CompatibilityCandidates/diffplex-benchmark/'+name
 data=subprocess.check_output(['git','show',harness+':'+rel],cwd=repo)
 (recipe/name).write_bytes(data);owned[name]=hashlib.sha256(data).hexdigest()
selected=[n.attrib['Include'].replace('$(CandidateSource)/','') for n in ET.fromstring((recipe/'SelectedRoots.csproj').read_text()).iter('Compile') if '$(CandidateSource)' in n.attrib['Include']]
if len(selected)!=14:raise ValueError('Unexpected source count')
receipts=[]
for name in selected+['License.txt']:
 expected=pins[name];url='https://raw.githubusercontent.com/mmanela/diffplex/'+provenance['candidate_commit']+'/'+name
 with urllib.request.urlopen(url,timeout=30) as r:data=r.read()
 if hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()!=expected['git_blob_sha1']:raise ValueError(name)
 dest=source/name;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
 receipts.append({'path':name,'url':url,'git_blob_sha1':expected['git_blob_sha1'],'sha256':hashlib.sha256(data).hexdigest()})
feed=inputs/'feed';feed.mkdir();pkg=a.package_cache/'netstandard.library/2.0.3/netstandard.library.2.0.3.nupkg'
if hashlib.sha256(pkg.read_bytes()).hexdigest()!='3eb87644f79bcffb3c0331dbdac3c7837265f2cdf58a7bfd93e431776f77c9ba':raise ValueError('Reference package hash')
shutil.copy2(pkg,feed/pkg.name)
# Host-only reference path adaptation; the candidate and BenchProbe remain unchanged.
host=(recipe/'DotNetHost.csproj').read_text().replace('/work/out/DiffPlex.Benchmark.dll',str(inputs/'dll/DiffPlex.Benchmark.dll'))
(recipe/'DotNetHost.csproj').write_text(host)
shutil.copytree(pathlib.Path(__file__).parent/'template',work/'template')
rows=json.loads((old/'measurement-progress.json').read_text());expected=next(r['records'][0] for r in rows if r['name']=='dotnet-verify-0')
if len(expected['values'])!=523 or expected['checksum']!=6146:raise ValueError('Historical transcript')
(out/'expected.json').write_text(json.dumps(expected,indent=2)+'\n')
receipt={'candidate_commit':provenance['candidate_commit'],'harness_commit':harness,'sources':receipts,'original_owned_sha256':owned,'adapted_host_project_sha256':hashlib.sha256(host.encode()).hexdigest(),'historical_benchmark_dll_sha256':'1da7fb72d688a9b346fdb52318d9683a2bd861bd4d09dcd3eca933c6da0368bb','historical_dll_hash_is_not_assumed_for_this_rebuild':True}
(out/'input-provenance.json').write_text(json.dumps(receipt,indent=2)+'\n')
print('Staged 14 unchanged source files, license, pinned adapter and 523 expected observations:',work)
