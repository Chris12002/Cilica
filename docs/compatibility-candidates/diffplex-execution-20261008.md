# Approved DiffPlex execution — 2026-10-08

**The selected source builds, but translation fails with CIL3003 before any
semantic comparison. Zero of the planned 64 scenarios ran.** This is an earlier
framework contract integration blocker, not an observed output mismatch and not
proof that any other candidate feature is unsupported.

## Inputs and result

- Candidate: [mmanela/diffplex at f500e73](https://github.com/mmanela/diffplex/tree/f500e73f28e28813f942f1f16dec5a34b40e48e3),
  Apache-2.0, selected `netstandard2.0`, no runtime package dependencies.
- Cilica: immutable Git export of `268024e94879acdb004228afa360e1222e2068b8`.
  Later GUID, CRC/index conversion, Int64 parser and enum work was excluded.
- All 24 previously screened file blob pins verified. The unchanged authored
  recipe compiled exactly 14 upstream source files plus the authored Probe.
  Upstream MSBuild imports, generators, test packages and binaries were excluded.
- Offline candidate restore/build: passed, zero warnings/errors. Assembly SHA256:
  `514ff0d67c5d10e0be5532d3ddd01a73fa705d3e28402e6c117fd14987b9e621`.
- Offline Cilica harness build: passed, zero warnings/errors.
- Explicit test: **1 failed, 0 passed**, with this actual diagnostic:

```text
CIL3003: Cannot resolve 'netstandard, Version=2.0.0.0, Culture=neutral,
PublicKeyToken=cc7b13ffcd2ddd51' from the explicit reference directories.
Available identities: netstandard, Version=2.1.0.0, Culture=neutral,
PublicKeyToken=cc7b13ffcd2ddd51. No version or key substitution is applied.
Location: type reference System.Collections.Generic.List`1 in DiffPlex.SelectedRoots;
resolving token 0x06000055.
```

`ApprovedDiffPlexExecutionTests` asserts translation success before creating or
invoking the oracle delegate. Therefore no candidate oracle call, generated C++,
O0/O2 native run, sanitizer run or semantic comparison occurred. Full upstream
project execution remains untested. Both builds passing does not establish
compatibility.

The pinned `BuildAndMap` uses the NETStandard.Library.Ref 2.1 contract directory
and type mappings. The candidate correctly retains its 2.0 reference identity.
The resolver refuses substitution as designed. The .NET Standard compatibility
filter remains a screening criterion, not evidence of Cilica integration support.

## Isolation and reproducibility

The user approved the selected test at 07:17:49 UTC and subsequently approved
starting installed Docker and acquiring official tooling/references. Setup
network activity finished before each separate execution timer began. There were
no unexpected permission, security-setting or persistent-access prompts.

Docker Desktop 4.56.0 / Engine 29.1.3 used the explicit local Unix endpoint and an
empty Docker client configuration, without credentials. The final execution image
was built with networking disabled and COPY instructions only, from the prepared
toolchain image. Actual restore, compilation and testing used `--network none`.

- Official Microsoft SDK 10.0.401, Ubuntu 24.04 arm64, runtime 10.0.12;
  base digest `sha256:e47b8fe8c16821354690e16ef744d53a53a9cdae13ad9e000058913cd0368718`.
- Official Ubuntu Clang 18 and libclang-rt-18-dev `1:18.1.3-1ubuntu1`, Python3
  `3.12.3-0ubuntu2.1`; all transitive installed versions retained.
- Official NuGet NETStandard.Library 2.0.3, Microsoft.NETCore.Platforms 1.1.0,
  and NETStandard.Library.Ref 2.1.0 (the SDK image lacked the latter).
  Existing Cilica dependency packages copied from cache with SHA512 checks;
  all package hashes/URLs retained. No package restore had network access.
- UID/GID 10001; read-only image; all capabilities dropped; no-new-privileges;
  Docker init; no host mounts, home directory, credentials, sockets or live repo.
- 2 CPUs, 8 GiB RAM with no additional swap, 256 processes; 9 GiB `/work` plus
  1 GiB `/tmp` scratch; separate 1 GiB `/evidence`; Docker logging disabled.
- Host supervisor verifies the container configuration before start, enforces
  the 30-minute limit, exports bounded regular-file evidence and removes its
  container. Source and package copies are immutable image inputs.
- Existing `CILICA_CSC_DIRECTORY` override selects the installed 10.0.401 Roslyn
  directory (baseline global.json names 10.0.400 with latestPatch roll-forward).
  `CILICA_CXX=clang++-18`; invariant culture. No candidate or compiler patch.

One initial run completed in 35.9 seconds, but `docker cp` returned an empty archive
for its tmpfs evidence. Its result is unknown and is not counted. After correcting
the host exporter to read the bounded evidence using `docker exec tar`, the same
unchanged image ran again. The retained attempt took 39.26 seconds through the
runner and 41.3 seconds through evidence collection/removal. The explicit test
itself took 0.733 seconds. Neither attempt approached its execution limit.

[Retained test log](diffplex-execution-20261008/differential.log),
[result and exact commands](diffplex-execution-20261008/result.json),
[container configuration](diffplex-execution-20261008/container-inspect.json),
[input/package provenance](diffplex-execution-20261008/input-provenance.json),
[tooling inventory](diffplex-execution-20261008/tooling-packages.txt), and
[cleanup receipt](diffplex-execution-20261008/cleanup.json) are versioned beside
this report. Full setup logs, Dockerfiles, preparation script, runner, supervisor,
licence and SHA256 receipt remain in `artifacts/diffplex-approved-20261008`.
The existing execution plan and candidate_snapshot workflow remain reusable.

## Cleanup and next action

After verifying the retained evidence hashes, removed the task containers,
selected-input and tooling images (including their untagged parent layers),
failed tooling-build container, exported Cilica copy and candidate/package staging
copies. No unique tests or evidence were removed. Kept the official SDK image
cache, which contains no candidate inputs. Left authorized Docker Desktop running;
no unrelated container, source file or peer implementation was changed.

The source-level mapping is now supplemented by an actual integration failure.
Keep DiffPlex `mapped-blocked`. Next: author a minimal independent netstandard2.0
reference fixture, then implement/configure explicit earlier-contract resolution
and type binding with identity/grant boundary tests. Determine whether this is
solely a harness configuration change before widening production support.
Coordinate shared binding/test-helper edits with Review milestone 6. Select the
updated validation baseline explicitly, then rerun the unchanged selected source
and complete all 64 transcripts at O0/O2. Do not retarget the candidate to 2.1 or
silently relax exact assembly identities. No new runtime feature is claimed by
this execution-only slice.
