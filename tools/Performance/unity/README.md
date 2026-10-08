# Local Unity / DiffPlex pipeline

Status: **setup and .NET verification completed; Unity build blocked by license
activation**. This is the user-approved local Mac workflow. It does not claim
container isolation, a Unity compatibility pass, generated C++ capture or timing.

The installed editor is Unity 6000.3.2f1 ARM64. Mac IL2CPP support was added using
Unity's official CLI 1.0.0-beta.13. The benchmark uses the original 14 reviewed
DiffPlex files at `f500e73f28e28813f942f1f16dec5a34b40e48e3`, plus the unchanged
owned `BenchProbe.cs` from `a581334811b8f303b0869079589e0e3ce528dfb3`. No upstream
project, generator, test suite or build script is invoked. Only the original
128-character workload is executed; the larger timed-out workload is excluded.

The Mac .NET SDK is 10.0.400. Its rebuilt candidate DLL has a new recorded hash;
it is not represented as the historical Linux DLL. The new DLL passes all 523
historical observations and checksum 6146. The Unity scripts compile against
installed Unity API assemblies with zero warnings/errors, but that is a metadata
compile check, not an Editor build. Five pipeline utility checks pass.

## Resume on this machine

Activate the user's existing Unity license in Unity Hub first. The actual Editor
exited 198 when creating the project; CLI license status also reported inactive.
No license purchase, entitlement selection, EULA acceptance or sign-in was automated.

From this repository:

```sh
python3 tools/Performance/unity/pipeline.py \
  --work '/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing' \
  --unity-cli '/Users/christian/Documents/Codex/2026-10-08/task-2/tooling/unity-cli/unity' \
  --dotnet '/Users/christian/.dotnet/dotnet'
```

This creates/imports the authored template, builds a non-development ARM64 IL2CPP
player, captures generated C++, and verifies the player against the complete
historical transcript and a fresh .NET oracle. It does not run clean timings.
Every attempt creates a separate evidence directory. An incomplete project is
preserved there before retrying creation. Existing completed projects keep their
Unity-generated scene/settings/meta files; only the owned scripts and candidate
DLL are refreshed. Cloud linkage is explicitly disabled with `--no-cloud`.

The successful build's input/script/native hashes must match before separate
`--stage capture` or `--stage verify` runs. `--stage build` builds without launching
the player. Capture refuses success unless generated C++ mentions `BenchProbe`.
Generation paths are discovered under this project's `Library/Bee` and `Temp`;
if Unity changes its layout, missing capture is reported, not silently accepted.

## Reproduce inputs in a fresh work directory

`prepare.py` downloads only the 14 pinned compile files and license, checks their
recorded Git blob hashes and records SHA-256 hashes. It verifies the existing
NETStandard.Library 2.0.3 package before staging a local feed. It refuses to replace
existing inputs. The template has no additional package dependencies, authored
scene YAML or copied project settings. Unity generates those through its APIs.

```sh
python3 tools/Performance/unity/prepare.py \
  --work /absolute/path/to/fresh-work \
  --package-cache /absolute/path/to/existing/nuget/packages
```

Then, from the fresh work directory, build the selected candidate and owned oracle
host using the installed .NET 10 SDK. Replace `WORK` with its absolute path:

```sh
dotnet build inputs/recipe/SelectedRoots.csproj -c Release --output inputs/dll \
  -p:CandidateSource=WORK/inputs/candidate -p:RestoreSources=WORK/inputs/feed \
  -p:ImportDirectoryBuildProps=false -p:ImportDirectoryBuildTargets=false \
  -p:NuGetAudit=false
dotnet build inputs/recipe/DotNetHost.csproj -c Release --output inputs/host \
  -p:RestoreSources=WORK/inputs/feed \
  -p:ImportDirectoryBuildProps=false -p:ImportDirectoryBuildTargets=false \
  -p:NuGetAudit=false
```

Use `run_bounded.py --log <new-log> --seconds 300 -- <command...>` to retain
bounded build receipts, as in the current run. Next run `pipeline.py` with absolute
work/tool paths. Project creation requires the installed editor and active license;
Mac IL2CPP is required for the default build. The documented custom-template route
uses `projects create`, not `projects new`. Unity requires that templates omit
`ProjectSettings/ProjectVersion.txt`; the pinned editor writes the real file.

## Measurements and limits

Build settings are explicit and saved: IL2CPP, ARM64, Release, OptimizeSpeed,
minimal managed stripping, normal incremental-GC setting, non-development player.
The C++ build itself remains unverified until the licensing gate is resolved.
The compiler/linker commands and actual settings must be read from the capture,
not inferred from these requested settings.

The harness supports `verify`, `cold`, `steady` and `noop` via `--cilica-mode`.
Only verification is launched by the pipeline. Future steady runs use at least
300 ms warmup, seven samples and explicit 1–32768 iteration batches; checksums
are checked outside timed intervals and results serialize after computation.
Store raw Stopwatch ticks and frequency. No forced GC or per-operation clocks
are introduced. Cold means first workload invocation in a fresh player; it is not
pure process startup. A noop player still starts Unity and is not a .NET-equivalent
startup baseline. Capture settings, compare identical outputs, coordinate quiet
host windows and collect fresh native Mac .NET/Cilica baselines before ratios.

This normal host run has a 1790-second per-command wall watchdog and 64 MiB captured
console-log bound; exported source/build records are capped at 1 GiB. The Editor's
job-worker hint is 2. These are **not** hard 2-vCPU/8-GiB/256-PID VM limits or a
network sandbox. Player log output uses a separate file. RSS, managed bytes and
runtime overhead remain distinct; no memory or performance result is claimed yet.

Source templates in this repository are unimported build inputs. Unity-generated
`.meta`, scene and settings files live in the separate local project. No Library,
Temp, downloaded CLI, candidate DLL or native build outputs are committed.
