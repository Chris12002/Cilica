from pathlib import Path
import subprocess,time,re,json,collections
out=Path(__file__).resolve().parent
methods={m['token']:m for m in json.loads((out/'core-methods.json').read_text())['methods']}
for name in ['base','DBT']:
 binary=out/(name+'-profile')
 subprocess.run(['clang++','-std=c++20','-O2','-g','-DNDEBUG','-fno-exceptions','-fno-rtti',str(out/(name+'.cpp')),'-o',str(binary)],check=True,timeout=240)
 with (out/(name+'-profile-run.jsonl')).open('w') as f:
  p=subprocess.Popen([str(binary),'measure','0','8192'],stdout=f,stderr=subprocess.PIPE,text=True)
  try:
   time.sleep(1)
   r=subprocess.run(['/usr/bin/sample',str(p.pid),'3','1','-file',str(out/(name+'-sample.txt'))],capture_output=True,text=True,timeout=15)
   (out/(name+'-sample.log')).write_text(r.stdout+r.stderr);r.check_returncode()
   _,err=p.communicate(timeout=60);assert p.returncode==0,err
  finally:
   if p.poll() is None:p.kill();p.wait()
 rows=[json.loads(x) for x in (out/(name+'-profile-run.jsonl')).read_text().splitlines()]
 assert len(rows)==8 and all(r['checksum']==6146*r['iterations'] for r in rows[1:])
 lines=(out/(name+'-sample.txt')).read_text().split('Call graph:',1)[1].split('Total number in stack',1)[0].splitlines()
 stack=[];groups=collections.Counter();symbols=collections.Counter()
 parsed=[]
 for line in lines:
  m=re.match(r'^([\s+!:|]+)(\d+) (.*)$',line)
  if m:parsed.append((len(m[1]),int(m[2]),m[3]))
 for i,(depth,count,label) in enumerate(parsed):
  children=0;j=i+1
  while j<len(parsed) and parsed[j][0]>depth:
   children+=parsed[j][1];childdepth=parsed[j][0];j+=1
   while j<len(parsed) and parsed[j][0]>childdepth:j+=1
  exclusive=count-children
  if exclusive<=0 or f'(in {name}-profile)' not in label:continue
  symbol=label.split('  (in ',1)[0].split(' (in ',1)[0].split('_objects_packed_closure::',1)[-1].split('(',1)[0]
  token=re.search(r'cilica_m_(\w{64})_(060[0-9a-f]{5})',symbol)
  if token:
   m=methods.get(token[2]) if token[1].startswith('10a994') else None
   key=(m['type']+'.'+m['name']) if m else token[1]+'/'+token[2]
  else:key=symbol.split(' + ',1)[0]
  groups[key]+=exclusive;symbols[label]+=exclusive
 summary={'application_exclusive_samples':sum(groups.values()),'groups':dict(groups.most_common()),'symbols':dict(symbols.most_common())}
 (out/(name+'-profile-summary.json')).write_text(json.dumps(summary,indent=2))
 print(name,sum(groups.values()),groups.most_common(22),flush=True)
