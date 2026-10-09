from pathlib import Path
import json,hashlib,shutil,importlib.util
b=Path(__file__).parent;r=Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica');h=b/'handoff';h.mkdir(exist_ok=False)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
m=json.loads((b/'workflow-manifest-draft.json').read_text());result=json.loads((b/'evidence/result.json').read_text());assert result['status']=='passed'
dll=b/'evidence/Albireo.SelectedDecode.dll';digest=sha(dll);assert digest==(b/'evidence/candidate-assembly.sha256').read_text().split()[0];assert digest==json.loads((b/'evidence-sha256.json').read_text())['Albireo.SelectedDecode.dll']
for name in ('Albireo.SelectedDecode.dll','oracle-observations.json','result.json','LICENSE.Albireo.txt'):shutil.copyfile(b/'evidence'/name,h/name)
source=Path('/tmp/cilica-albireo-approved-20261009-r1/inputs/input/Albireo.Base32/Base32.cs');data=source.read_bytes();assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()=='bd6584e246151e3e493ed1eb9f17de269cb1ddc1';shutil.copyfile(source,h/'Base32.cs')
for name in ('input-provenance.json','input-cases.json','approval.txt','execution-boundaries.json'):shutil.copyfile(b/name,h/name)
m['scope']['candidate_assemblies']={'Albireo.SelectedDecode.dll':digest};m['unity'].update(candidate_assemblies=dict(m['scope']['candidate_assemblies']),owned_inputs=dict(m['scope']['owned_inputs']),reference_commit=m['candidate']['commit'],reference_roots=list(m['scope']['roots']),reference_input_cases=dict(m['scope']['input_cases']),blocker='Approved exact-input handoff ready; Unity build/capture/correctness/profile pending.')
def load(name,path):
 spec=importlib.util.spec_from_file_location(name,path);mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod);return mod
wf=load('wf',r/'tools/CompatibilityCandidates/reference_workflow.py');tr=load('transcript',r/'tools/CompatibilityCandidates/albireo-unity/transcript.py');fp=wf.fingerprint(m);assert fp==wf.unity_fingerprint(m)
rows=json.loads((h/'oracle-observations.json').read_text());assert len(rows)==851
(h/'expected.json').write_text(json.dumps(tr.normalize(rows,fp),indent=2)+'\n')
(h/'workflow-manifest.json').write_text(json.dumps(m,indent=2)+'\n')
receipt={'status':'verified-ready-for-approved-unity','candidate_commit':m['candidate']['commit'],'cilica_commit':'b8f462870351be165692401d90340c019fadc3ae','adapter_commit':'dbfcca2deac3b941e0a857afaf92eab68a04c7d3','input_fingerprint':fp,'candidate_dll_sha256':digest,'oracle_observations':851,'scenarios':32,'isolated_status':'passed','isolated_elapsed_seconds':result['elapsed_seconds'],'container_removed':True,'files':{p.name:sha(p) for p in sorted(h.iterdir()) if p.is_file()},'owned_inputs':m['scope']['owned_inputs'],'boundary':'Normal-account Mac Unity authorized only for this exact candidate plan. No additional packages or candidates; Decode-only execution. Diagnostic profile, no speed claim.'}
(h/'handoff.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps({k:v for k,v in receipt.items() if k not in ('files','owned_inputs')},indent=2))
