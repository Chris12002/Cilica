# DiffPlex selected-root execution approval plan

**Approved, not executed.** User approval was recorded on 2026-10-08 at
07:17:49 UTC for compiling and executing the 14 reviewed DiffPlex source
files at `f500e73f28e28813f942f1f16dec5a34b40e48e3` plus the authored validation
adapter, inside a disposable credential-free environment. The user instruction
requires approval before third-party execution. The explicit-test attribute and
environment variables prevent accidental default runs; they are not isolation.
No suitable isolated environment has been verified or provisioned by this task.
The [read-only feasibility check](diffplex-isolation-feasibility.md) records the
missing daemon/offline prerequisites. Use approved Cilica revision
`268024e94879acdb004228afa360e1222e2068b8`; subsequent independent work is excluded.

This plan validates the character/line diff paths, including whitespace and case
options. It is **not** execution of the full upstream project, builders, renderer,
three-way diff, console, or upstream test suite. Source mapping and successful
independent regressions do not establish actual candidate compatibility.

## Reviewed build surface

Batch006 pins 24 files, including the original projects/imports/tests and four
additional compile-time chunker dependencies plus global.json/NuGet.config.
Upstream selects SDK 9.0.101, SourceLink 1.1.1, signing/package-on-build properties,
and nuget.org. Do not invoke those project/import files. The owned
[SelectedRoots.csproj](../../tools/CompatibilityCandidates/diffplex-validation/SelectedRoots.csproj)
lists 14 unchanged sources explicitly, targets netstandard2.0, and disables default
source discovery/analyzers. It uses only SDK imports and the standard reference
package. No third-party project, target, generator, package script or executable is
loaded by this recipe. Source code itself executes in the subsequent oracle/native
step, hence the approval and isolation requirement.

## Isolation prerequisites

Use an already approved disposable VM/container with an unprivileged user, no
network, no credentials/agent sockets/keychains, no home-directory or live-repository
mount. Proposed hard limits: 2 vCPUs, 8 GiB RAM, 256 processes, 10 GiB writable
scratch, 1 GiB retained output and 30 minutes total wall time; stop on any limit.
These limits must be enforced by the isolation supervisor before execution. They
are a proposal for approval, not limits already provisioned. A sanitized environment alone is
insufficient. Mount/export an approved Cilica revision as `/work/cilica`, reviewed
pinned text as `/work/input/reviewed.json`, and known offline .NET SDK 10/compiler,
Clang and package/reference cache. The offline feed must contain the existing
Cilica test dependencies and NETStandard.Library 2.0.3; fail closed if absent.
Installing tooling or provisioning a new image requires separate approval.
Keep the snapshot licence beside source. Permit writes only to disposable work/output
locations. Export logs, manifests, hashes and diagnostics before destroying them.

## Exact commands after approval (fixed isolated paths)

Run from `/work/cilica`. These commands are a prepared recipe, not a record of work
already performed; the adapter and real-candidate compilation remain untested.

```sh
python3 tools/CompatibilityCandidates/candidate_snapshot.py stage diffplex --batch batch-006 --input-json /work/input/reviewed.json
python3 tools/CompatibilityCandidates/candidate_snapshot.py verify diffplex --batch batch-006
mkdir -p /work/recipe /work/out
cp tools/CompatibilityCandidates/diffplex-validation/SelectedRoots.csproj /work/recipe/
cp tools/CompatibilityCandidates/diffplex-validation/Probe.cs /work/recipe/
dotnet restore /work/recipe/SelectedRoots.csproj --source /work/offline-feed -p:CandidateSource=/work/cilica/artifacts/compatibility-candidates/diffplex-f500e73f28e28813f942f1f16dec5a34b40e48e3 -p:NuGetAudit=false
dotnet build /work/recipe/SelectedRoots.csproj -c Release --no-restore --output /work/out -p:CandidateSource=/work/cilica/artifacts/compatibility-candidates/diffplex-f500e73f28e28813f942f1f16dec5a34b40e48e3
dotnet build tests/Cilica.Metadata.Tests/Cilica.Metadata.Tests.csproj --artifacts-path artifacts/diffplex-approved/build -p:CustomBeforeDirectoryBuildProps=/work/cilica/tools/CompatibilityCandidates/isolated-build.props -p:RestoreSources=/work/offline-feed -p:NuGetAudit=false
CILICA_APPROVED_DIFFPLEX_PIN=f500e73f28e28813f942f1f16dec5a34b40e48e3 CILICA_DIFFPLEX_ASSEMBLY=/work/out/DiffPlex.SelectedRoots.dll dotnet artifacts/diffplex-approved/build/bin/_work_cilica_tests_Cilica.Metadata.Tests/debug/Cilica.Metadata.Tests.dll -explicit only -method Cilica.Metadata.Tests.ApprovedDiffPlexExecutionTests.Selected_roots_match_every_transcript_observation -culture invariant
```

The explicit test translates the actual adapter/candidate IL through Cilica, then
compares normal .NET with native O0/O2. Sixty-four scenarios cover both chunkers,
the original three-argument roots plus four-argument ignore-case calls,
whitespace/case flags, empty text, insert/delete, mixed line endings, supplementary
characters, lone surrogates, special ordinal exclusions and null parameter faults.
Each old/new piece length and UTF-16 unit, each block position/count, and each fault
message/parameter is an individual comparison; total transcript length is compared
too. Forced collection/sanitizers/frame cleanup remain enabled. Permanent-root
counts use the existing persistent-root mode because actual candidate singleton
reachability has not yet been measured. Report any translation/limit/semantic
failure; never replace the runtime or candidate algorithm to obtain a pass.

After retaining results, update the manifest to verified only for the exercised
roots, or record newly exposed gaps. Cleanup uses `candidate_snapshot.py cleanup`
only for its owned verified pin. A full upstream project/test run needs a separate
reviewed source/build/dependency closure and approval; it is not implied here.
