# ADR 0003: Class library strategy

Status: proposed. Recorded 2026-09-28.

## Context

Most of what programs call is class library, not language. Even a small game
engine reaches collections, strings, number formatting, equality comparers,
exceptions, enums and, from stage 2 of ADR 0001, `Task`. Library coverage is a
major factor in adoption and one of the main risks in the specification
(section 19).

Two goals constrain the choice:

- **Behavioural equivalence.** The flagship requires identical command traces
  on every platform and agreement with .NET in differential tests. That depends
  on details .NET documents loosely or not at all, such as:
  - the order of elements `List<T>.Sort` leaves among equal keys (unstable
    introsort);
  - the sequence produced by seeded `System.Random`;
  - `Dictionary` and `HashSet` enumeration order, which follows the internal
    entry array, including slot reuse after removals;
  - number formatting and parsing output.
- **Independence.** Cilica's claim concerns its compiler and runtime. Reusing
  library code is allowed with provenance (specification section 11), but
  NativeAOT, Mono or IL2CPP must not substitute for Cilica's compiler or runtime.

Microsoft's library source in dotnet/runtime is MIT-licensed. Its core library
is organised as shared source plus runtime-specific parts: CoreCLR, Mono and
NativeAOT each combine the shared `System.Private.CoreLib` source with their own
implementations of runtime-coupled types. The shared code relies on
`System.Runtime.CompilerServices.Unsafe`, `ref` arithmetic, `Span<T>`, hardware
intrinsics with software fallbacks, and methods the runtime implements
internally.

## Decision

### 1. Layered library

| Layer | Contents | Source |
| --- | --- | --- |
| Runtime (C++) | Allocation, collection, type descriptors, exception dispatch, handles, thread contexts | Written for Cilica |
| Runtime-coupled managed core | `Object`, `String` and `Array` internals, `RuntimeType`/`Type`, `Delegate`, `Enum` internals, `RuntimeHelpers`, `GC`, `Monitor`, `Interlocked`, `Thread`, `Environment`, `EqualityComparer<T>.Default`, resource strings | Written for Cilica, in C# with compiler intrinsics |
| Shared library | Collections, `Span<T>`/`Memory<T>`, string operations, formatting and parsing, `Math`, `Random`, LINQ, text, later `Task` | Ported from dotnet/runtime at a pinned release |

Cilica becomes another runtime flavour of the same shared source: ported shared
files plus Cilica's own runtime-coupled partial classes. Runtime-specific parts
of CoreCLR, Mono and NativeAOT are not imported.

### 2. Pinned upstream release

- Port from one dotnet/runtime release tag, initially the current long-term
  support release (.NET 10 at the time of writing). The exact tag is recorded
  at import.
- Differential tests run against the same .NET release, so Cilica's reference
  behaviour and its library source match.
- Alchemy targets .NET Standard 2.1 with a Unity-compatible C# language
  version but does not run on Unity, so its authoritative behaviour is that of
  the .NET runtime executing it. Its baseline transcripts are regenerated on
  the pinned release. Before that baseline is adopted, transcripts from the
  runtime Alchemy currently uses are compared with the pinned release, and any
  difference is recorded.
- Upgrading is a deliberate change: re-import, reapply local modifications,
  rerun conformance and record the new tag.

### 3. Import rules and provenance

- Imported files live in a directory mirroring their upstream paths, kept as
  close to unchanged as possible, so upstream updates can be diffed.
- Cilica-specific behaviour goes in separate partial-class files. When an
  imported file must change, the change is marked in the file and listed in a
  provenance manifest: upstream path, commit, local modification and reason.
- Upstream licence headers are retained. Applicable entries from dotnet/runtime's
  third-party notices are carried into Cilica's notices.
- The library is compiled from C# source to IL by Roslyn, then compiled by
  Cilica like any other input. It is never linked from Microsoft's shipped
  binaries.

### 4. Assembly identity and API surface

- Cilica accepts an input assembly when a .NET application targeting the
  pinned release could reference it under NuGet's compatibility rules,
  excluding assemblies built only for .NET Framework:

  | Input target | Accepted | Reason |
  | --- | --- | --- |
  | .NET Standard 2.0 and 2.1 | Yes | The most common library targets |
  | .NET Core 3.x and .NET 5 up to the pinned release | Yes | Their reference surfaces are essentially subsets of the pinned release |
  | Newer than the pinned release | No | May reach members the ported library lacks. The diagnostic names the pinned release |
  | .NET Framework only (`net4x`) | No | A different core-library identity and surface |

- Accepted assemblies run with the pinned release's library behaviour, as they
  would when referenced by an application on that release. A member that an
  older target exposed but the pinned release removed produces the ordinary
  unsupported-member diagnostic.
- The language version used to produce an assembly does not matter; Cilica
  consumes IL.
- The compiler maps reference-assembly identities, facades and type forwarders
  to Cilica's implementation types through a generated identity table. User
  types keep assembly-qualified identity.
- A reachable member that the Cilica library does not implement produces a
  diagnostic with its reachability chain.
- The compatibility manifest lists implemented members. It is generated from
  the library and its tests, not maintained by hand.

### 5. Trusted library assemblies

The shared source uses `Unsafe`, `ref` arithmetic and interior references,
which are outside the user-code profile (specification section 3).

- These operations are compiler intrinsics permitted only in trusted
  assemblies: those built and shipped by Cilica, identified by build
  provenance, not by assembly name. A user assembly named
  `System.Private.CoreLib` gains nothing.
- User code using the same operations still receives diagnostics until an
  unsafe-code profile exists.
- `Span<T>` and `ref` fields in library code make interior references part of
  the first library milestone. The interior-reference ADR anticipated by
  ADR 0002 is therefore required before milestone 6.

### 6. Compile-time specialisation of runtime-coupled behaviour

Operations CoreCLR performs through runtime reflection or JIT intrinsics are
resolved by the compiler in the closed world:

- `EqualityComparer<T>.Default`, `Comparer<T>.Default` and array sort helpers
  select their implementation per instantiation at compile time.
- `RuntimeHelpers.IsReferenceOrContainsReferences<T>()`, `typeof(T) ==
  typeof(X)` tests and similar patterns fold to constants per instantiation.
- Enum names, values and validation use generated tables for reachable enum
  types.
- Resource strings use generated static tables instead of `ResourceManager`.
  A build option can replace messages with resource keys to reduce size.
- Configured feature switches, such as invariant globalization, are
  compile-time constants, so disabled paths become unreachable.

### 7. Behaviour choices for the first library profile

- **Hardware intrinsics** report as unsupported initially, so shared code takes
  its scalar paths. Mapping `Vector128` and related types to Clang vector
  extensions and WebAssembly SIMD is a later optimisation with its own tests.
- **Globalization** uses invariant mode only. Differential tests run .NET with
  invariant globalization enabled. Culture data and ICU are out of scope.
- **String hashing** stays randomized per runtime instance, as in .NET.
  Dictionary enumeration order does not depend on hash values, so traces are
  unaffected. A fixed seed is available only as a test option.
- **Floating-point `Math` functions** are deferred to the floating-point ADR.
  Platform maths libraries differ across iOS, Android and the web, so
  cross-platform consistency will probably require one bundled implementation.
- **Stack trace text** is limited initially and excluded from differential
  comparison.
- **Host-dependent APIs** (files, networking, console, time zones, cryptography)
  are out of the first profile. Console output is available as a test and
  diagnostic sink. Host services later arrive as explicit embedding capabilities.

### 8. Bootstrap core library

Early milestones cannot compile the ported library. Until milestone 6, Cilica
uses a small hand-written bootstrap core library. Test programs always compile
against the real reference assemblies, and the identity table maps them to
whichever library is present. Tests therefore do not change when the ported
library replaces the bootstrap one, and a missing member is an ordinary
diagnostic. The bootstrap library is deleted once the ported library covers
its surface.

## Consequences

- Library behaviour matches the pinned .NET release by construction wherever
  shared source is used, which is what the flagship's equivalent traces and
  differential tests require.
- Adoption benefits: existing C# libraries see familiar APIs with familiar
  behaviour, and supported coverage can grow file by file.
- Whole-program reachability keeps binaries limited to the library code a
  program actually reaches. The shared source has deep dependency chains,
  especially in formatting, so size reports (`why-included`) and feature
  switches are needed from the first library milestone. Binary size is
  priority 3 in section 15.
- Cilica takes on more work earlier: interior references and trusted
  `Unsafe` intrinsics become prerequisites for milestone 6, and
  runtime-coupled partials must be written for each imported area.
- Upstream code evolves. Pinning to a long-term support release limits churn,
  but each upgrade costs a re-import and a conformance run.
- Scalar fallbacks make string and span operations slower than .NET until SIMD
  mapping lands.
- The portfolio claim stays accurate: Cilica independently compiles and runs
  the library, and its origin is disclosed.

## Alternatives considered

- **Write the whole library from scratch.** It offers full control and a small
  library. Rejected except for the runtime-coupled core: matching .NET's
  sorting, collection ordering, `Random` and formatting exactly would mean
  re-deriving existing code. Every divergence breaks differential tests, and
  unfamiliar behaviour harms adoption.
- **Compile Microsoft's shipped `System.Private.CoreLib` binary unchanged.**
  Rejected: it depends on CoreCLR's private internal calls, object layout and
  JIT intrinsics. Cilica would have to emulate another runtime's private
  contracts, without being able to patch the code.
- **Use Mono's older class library.** Rejected: its behaviour predates modern
  .NET, so it diverges from the differential reference and still needs the
  same porting work.
- **Import NativeAOT's runtime-specific CoreLib parts.** Rejected: they are
  coupled to NativeAOT's object model and runtime, and importing them risks
  NativeAOT substituting for Cilica's runtime.

## Validation

Before acceptance, the milestone 1 assembly explorer computes the reachable
closure of the Alchemy kernel against a trial Roslyn build of the ported shared
library. It reports the internal calls, intrinsics, `Unsafe` operations,
interior references and generic instantiations that closure requires. The
report sizes the runtime-coupled layer and confirms whether the kernel reaches
floating-point `Math`, globalization or async.
