from pathlib import Path
import subprocess,json,time,hashlib
root=Path(__file__).parent;dest=root/'preflight-reviewed';dest.mkdir(exist_ok=False)
(dest/'ProbeSignatureOnly.cs').write_bytes((root/'preflight-final/ProbeSignatureOnly.cs').read_bytes())
rows=[]
for name in ('player-development','player-release','editor'):
    rsp=(root/'preflight-final'/f'{name}.rsp').read_text().replace('/preflight-final/','/preflight-reviewed/')
    if name=='player-development':rsp=rsp.replace('DEVELOPMENT_BUILD','DEVELOPMENT_BUILD,ENABLE_PROFILER')
    (dest/f'{name}.rsp').write_text(rsp)
    cmd=['/Users/christian/.dotnet/dotnet','/Users/christian/.dotnet/sdk/10.0.400/Roslyn/bincore/csc.dll','/noconfig','@'+str(dest/f'{name}.rsp')]
    start=time.monotonic();r=subprocess.run(cmd,capture_output=True,text=True,timeout=30)
    (dest/f'{name}.log').write_text(r.stdout+r.stderr)
    rows.append(dict(name=name,argv=cmd,exit_code=r.returncode,seconds=time.monotonic()-start,compiler_only=True,candidate_input='Owned throwing signature stub; no upstream source or DLL',source_sha256={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [root/'template/ProjectData~/Assets/AlbireoUnityHarness.cs',root/'template/ProjectData~/Assets/Editor/AlbireoReferenceBuild.cs',dest/'ProbeSignatureOnly.cs']}))
(dest/'results.json').write_text(json.dumps(rows,indent=2)+'\n')
print([(x['name'],x['exit_code']) for x in rows]);assert all(x['exit_code']==0 for x in rows)
