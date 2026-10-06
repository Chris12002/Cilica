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
  AC1a adds bounded owned Nullable<T> storage/members through ordinary value
  copies and roots. AC1b.1 adds nullable conversions and named-struct box/copy
  unboxing. AC1b.2 adds integer/Boolean/Char and public Int32-enum box/copy
  unboxing, with strict nullable identity. AC1b.3 adds exact boxed isinst/castclass
  for those scalars and admitted named structs, including nullable targets. AC1b.4
  allows proven confined scalar boxes beside independent interface operations.
  Raw scalar/nullable unbox, scalar interface views/dispatch, unproven box escapes
  in those compositions and nullable keys remain diagnosed; see ADR AC0006.
  AC2.1 adds exact managed reference results to single-target application delegates
  through normal rooted returns. AC2.2 adds the required framework
  Func/Action/Predicate/Comparison declarations and Boolean results with exact
  scalar target checks before lowering. See ADRs AC0007/AC0008; private closed
  callbacks and full saved-game replay are not verified.
  AC3.1 adds concrete List Insert/RemoveAt/Clear/ToArray, preserving snapshot
  identity and releasing reference/value-element roots. AC3.2 adds exact array
  Insert/RemoveAt/Clear helpers, unblocking bounded list/read-only interface
  mutations. Generic array Clear throws; nongeneric Clear resets elements.
  AC3.3 adds Dictionary Remove/Clear and free-slot reuse, preserving mutating key
  callbacks and releasing value roots. Broader collections and replay remain
  incomplete; see ADRs AC0009–AC0011. AC5a.1 adds managed 32/64-bit integer
  Math Abs/Min/Max and typed CompareTo (ADR AC0012); object comparison and
  comparer-interface execution remain outside this slice. AC3.4 adds bounded
  list/array search and value removal. Value equality can mutate the stored
  element; preserve its managed borrow (ADR AC0013).
  Milestone 7 adds a bounded persistent Int32 C boundary and shared export modules; see
  `docs/milestones/07-embedding-and-phone-libraries.md` and `docs/embedding-abi.md`.
  V2/V3 exports share one validated closure; V3 adds typed retained object handles
  and void results; an optional capability copies exact UTF-16 through host-owned
  buffers, and Integer64 exports preserve all 64 bits. Independent multi-root
  discovery, structured value copying and phone support remain planned. Optional
  managed-exception capture retains a handle and copies type/message UTF-16 plus
  declaration identity; inner traversal and stack traces remain planned.
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
