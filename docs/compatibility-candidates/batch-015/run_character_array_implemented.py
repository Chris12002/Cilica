import subprocess,pathlib,json,time,os
r=pathlib.Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica');d=r/'artifacts/compatibility-batch-015';d.mkdir(exist_ok=True); started=time.monotonic()
steps=[['dotnet','build','tests/Cilica.Metadata.Tests/Cilica.Metadata.Tests.csproj','--artifacts-path',str(d/'build'),'-p:CustomBeforeDirectoryBuildProps='+str(r/'tools/CompatibilityCandidates/isolated-build.props'),'-p:UseSharedCompilation=false','-nodeReuse:false','-m:1']]
steps.append(['dotnet',str(d/'build/bin/_Users_christian_.codex_worktrees_alchemy-replay-baseline_Cilica_tests_Cilica.Metadata.Tests/debug/Cilica.Metadata.Tests.dll'),'-class','Cilica.Metadata.Tests.CandidateCharacterArrayTests','-class','Cilica.Metadata.Tests.CandidateTrimSetTests','-class','Cilica.Metadata.Tests.CharacterStringTests','-class','Cilica.Metadata.Tests.EmptyArrayLibraryTests','-parallel','none','-culture','invariant','-noColor'])
os.environ['CILICA_NATIVE_EVIDENCE_DIRECTORY']=str(d/'native-evidence');(d/'native-evidence').mkdir(exist_ok=True)
records=[]
for name,command in zip(['implemented-build','implemented-tests'],steps):
 with (d/(name+'.log')).open('w') as f:code=subprocess.run(command,cwd=r,stdout=f,stderr=subprocess.STDOUT,timeout=600).returncode
 records.append({'name':name,'argv':command,'exit_code':code});print(name,code,flush=True)
 if code:break
(d/'implemented-run.json').write_text(json.dumps({'steps':records,'elapsed_seconds':time.monotonic()-started},indent=2)+'\n')
