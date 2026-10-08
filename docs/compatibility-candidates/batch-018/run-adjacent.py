import pathlib,subprocess,json,time,os,hashlib
r=pathlib.Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica');d=r/'artifacts/compatibility-batch-018';started=time.monotonic();build=d/'build/bin/_Users_christian_.codex_worktrees_alchemy-replay-baseline_Cilica_tests_Cilica.Metadata.Tests/debug'
(d/'assembly-hashes.json').write_text(json.dumps({p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in build.glob('Cilica*.dll')},indent=2)+'\n')
command=['dotnet',str(build/'Cilica.Metadata.Tests.dll'),'-method','Cilica.Metadata.Tests.InvariantLowerTests.Identity_mixed_surrogates_nulls_and_retained_source_match_dotnet','-method','Cilica.Metadata.Tests.CandidateOrdinalIgnoreCaseTests.Every_scalar_and_lone_surrogate_matches_the_oracle_profile','-parallel','none','-culture','invariant','-noColor']
os.environ['CILICA_NATIVE_EVIDENCE_DIRECTORY']=str(d/'native-evidence-adjacent');(d/'native-evidence-adjacent').mkdir(exist_ok=True)
with (d/'adjacent-tests.log').open('w') as f:code=subprocess.run(command,cwd=r,stdout=f,stderr=subprocess.STDOUT,timeout=600).returncode
(d/'adjacent-run.json').write_text(json.dumps({'argv':command,'exit_code':code,'elapsed_seconds':time.monotonic()-started},indent=2)+'\n');print('adjacent-tests',code,flush=True)
