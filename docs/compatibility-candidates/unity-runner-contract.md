# Unity reference handoff contract

The compatibility lane owns the candidate ledger and `reference_workflow.py`.
The performance lane owns adaptation of Unity inputs, capture and profile harnesses.
This document is an interface review and handoff; it grants no new execution approval.

## Existing reusable pieces and concrete limits

All paths below are under `tools/Performance/unity/` in `Cilica-performance`.

| Entry point | Current behavior | Required candidate adaptation |
|---|---|---|
| `prepare.py --work --package-cache` | Retrieves exactly the reviewed DiffPlex14 source subset and owned adapter at their pins | New candidate selection/provenance; do not call this script for another library |
| `pipeline.py --work --unity-cli --dotnet --stage build\|capture\|verify\|all` | Builds the fixed DiffPlex template; checks candidate/script/native hashes across stages | Project/product names, assembly set, roots, Unity build harness, oracle/player launch |
| `capture.py --project --output` | Copies C++/headers, response files, JSON, text and logs from Library/Bee and Temp; hashes every file; caps export at1GiB | Exact candidate-generated symbol matchers; it currently requires BenchProbe |
| `check_result.py EXPECTED ACTUAL` | Generic exact JSON `values` and `checksum` comparison, rejecting `error` | Adapt the expected transcript and producer/result schema; current harness fixes523 DiffPlex observations |
| `run_bounded.py --log --seconds1790 -- COMMAND` | Preserves exit/timeout receipts;64MiB console-log limit; signals only its own child process group | Supply the reviewed command and paths; this is not VM/network isolation |

The Mac profile used Unity6000.3.2f1, ARM64 IL2CPP Release/OptimizeSpeed,
Minimal stripping and incremental GC. These are observed recorded settings for
the existing build, not defaults to silently apply to another candidate. Retain
the actual editor/compiler versions, target settings, commands and dependencies.

Direct repeated Mac player launches previously aborted before engine startup;
the retained successful measurement route uses LaunchServices. Keep that failure
and route distinction in any adapted runner. The original pipeline's direct-player
verify path is not a generic promise of launch reliability.

## Handoff inputs

- Exact candidate repository/commit, licensed selected source subset and hashes;
  exact owned adapter/workload harness source or assembly identities.
- Selected root signatures, representative input cases and expected observations.
  Preserve excluded paths and reasons. Presence in captured C++ does not prove a
  path ran or was representative.
- Per-runtime input assembly map and provenance from source/build inputs. Bind the
  adapter and workload harness to the input fingerprint, not only library DLLs.
- Explicit authorization covering the exact candidate, stages and environment;
  approved resource/isolation plan. Local host limits do not imply container limits.
- Requested reference questions/gap IDs, profile modes and the reviewed Unity
  target/toolchain. Missing approval is a pending handoff, not an executable job.

## Retained outputs

1. Build provenance and actual settings; candidate/adapter/native hashes, commands,
   logs and compiler response files. A successful exit alone is insufficient.
2. A generated-source manifest of relative paths, byte counts and SHA-256 hashes,
   plus exact symbols/source locations and their relationship to selected gap IDs.
   The legacy output is `generated/manifest.json`; `pipeline.json` separately
   records stage outcomes and starts with `timing_claim:false`.
3. A reviewed static finding for each used reference location. State what the code
   demonstrates, what Cilica will do independently, and which ownership/resource
   guarantees cannot be inferred from Unity's implementation.
4. Executed output verification as its own receipt. Preserve the full transcript,
   exceptions and identities where applicable; a matching checksum alone is not
   an equivalence proof. Keep .NET, Unity and Cilica execution statuses separate.
5. For an actual comparative profile: matched executed workload identity/output,
   roots/cases, raw samples/counts/stacks, warmup/iterations/clock units, toolchain
   settings and host activity. CPU counters/static source/elapsed time/RSS/disk
   size are distinct observations. Document any path-relevance exception.
6. Exact failure stage and evidence if Unity cannot build/run/profile the selected
   input. A reviewed alternative may be used with explicit limitations, without
   relabeling a blocked capture or omitted profile as completed.

## Contract review findings sent to compatibility

- Bind the owned harness/adapter identity into the scope fingerprint and executed
  receipts; candidate DLL hashes alone do not prove the workload is unchanged.
- Model a successful but differently pinned Unity capture explicitly. The retained
  Alchemy capture has different DLL hashes from the frozen compiler audit. It is
  useful qualified source reference but cannot establish same-binary validation
  or a matched performance ratio. Calling this successful capture blocked would
  also be inaccurate.
- A measured profile with Unity correctness marked not-run must remain qualified
  diagnostic evidence. Comparative timing requires executed matched-output checks.

No generic Unity execution pipeline is implemented by this handoff. No Albireo or
other new upstream candidate has been executed. The runtime speed experiment uses
only the previously approved pinned DiffPlex workload and remains a separate lane.
