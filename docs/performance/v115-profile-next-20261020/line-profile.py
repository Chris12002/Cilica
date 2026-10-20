from pathlib import Path
import subprocess,time,re,json,collections,hashlib,os,statistics
out=Path(__file__).resolve().parent
expected=json.loads(Path('/Users/christian/Cilica/artifacts/leaf-getter-performance-20261020/native/expected.json').read_text())
methods={m['token']:m for m in json.loads((out/'core-methods.json').read_text())['methods']}
app={m['token']:m for m in json.loads(Path('/Users/christian/Cilica/artifacts/roslyn-il-investigation-20261019/methods.json').read_text())['methods']}
corehash=hashlib.sha256((out/'baseline-core/Cilica.Core.dll').read_bytes()).hexdigest()
assert corehash == '655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1', 'Regenerate metadata map for a changed core'
assert (out/'baseline.cpp').read_bytes()==(out.parent/'v115-collector-20261020/v115.cpp').read_bytes()
for repeat in range(2):
 with (out/f'line-profile-run-{repeat}.jsonl').open('w') as f:
  p=subprocess.Popen([str(out/'profile'),'measure','1','16384'],stdout=f,stderr=subprocess.PIPE,text=True)
  try:
   time.sleep(1)
   r=subprocess.run(['/usr/bin/sample',str(p.pid),'3','1','-file',str(out/f'line-sample-{repeat}.txt')],capture_output=True,text=True,timeout=15)
   (out/f'line-sample-{repeat}.log').write_text(r.stdout+r.stderr);r.check_returncode()
   _,err=p.communicate(timeout=60);assert p.returncode==0,err
  finally:
   if p.poll() is None:p.kill();p.wait()
 rows=[json.loads(x) for x in (out/f'line-profile-run-{repeat}.jsonl').read_text().splitlines()]
 assert len(rows)==8 and all(r['checksum']==3122*r['iterations'] for r in rows[1:])
 lines=(out/f'line-sample-{repeat}.txt').read_text().split('Call graph:',1)[1].split('Total number in stack',1)[0].splitlines()
 groups=collections.Counter();symbols=collections.Counter();allgroups=collections.Counter();inclusive=collections.Counter();parsed=[]
 for line in lines:
  m=re.match(r'^([\s+!:|]+)(\d+) (.*)$',line)
  if m:parsed.append((len(m[1]),int(m[2]),m[3]))
 for i,(depth,count,label) in enumerate(parsed):
  children=0;j=i+1
  while j<len(parsed) and parsed[j][0]>depth:
   children+=parsed[j][1];childdepth=parsed[j][0];j+=1
   while j<len(parsed) and parsed[j][0]>childdepth:j+=1
  exclusive=count-children
  isapp='(in profile)' in label
  symbol=label.split('  (in ',1)[0].split(' (in ',1)[0].split('_objects_packed_closure::',1)[-1].split('(',1)[0]
  token=re.search(r'cilica_m_(\w{64})_(060[0-9a-f]{5})',symbol)
  if token:
   m=(methods if token[1]==corehash else app).get(token[2]);key=(m['type']+'.'+m['name']) if m else token[1]+'/'+token[2]
  else:key=symbol.split(' + ',1)[0]
  if isapp:inclusive[key]+=count
  if exclusive<=0:continue
  allgroups[key]+=exclusive
  if isapp:groups[key]+=exclusive;symbols[label]+=exclusive
 data={'application_exclusive_samples':sum(groups.values()),'all_exclusive_samples':sum(allgroups.values()),'groups':dict(groups.most_common()),'all_groups':dict(allgroups.most_common()),'inclusive_not_additive':dict(inclusive.most_common()),'symbols':dict(symbols.most_common())}
 (out/f'line-profile-summary-{repeat}.json').write_text(json.dumps(data,indent=2));print('profile',repeat,sum(groups.values()),groups.most_common(20),flush=True)
