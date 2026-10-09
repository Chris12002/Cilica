from pathlib import Path
import json,hashlib,importlib.util
b=Path(__file__).parent;u=Path('/Users/christian/Documents/Codex/2026-10-08/task-3/albireo-unity-trial-20261009');r=Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica')
spec=importlib.util.spec_from_file_location('transcript',r/'tools/CompatibilityCandidates/albireo-unity/transcript.py');tr=importlib.util.module_from_spec(spec);spec.loader.exec_module(tr)
expected=json.loads((b/'handoff/expected.json').read_text());actual=json.loads((u/'verify/result.json').read_text());assert not actual.get('error');assert actual['candidateCommit']==expected['candidateCommit'] and actual['inputFingerprint']==expected['inputFingerprint'];assert actual['checksum']==tr.checksum(actual['values'])
def group(v):
 assert len(v)%3==0;rows=[v[i:i+3] for i in range(0,len(v),3)];tr.normalize(rows,expected['inputFingerprint']);groups={}
 for s,i,x in rows:groups.setdefault(s,{})[i]=x
 return groups
left,right=group(expected['values']),group(actual['values']);differences=[];matched=[]
def summary(v):
 status,length,identity,msg,param,source=[v[i] for i in range(6)];offset=6+max(length,0)
 return {'status':status,'length':length,'result_identity':identity,'message':''.join(chr(v[i]) for i in range(offset,offset+msg)),'parameter':''.join(chr(v[i]) for i in range(offset+msg,offset+msg+param)),'bytes':[v[i] for i in range(6,6+max(length,0))]}
for s in range(32):
 if left[s]==right[s]:matched.append(s)
 else:differences.append({'scenario':s,'dotnet_and_cilica':summary(left[s]),'unity':summary(right[s])})
assert [x['scenario'] for x in differences]==[0,17]
record={'review':'Independent data-only exact transcript comparison; no candidate invoked','input_fingerprint':expected['inputFingerprint'],'unity_player_sha256':hashlib.sha256((u/'verify/result.json').read_bytes()).hexdigest(),'expected_sha256':hashlib.sha256((b/'handoff/expected.json').read_bytes()).hexdigest(),'matched_scenarios':matched,'differences':differences,'decision':'Keep Unity correctness failed. Preserve existing .NET-compatible Cilica behavior. Same input identity; this is semantic divergence, not captured-unmatched input. No speed equivalence or normalization.','production_change_required':False}
if (u/'profile/result.json').exists():
 prof=json.loads((u/'profile/result.json').read_text());assert prof['inputFingerprint']==expected['inputFingerprint'];g=group(prof['values']);assert all(g[s]==left[s] for s in [2,8,29,31]);record['profile_selected_cases_match']=True;record['profile_qualification']='Selected-case output match alone does not prove raw marker capture or make full correctness pass.'
(b/'unity-transcript-independent-review.json').write_text(json.dumps(record,indent=2,ensure_ascii=True)+'\n');print(json.dumps(record,indent=2))
