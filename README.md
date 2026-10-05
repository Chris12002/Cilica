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
copying, rich error transfer and phone libraries remain planned;
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
