# Albireo Decode Unity preparation — 2026-10-09

The candidate-specific owned adapter is prepared. **Albireo has not been built,
imported or executed by this preparation.** The smallest demonstrated Unity route
is the normal Mac host, requiring separate explicit approval for this exact
candidate and workload. The earlier isolated .NET/Cilica proposal and DiffPlex
host approval do not grant that permission. The [reference manifest](reference/albireo-decode.json)
remains pending, with no execution receipt, measured profile or reviewed exception.

## Exact proposed inputs

Use `kappa7194/base32` at `1eabd2fcba515d0bd63881fd2a2879555c43648b`, MIT,
Copyright 2014 Albireo. Only `Albireo.Base32/Base32.cs` (Git blob
`bd6584e246151e3e493ed1eb9f17de269cb1ddc1`) and `LICENSE.txt`
(`1b3da92904fd9e9c1f4f327fafa5c7e0c771b150`) are needed from upstream.
Online screening is retained in batch 018; no additional download is needed for
this preparation. No upstream project, target, test, analyzer or generator runs.

The [existing isolated proposal](albireo-decode-proposal.md) supplies the reviewed
netstandard2.0 build recipe and unchanged owned `AlbireoDecodeProbe`. Reuse its
**whole** `Albireo.SelectedDecode.dll`, not a rewritten Unity-specific library.
The DLL includes the entire source, including Encode, although only Decode is
selected for execution. The actual DLL hash is not yet available; record its
SHA-256 and recipe/source pins before any host import. Retain the exact MIT notice
beside all retained source/assembly bundles. The new [machine-readable plan](albireo-unity-plan.json)
pins owned wrappers, template, comparison/staging tools and unchanged probe.

The root is `Cilica.CandidateValidation.AlbireoDecodeProbe.Run(Int32,Int32)` →
`Base32.Decode(in String)`. Preserve all 32 existing scenarios, inputs at most 257
UTF-16 units and at most 4096 observations per scenario. Every byte, result
identity, status and exception message/parameter UTF-16 unit is compared. Do not
normalize a Unity/.NET message difference into a pass. A failed match may remain
qualified source-reference evidence; it cannot support equivalent-output timing.

## Demonstrated environment and required decision

The Unity lane verified Unity 6000.3.2f1 ARM64, installed Mac IL2CPP and Xcode
26.1.1 on this Mac. The current runner has a 1790-second **per-command** watchdog,
64 MiB captured console and 1 GiB exported generated-code capture. Worker count 2
is a scheduling hint, not an enforced two-CPU limit. There is no demonstrated
8 GiB/256-PID cap, offline network boundary, credential isolation or filesystem
isolation. The Linux arm64 offline image in the original proposal cannot run
this Mac Unity target. No isolated macOS/Unity guest has been established.

The smallest demonstrated route therefore needs explicit approval to import and
build this exact whole DLL with these owned wrappers, then run verification and
the bounded diagnostic player on the normal host with those limitations. Approval
must record that scope; it must not be inferred from a yes to the isolated recipe.
No current licence activation is needed. No installation/account/EULA action is
part of this plan. If host execution is declined, separately investigate/provision
an isolated Unity environment under its own approval, or seek a reviewed workflow
exception. Neither alternative is currently verified or approved.

## Handoff sequence after the relevant approvals

1. Complete only the separately authorized isolated .NET/Cilica recipe first.
   Retain the exact DLL, source/licence/recipe hashes, full `oracle-observations.json`
   and native results. Any build/translation failure remains evidence, not permission
   to broaden execution. Bind input cases, whole DLL and all owned workload/project
   inputs in the workflow manifest; obtain its SHA-256 fingerprint.
2. After separate host scope approval, use the data-only staging command below in a
   fresh canonical physical directory. The template contains only owned wrappers,
   built-in JSON serialization module and the exact supplied DLL. It has no Editor
   initialization hook that invokes the candidate. Stop on unexpected package
   downloads, new tools or input substitutions. Retain the complete resulting
   project/package/settings identities before execution.
3. The Unity lane creates the project from the owned template through its verified
   route and invokes `AlbireoReferenceBuild.Build` explicitly, passing
   `--cilica-output /absolute/new/Albireo.app`. Bound each command; retain logs and
   failures without automatic retries. Build is Mac ARM64 IL2CPP, Release compiler,
   OptimizeSpeed, Minimal stripping, incremental GC and **Development** player,
   with automatic profiler connection disabled. The wrapper's settings JSON mainly
   records requested configuration: also capture API readbacks, BuildReport options
   and actual generated compiler flags; do not label requested strings as proof.
4. Adapt the capture matcher to the exact generated symbols in
   `capture-requirements.json` (`AlbireoDecodeProbe_Run`, `Base32_Decode`). Preserve
   generated C++/headers/options/logs and their hashes under the existing 1 GiB
   bound. DiffPlex `prepare.py`, the `BenchProbe` matcher and its 523-result fixture
   are not an Albireo pipeline. The runner/capture adapter remains the Unity lane's
   work; this package does not implement a generic runner.
5. Use the lane's verified **LaunchServices** route, bounded and monitored for this
   exact app/PID. Direct repeated executable launch previously aborted for DiffPlex;
   do not silently substitute it. Proposed player arguments are below. Perform
   verification and compare the complete transcript before profiling. A fingerprint
   echoed by the wrapper is insufficient alone: verify input/build/native hashes
   externally before launch and bind raw artifacts in the execution envelope.
6. Run one bounded diagnostic profile attempt (three selected batches, up to 128
   iterations each); no clean timing or speed ratio is claimed. Preserve failed
   attempts. Stop/close and check the raw file's final size, and have the future
   owned watchdog monitor that file as a **soft** 64 MiB limit. A post-frame check
   cannot enforce a disk quota. The 32 MiB profiler buffer is not a file-size cap.
7. Inspect the capture for all three `Cilica.Albireo.DecodeSelectedRoots` samples.
   Retain the inspection receipt plus overflow/paused-collection/skipped-frame logs.
   Reject or explicitly qualify missing data; do not silently raise the buffer.
   Nonempty raw bytes alone cannot set profile status to measured. Then inspect
   generated implementations for the API/root/allocation questions in the capture
   manifest and map findings to independently authored Cilica regressions.
8. Emit the existing [stage/input/outcome/raw-artifact envelopes](unity-runner-contract.md).
   Keep static reference, executed correctness and diagnostic profile outcomes
   separate. Only matched executed output receipts may support comparative timing.
   Preserve licences, exact hashes, transcripts, reviews and failures before deleting
   only task-created recoverable bundles. No runtime change, push or merge is proposed.

Commands below are specifications for the future approved handoff, **not evidence
that they ran**. The stage/compare scripts themselves only process data:

```text
python3 tools/CompatibilityCandidates/albireo-unity/stage_bundle.py \
  --dll ISOLATED/Albireo.SelectedDecode.dll --dll-sha256 ACTUAL_SHA256 \
  --license ISOLATED/LICENSE.txt --output /absolute/fresh/bundle
python3 tools/CompatibilityCandidates/albireo-unity/transcript.py expected \
  ISOLATED/oracle-observations.json --fingerprint REVIEWED_INPUT_SHA256 \
  --output /absolute/fresh/expected.json

Player verify arguments:
--cilica-mode verify --cilica-fingerprint REVIEWED_INPUT_SHA256 \
  --cilica-result /absolute/fresh/verify.json
Player profile arguments:
--cilica-mode profile --cilica-fingerprint REVIEWED_INPUT_SHA256 \
  --cilica-result /absolute/fresh/profile.json \
  --cilica-profile /absolute/fresh/profile.raw --cilica-iterations 32

python3 tools/CompatibilityCandidates/albireo-unity/transcript.py compare \
  /absolute/fresh/expected.json /absolute/fresh/verify.json
```

The representative profile uses scenarios 2, 8, 29 and 31: padded short input,
lowercase text and bounded long inputs. It warms eight iterations and records
three batches. Each `Probe.Run(scenario,-1)` constructs source and performs two
Decode calls plus owned observation-length work. This is **not pure Decode timing**.
Development profiling and marker overhead also prevent competitive speed claims.
The final raw inspection, launch monitoring and execution envelopes are deliberately
pending runner work, not automatically supplied by the comparator.

## Preparation validation and review

Eight synthetic staging/transcript tests pass using opaque owned bytes, never a
candidate DLL. Installed-API compilation of the owned wrappers passes development,
release and Editor configurations with warnings treated as errors. The reviewed
profiling fixture defines both `DEVELOPMENT_BUILD` and `ENABLE_PROFILER`; an owned
metadata-only inspector confirms emitted `Profiler.BeginSample` and `EndSample`
call instructions in its Update method. It does not load or invoke the inspected
assembly. The probe implementation used for compilation throws and was never run.

[Retained receipts](albireo-unity-preparation/preflight-reviewed/results.json)
include source hashes, commands and logs. Earlier failure evidence is retained:
the initial release fixture rejected an unassigned raw-file field under warnings
as errors; the corrected fixture initializes it. Historical response-file paths
refer to the disposable preparation directory; they are not portable runner scripts.
No compiled fixture binaries or third-party material are committed.

The independent Unity lane reviewed the supplied APIs and requested the final
raw-size check, explicit marker inspection and `ENABLE_PROFILER` evidence; all
are now addressed in code or required future completion gates. Actual Unity import,
IL2CPP build, packages, candidate behavior, marker capture, LaunchServices execution
and host resource behavior remain untested for Albireo. These preparation passes
do not advance the candidate compatibility or reference-completion gate.

Unity profiler behavior references: [binary log lifecycle](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Profiling.Profiler-enableBinaryLog.html),
[buffer limit and paused collection](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Profiling.Profiler-maxUsedMemory.html),
[conditional BeginSample calls](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Profiling.Profiler.BeginSample.html).
