# Cilica

An independent, open-source .NET IL-to-C++ compiler and managed runtime, designed
for embedding C# libraries in native and browser applications.

**Status: bounded native compiler profiles for primitives, objects and managed semantics implemented.**
The assembly explorer inspects IL and audits bounded dependencies without running
input assemblies. The bounded integer profile now emits C++20 and passes local
native differential tests, including division, remainder and checked-arithmetic faults.
Detached layouts, typed references and bounded object IL translation through
branches and loops are also implemented. The compiler API now emits a bounded instance-owned object
heap and phase A root frames, including bounded Int32/reference vectors with checked
element stores, immutable UTF-16 strings and class/interface dispatch, tested against
.NET under native sanitizers.
Bounded non-moving collection reclaims unreachable cycles, with exact heap accounting,
allocation retry and private instance-owned retained handles. Phase A roots pass
local O0/O2 collection stress checks. Compiler-library profiles also support bounded
struct copies/borrows/returns, named-struct boxing, closed generics, single-target
delegates and managed exceptions with type-test filters. The
[milestone 5 record](docs/milestones/05-managed-semantics.md) documents their exact
boundaries and local .NET/sanitizer evidence. Broader libraries, general filters,
a full embedding SDK and Expo integration remain planned.
[Milestone 6](docs/milestones/06-library-and-kernel.md) is complete for the bounded
required-library and real-kernel profile on macOS ARM64. The owned core library,
explicit reference mappings and exact intrinsic grants compile the unchanged
private turn kernel. Its ten-step .NET transcript matches native O0/O2 execution,
including forced collection and complete canonical state text (10,752 comparisons).
The [pinned baseline](docs/library-baseline.json) and milestone record define the
library subset, provenance and filter-closure evidence.
[Milestone 7](docs/milestones/07-embedding-and-phone-libraries.md) has started with
a [versioned persistent Int32 C boundary](docs/embedding-abi.md), tested through a
separate C consumer on macOS ARM64. V2 exports share instance state within one
compiled closure, with stable application IDs, checked manifests and configurable
bounded quotas. V3 adds typed retained object handles and void results.
An optional v3 capability copies exact UTF-16 strings through host-owned buffers.
Integer64 exports preserve exact signed/unsigned bit patterns. Structured value
copying and phone libraries remain planned. Optional exception capture preserves
a retained exception for type/message copying without reexecuting the failed call;
iOS is the first device target.
Broader platform and language support are planned capabilities,
not compatibility guarantees. The [compatibility manifest](docs/compatibility.md)
records the current execution evidence. Cilica is a working name, pending a naming check.

Run the reproducible primitive example through C# → IL → Cilica C++ → native:

```bash
bash examples/Primitive/run.sh
```

It compares a loop and checked helper call with .NET, including overflow and
recovery. See the [example instructions](examples/Primitive/README.md) for tools,
generated artifacts, offline options and supported limits.

Run the composed object example with arrays, strings, inheritance and dispatch:

```bash
bash examples/Objects/run.sh
```

Its inventory simulation compares results and exact null/bounds/overflow fault
origins with .NET. See the [object example instructions](examples/Objects/README.md)
for the workload, O0/O2 options and bounded-runtime limitations.

## Intended pipeline

```text
C# → existing C# compiler → .NET IL → Cilica → C++ + Cilica runtime
                                              ├─ Clang → native library
                                              └─ Emscripten → WebAssembly
```

Cilica will own IL translation, managed semantics and runtime support. Roslyn
and native platform compilers supply the stages on either side. NativeAOT may
serve as a comparison baseline; it will not silently replace Cilica's backend.

## Design

Read the [design specification](docs/design-specification.md) for architecture,
compatibility boundaries, memory management, the embedding API, validation and
milestone exit criteria.

The [benchmarking and improvement plan](docs/benchmarking-and-improvement-plan.md)
describes proposed NativeAOT and IL2CPP comparisons, generated-code inspection
and correctness-preserving optimization experiments. It builds on the existing
local execution-frame microbenchmarks; cross-backend results remain future work.

The first milestone is an assembly explorer: inspect metadata, dependencies and
IL without executing input assemblies, and produce stable diagnostics. Its plan
and progress are in [docs/milestones](docs/milestones/01-assembly-explorer.md).

The eventual flagship demonstration is the Alchemy C# turn engine running
offline inside Expo on iOS, Android and web, with equivalent results for the
same command transcript. Alchemy source and assets are not included in this
repository; independent compiler examples must remain usable without them.
The [parallel compatibility plan](docs/milestones/06-library-and-kernel.md#parallel-alchemy-compatibility-implementation-plan)
now includes exact reference-returning application delegates (AC2.1) and required
framework Func/Action/Predicate/Comparison contracts (AC2.2), with bounded native
GC/exception/identity evidence. AC3.1 adds concrete list insertion, indexed removal,
clearing and snapshots. AC3.2 supplies exact array mutation helpers and unblocks
bounded list/read-only interface mutations. AC3.3 adds dictionary removal, clearing
and slot reuse. AC5a.1 adds bounded integer math and typed comparisons. Broader
collections and full
saved-game replay remain unfinished. AC3.4 adds list/array search and value removal
for admitted equality types, preserving mutating value equality. AC7b.1 adds
String.CompareOrdinal and IsNullOrEmpty with pinned UTF-16 results. AC7b.2 adds
32/64-bit integer StringBuilder append under a fixed invariant-culture contract.
AC3.5 adds live Array.AsReadOnly views and shared empty-view identity. AC4.1 adds
concrete list enumeration; generic interface enumeration remains open. AC4.2 adds
live dictionary key/value views and bounded value lookup. AC4.3 adds concrete
dictionary enumeration with the pinned removal/overwrite rules. AC4.4 adds
concrete dictionary key/value enumeration with exact payload retention.

## Development

Prerequisite: the .NET SDK pinned in [global.json](global.json) (10.0.400, with
patch roll-forward). Build and test locally:

```bash
dotnet build Cilica.slnx
```

```bash
dotnet test Cilica.slnx
```

Native differential tests also require a C++20 compiler accepting Clang-style
flags and the undefined-behaviour sanitizer. The recorded local toolchain is
Apple Clang 17.0.0 (`clang-1700.4.4.1`) on macOS ARM64. `CILICA_CXX` optionally
selects a compiler executable path; it does not accept extra flags. Missing tools
fail these tests rather than silently skipping execution evidence. Other native
targets and WebAssembly remain untested.

If a restricted environment blocks MSBuild worker pipes or VSTest's local
socket, build without worker/compiler servers and use the existing xUnit
executable directly:

```bash
dotnet build Cilica.slnx -m:1 -p:UseSharedCompilation=false -nodeReuse:false
dotnet tests/Cilica.Metadata.Tests/bin/Debug/net10.0/Cilica.Metadata.Tests.dll -noColor -parallel none
```

Inspect an assembly's metadata without loading or running it:

```bash
dotnet run --project src/Cilica.Cli -- inspect path/to/Assembly.dll --format text
```

Decode and validate method bodies (instructions, basic blocks, exception
regions and stack depths), optionally for one method:

```bash
dotnet run --project src/Cilica.Cli -- il path/to/Assembly.dll --method Type::Name
```

Audit selected roots with explicit dependency directories:

```bash
dotnet run --project src/Cilica.Cli -- audit path/to/Assembly.dll \
  --reference path/to/dependencies --root-type Example.Entry --format json
```

Optionally add `--runtime-library path/to/CoreLibrary.dll` to map primitive
receivers and implicit strings through a selected library. Supply its dependencies
through `--reference`; no installed runtime is discovered automatically. Reports
record the selected image and mappings used. Array shapes follow inherited and
base-declared interface bodies; runtime-provided generic helpers and intrinsic
array members remain explicit gaps. See [ADR 0011](docs/decisions/0011-explicit-runtime-type-mapping.md)
and [ADR 0013](docs/decisions/0013-structural-array-receivers.md).

Use `--public-surface` instead of explicit roots to select visible declarations,
or repeat `--root 0x06000001` for exact metadata tokens. The report records feature
observations and why each node was included through its first incoming edge.
An incomplete audit exits with code 1 and preserves its evidence. Even a bounded
graph without issues does not establish runtime compatibility or clear the
ported-library exception-filter gate; see [ADR 0009](docs/decisions/0009-audit-evidence-contract.md).

To inventory application dependencies before implementing a library, explicitly
select each library image whose internals should be deferred:

```bash
dotnet run --project src/Cilica.Cli -- audit path/to/Assembly.dll \
  --reference path/to/dependencies --root-type Example.Entry \
  --contract-only path/to/dependencies/CoreLibrary.dll --format json
```

`--contract-only` is repeatable and selects exact assembly identities and hashes.
Supply the implementation image when declarations are forwarded through a facade.
It does not add reference directories or grant compiler intrinsic trust. The
`explicit-library-contracts-v1` report retains signatures and dependency reasons,
marks `deferredNodes`, and uses `contract-inventory` for a no-issue result (exit 0).
Analysis issues still produce `incomplete` and exit 1. Library callbacks and
implementation dependencies remain unresolved; this mode cannot establish that
general filters are absent. See [ADR 0088](docs/decisions/0088-explicit-library-contract-inventory.md).

Golden-file tests compare output with reviewed files in
`tests/Cilica.Metadata.Tests/Golden`. After an intended output change, rerun the
tests with `CILICA_UPDATE_GOLDEN=1` and review the diff before committing.
Corruption tests run 500 iterations by default; set `CILICA_FUZZ_ITERATIONS`
for a longer local run.

Emit one supported static integer method, selected by its MethodDef token:

```bash
dotnet run --project src/Cilica.Cli -- emit-cpp path/to/Assembly.dll \
  --root 0x06000001 --output generated.cpp
clang++ -std=c++20 -Wall -Wextra -Werror -c generated.cpp -o generated.o
```

The generated source declares its C entry point, named from the image SHA-256
and method token. Programs proven not to fault retain the value-only entry with
`std::int32_t` parameters and return. Every program also has a distinct `_try`
entry: UInt32 status, four output pointers (Int32 result, UInt32 MethodDef,
UInt32 IL offset, borrowed image-hash string), then its Int32 inputs. Throwing
programs expose only this status entry. Status is 0 success, 1 divide-by-zero,
2 overflow, or 3 for null output pointers. Outputs require valid,
disjoint storage; faults preserve the result output, and success clears origin
outputs. The borrowed string lasts as long as the generated library. This is
an experimental primitive ABI; managed exception objects/messages use the separate
compiler-library exception profile.
A native caller can link the declared function; this command emits source and
never starts a compiler or executes the input. Optional `--reference` directories use the existing explicit
resolver. Unsupported operations write diagnostics to stderr, return code 1 and
preserve an existing output file. The profile accepts constants, argument loads,
unchecked add/subtract/multiply, bitwise operations and masked shifts,
signed/unsigned division/remainder and checked add/subtract/multiply, initialized
Int32 locals, comparisons, branches,
loops, direct static calls and return. All reachable helpers must fit the profile;
required initialization, conversions and exception handlers remain unsupported.
Boolean locals, including bookkeeping locals emitted by Debug C# builds, remain
unsupported; use optimized C# builds for the current integer profile. See the
[milestone 2 plan](docs/milestones/02-primitive-execution.md).

Run the complete authored C# → IR → C++ → native comparison locally:

```bash
dotnet tests/Cilica.Metadata.Tests/bin/Debug/net10.0/Cilica.Metadata.Tests.dll \
  -noColor -parallel none -class '*NativeExecutionTests' -class '*NativeFaultExecutionTests'
```

The disposable harness builds 32 authored C# entry methods and five reduced IL probes
at `-O0` and `-O2`, checks
normal, wraparound and bitwise/shift results against .NET, and enables undefined-behaviour
sanitization for the optimized build. It bounds native process time/output and
cleans up its own processes and temporary files. The fault harness covers 37 entries
and 13,986 results per configuration, checking division/remainder and checked
arithmetic faults, propagation order, source origins and reset between invocations, with C++
exceptions/RTTI disabled.

- See [CONTRIBUTING.md](CONTRIBUTING.md) before starting an implementation slice.
- See the [test strategy](docs/test-strategy.md) for evidence requirements and
  planned coverage as the compiler and runtime grow.
- See [AGENTS.md](AGENTS.md) for repository-specific agent instructions.
- Hosted CI is intentionally unconfigured. Adding a workflow or enabling paid
  hosted work requires explicit maintainer authorization and a runner budget.

## License

[MIT](LICENSE). Third-party code must retain its applicable notices and license.
This is an independent project, not affiliated with Unity or Microsoft.


### Generic array enumeration (AC4.5)

Generic array GetEnumerator now uses an exact-granted managed helper and a traced
reference enumerator. It preserves live Current, reset/disposal, mutable per-T
empty identity and widened reference-array views. Seven authored probes plus the
public array probe supply 2,352 repeated O0/O2 native/.NET comparisons; four native
GC/cache-isolation runs, exact grant/packet boundaries and 60 existing regressions
pass with sanitizers and forced collection. Interface plan v6; native v41 unchanged.
The ledger has 21 gaps and 26 controls. Boxed collection enumerators, mixed variant
class dispatch, nongeneric Current and full replay remain unfinished.


### Parameterless LINQ cardinality (AC6.1)

Enumerable.Any/Count now preserve generic/nongeneric collection shortcuts and
reference-enumerator fallback disposal. Eight authored probes plus public LinqAny
supply 6,438 repeated O0/O2 native/.NET comparisons; 35 existing regressions and
the negative overload/factory check pass with forced GC and sanitizers. The ledger
has 20 gaps and 27 controls. Predicate/deferred operators, boxed collection
factories and full replay remain unfinished. No grants or profiles changed.


### Predicate LINQ terminals (AC6.2)

Any/All/Count predicate overloads now preserve null order, live array reads,
short circuit and enumerator disposal without using collection-count shortcuts.
Twelve probes supply 5,184 repeated O0/O2 native/.NET comparisons; 42 existing and
boundary checks pass with forced GC and sanitizers. Int32/reference callbacks are
supported; value-payload delegates, boxed collection factories, deferred operators
and full replay remain unfinished. The ledger stays at 20 gaps and 27 controls;
no grants or profiles changed.


### Single-thread yield iterators (AC4.6)

The selected core now exposes a stable positive managed-thread identity scoped to
each single-threaded runtime instance, independent of the host OS thread. Unchanged
Roslyn yield state machines pass 2,646 repeated O0/O2 native/.NET comparisons,
including reuse/clones, suspended values and finally cleanup. Two GC/serialized-host
thread runs, 24 existing core/baseline checks and the negative environment/thread
check pass with sanitizers. Other threading APIs remain unsupported; no async or
multithreaded profile is claimed. The ledger has 19 gaps and 28 controls. No new
grants/native profiles; full replay remains unfinished.


### Deferred Where (AC6.3)

Where now preserves eager validation, deferred source access, iterator reuse/clones,
Current/disposal state and nested predicate order. Query Any/Count leave an active
cursor unchanged and use a nongeneric owned contract to preserve struct-array
cardinality support. Ten probes plus public Where supply 4,614 repeated O0/O2
native/.NET comparisons; four precise query/cache GC checks and 36 existing
regressions pass. The public lambda's two static cache objects are explicitly
validated. The ledger has 18 gaps and 29 controls. Indexed operators, boxed
collection factories, value predicates and full replay remain unfinished.


### Internal Int32 enums (AC5b.1)

Top-level internal Int32 enums now use scalar storage in admitted layouts and
same-assembly direct memory operands. Five probes pass 1,470 repeated O0/O2
native/.NET comparisons with sanitizers and forced GC. Cross-assembly operands,
nonpublic generic/box access and cross-owner internal fields remain checked.
The frozen state-lookup probe advances to a metadata-constant layout rejection;
it is not yet executable. No profiles/grants change; public counts remain 18 gaps
and 29 controls. See [ADR AC0026](docs/decisions/AC0026-internal-int32-enums.md).


### Metadata constant layouts (AC5b.2)

Validated primitive constant declarations no longer occupy class or struct storage.
Five probes pass 1,470 repeated O0/O2 native/.NET comparisons with sanitizers and
forced GC; 12 metadata boundaries, 97 layout/enum regressions and 21 baseline checks
pass. Direct field operations and malformed constants remain rejected. The frozen
state probe now reaches a private nested enum; replay remains incomplete. See
[ADR AC0027](docs/decisions/AC0027-metadata-constant-layouts.md).


### Direct string calls (AC7b.3)

Exact String.Length/Chars now accept Roslyn's direct call form as well as callvirt.
Existing declaration and intrinsic permissions remain enforced. New fault/UTF-16
probes and direct-call layout regressions pass 2,646 repeated O0/O2 comparisons;
150 string/admission checks pass with sanitizers and forced GC. No profile/grant
changes. See [ADR AC0028](docs/decisions/AC0028-direct-string-instance-calls.md).


### Nested enum storage (AC5b.3)

Exact nested Int32 enum payloads now use admitted layouts with separate lexical
operand access. Three probes pass 882 repeated O0/O2 native/.NET comparisons;
eight boundary checks and 30 previous enum/lexical checks pass. The frozen state
probe now reaches the unchanged 256-type catalog limit. Static value storage and
full replay remain unfinished. See [ADR AC0029](docs/decisions/AC0029-nested-enum-storage.md).


### Wide static storage (AC5b.4)

Int64/UInt64 static fields preserve all bits in per-instance storage. Five probes
pass 1,470 repeated native/.NET comparisons; two persistence/isolation runs, 18
static/default checks, 25 initialization/packet checks and 21 public baseline checks
pass. Native profile v42 and layout v16 account for the wider private slot. Inline
static structs and full replay remain unfinished. See
[ADR AC0030](docs/decisions/AC0030-wide-static-storage.md).


### Static inline-value copies (AC5b.5)

Whole static struct loads/stores now expand through validated value layouts into
per-instance scalar and traced reference slots. Eight probes pass 2,352 repeated
O0/O2 native/.NET comparisons; four GC scenarios, eight packet mutations, the
address boundary, 90 existing regressions and 21 baseline checks pass. Layout v17,
native v42. Frozen EngineStep advances to a nonpublic generic argument; state
lookup still reaches the type limit. Static addresses and full replay remain
unfinished. See [ADR AC0031](docs/decisions/AC0031-static-inline-value-copies.md).
