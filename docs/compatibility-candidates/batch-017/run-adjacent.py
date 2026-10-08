import pathlib,subprocess,json,time,os
r=pathlib.Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica');d=r/'artifacts/compatibility-batch-017';started=time.monotonic()
command=['dotnet',str(d/'build/bin/_Users_christian_.codex_worktrees_alchemy-replay-baseline_Cilica_tests_Cilica.Metadata.Tests/debug/Cilica.Metadata.Tests.dll')]
for name in ('ArrayEnumerationTests','NarrowEnumerationTests','ObjectArrayEnumerationValidationTests','LinqPredicateTests'):command+=['-class','Cilica.Metadata.Tests.'+name]
command+=['-parallel','none','-culture','invariant','-noColor']
os.environ['CILICA_NATIVE_EVIDENCE_DIRECTORY']=str(d/'native-evidence-adjacent');(d/'native-evidence-adjacent').mkdir(exist_ok=True)
with (d/'adjacent-tests.log').open('w') as f:code=subprocess.run(command,cwd=r,stdout=f,stderr=subprocess.STDOUT,timeout=600).returncode
(d/'adjacent-run.json').write_text(json.dumps({'argv':command,'exit_code':code,'elapsed_seconds':time.monotonic()-started},indent=2)+'\n');print('adjacent-tests',code,flush=True)
