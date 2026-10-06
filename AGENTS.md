# Working on Cilica

Act as a senior developer and architect. Read `docs/design-specification.md`
before changing architecture, and present the intended design to the user for
feedback before implementation. Explain consequential choices in plain language.

## Project boundaries

- This repository has a metadata frontend, bounded Int32 native backend and
  bounded native object control flow with phase A roots, bounded non-moving
  collection and private retained handles. Bounded compiler-library profiles also
  implement values/borrows/returns, boxing, closed generics, single-target delegates
  and managed exceptions/type-test filters; see `docs/milestones/05-managed-semantics.md`.
  Milestone 6 adds the bounded owned core and verified real-kernel native transcripts;
  see `docs/milestones/06-library-and-kernel.md` for exact scope and evidence.
  Milestone 7 adds a bounded persistent Int32 C boundary and shared export modules; see
  `docs/milestones/07-embedding-and-phone-libraries.md` and `docs/embedding-abi.md`.
  V2/V3 exports share one validated closure; V3 adds typed retained object handles
  and void results; an optional capability copies exact UTF-16 through host-owned
  buffers, and Integer64 exports preserve all 64 bits. Numeric records copy nested
  public Int32/Integer64 fields; reference-bearing records, independent multi-root
  discovery and phone support remain planned. Optional
  managed-exception capture retains a handle and copies type/message UTF-16 plus
  declaration identity; inner traversal and stack traces remain planned.
  A scripted persistent real-kernel adapter has C11/shared-library transcript
  evidence on macOS ARM64 (7c.6); this is not a general application adapter or
  phone-device validation.
  Slice 7d.1 adds pinned arm64 iOS/device and simulator XCFramework packaging,
  simulator C transcript evidence and Swift lifecycle smoke checks. Physical
  device execution and a full native application adapter remain unverified.
  Slice 7d.2 adds a generated Swift runtime owner with a serial worker, scoped
  leases, numeric records, exact UTF-16 and captured fault details. Simulator
  concurrency/kernel checks pass; this is not a full application/Expo adapter.
  Slice 7d.3 adds scoped Swift pause barriers with bounded overlapping holds;
  simulator checks cover preserved state, admission races and cleanup while paused.
  Slice 7d.4 adds an opt-in UIKit scene host with immediate UI admission gating
  and one transition task. Real simulator foreground/background callbacks pass;
  OS suspension/resumption, simultaneous windows and devices remain unverified.
  Slice 7d.5 composes the unchanged kernel transcript with real UIKit transitions;
  repeated simulator cycles preserve state/text and public retained fault details.
  This remains a scripted scenario, not the application's full command adapter.
  Slice 7e.1 adds pinned NDK r27b arm64/API-26 shared-library packaging with private
  libc++, C-only exports, verified 16 KB ELF alignment and C11 cross-link checks.
  Slice 7e.2 adds a disposable arm64 Android 16/API-36 emulator harness. Both C
  lifecycle consumers and 5,376 real-kernel transcript comparisons pass with 4 KB
  pages. Slice 7e.3 adds a bounded JNI/Kotlin serial owner, scoped leases/pauses,
  captured fault copying and CheckJNI session/kernel parity. Android activity,
  API-26/16-KB runtime behavior and physical devices remain unverified.
  The broader runtime/library and full public embedding SDK remain planned. Never describe
  planned features as implemented or claim untested language/platform compatibility.
- Cilica owns an independent IL-to-C++ compiler and managed runtime. Do not
  substitute NativeAOT, Mono or Unity's IL2CPP for its advertised implementation.
- Reuse existing compilers for C# input and native machine-code generation.
- Treat assemblies as data: do not execute them in the compiler process.
- Unsupported reachable semantics must produce actionable build errors.
- Do not change example game rules to conceal compiler or runtime defects.
- Keep application-specific game logic out of compiler/runtime packages.

## Correctness and review

- Preserve managed arithmetic, exception order, object identity and ownership.
- Review every completed slice before committing. For generated C++, check
  undefined behaviour, rooting across allocations and native boundary ownership.
- Use focused local conformance tests and differential execution against .NET
  when implementing semantics. Add a reduced regression for each discovered bug.
- No tests are needed merely to mirror documentation edits.
- Keep the specification current. Add architecture decisions and a compatibility
  manifest as real implementation decisions and capabilities emerge.
- Commit coherent slices regularly; inspect main before integrating work and
  never overwrite another contributor's changes or rewrite shared history.

## Resource and output constraints

- No additional agents unless the user explicitly requests delegation.
- Hosted CI is disabled by default. Do not add/enable workflows, paid services,
  or spending increases without explicit authorization.
- Before an authorized push that can trigger hosted work, report aggregate
  runner minutes across every matrix job, repetition and retry.
- Prefer targeted local validation. Repeated expensive runs require a concrete
  new hypothesis or fix; inspect existing logs first.
- Present plans/reviews in chat. Do not create Canvas files unless requested.

## Long-running services

Use Cawtex MCP for development services; use the terminal for Git, builds and
bounded tests. Test-harness-owned disposable subprocesses may stay in that
harness. Inspect runtime/runs and confirm exact checkout paths before reuse.
Review detected commands, directories, environment, health checks and connections
before registration. Registration alone does not authorize execution. Use explicit
run IDs, inspect the cluster plan, and preserve one operation UUID and its full
arguments across retries until the result is known. Let Cawtex allocate ports.
Never kill unrelated processes or stop shared services merely because work ends.
If unavailable, report the required setup rather than starting an unmanaged server.
