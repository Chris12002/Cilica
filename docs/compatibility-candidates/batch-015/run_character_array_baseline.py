import subprocess,pathlib,json,time,os
r=pathlib.Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica');d=r/'artifacts/compatibility-batch-015';d.mkdir(exist_ok=True); started=time.monotonic()
steps=[['dotnet','build','tests/Cilica.Metadata.Tests/Cilica.Metadata.Tests.csproj','--artifacts-path',str(d/'build'),'-p:CustomBeforeDirectoryBuildProps='+str(r/'tools/CompatibilityCandidates/isolated-build.props'),'-p:UseSharedCompilation=false','-nodeReuse:false','-m:1']]
steps.append(['dotnet',str(d/'build/bin/_Users_christian_.codex_worktrees_alchemy-replay-baseline_Cilica_tests_Cilica.Metadata.Tests/debug/Cilica.Metadata.Tests.dll'),'-method','Cilica.Metadata.Tests.CandidateCharacterArrayTests.Copies_payload_independence_empty_identity_and_faults_match_dotnet','-parallel','none','-culture','invariant','-noColor'])
records=[]
for name,command in zip(['baseline-build','baseline-tests'],steps):
 with (d/(name+'.log')).open('w') as f:code=subprocess.run(command,cwd=r,stdout=f,stderr=subprocess.STDOUT,timeout=300).returncode
 records.append({'name':name,'argv':command,'exit_code':code});print(name,code,flush=True)
 if code:break
(d/'baseline-run.json').write_text(json.dumps({'steps':records,'elapsed_seconds':time.monotonic()-started},indent=2)+'\n')
