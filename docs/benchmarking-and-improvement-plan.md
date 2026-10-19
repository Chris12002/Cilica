# Benchmarking and improvement plan

Status: proposed implementation plan. Recorded 2026-10-05. No NativeAOT or
IL2CPP comparison, standalone extraction or resulting speedup is established by
this document. Writing this plan does not start builds, benchmarks or hosted work.

Compare the same managed workloads across Cilica, NativeAOT and Unity IL2CPP,
with ordinary .NET as the semantic reference. Preserve enough build artifacts to
explain differences, then use small, verified experiments to improve Cilica.
The output should answer both how a library performs and why it performs that way.

Cilica remains an independent compiler and runtime. Comparator output supplies
evidence and optimization hypotheses; it does not become Cilica's implementation.
Follow the priorities in the [design specification](design-specification.md):
as of 2026-10-19, execution latency and steady-state throughput for supported,
correct workloads come first, with DiffPlex as the immediate benchmark. Startup,
embedding overhead, size, memory and collection pauses remain measured trade-offs.
Historical prototype performance disclaimers do not defer this work.

## Existing foundation and scope

The [execution-frame runner](../benchmarks/ExecutionFrames/README.md) already
compares three nonallocating integer workloads across the primitive emitter,
object boundary, private warm object path and .NET. It checks timed checksums
and records samples, executable size, native compile duration and environment.
It uses O2 without LTO or sanitizers for timing and owns bounded child processes.
Its sequential backend order and five samples provide a starting point, not a
complete statistical protocol.

The current kernel targets net10.0. Unity compatibility must be established
before importing it. The primitive CLI and broader compiler-library APIs have
different capabilities. The private warm path is not a public persistent ABI
and currently provides no evidence for allocating workloads. Consult the current
[compatibility manifest](compatibility.md) when admitting each new case.

Start on the repository's locally evidenced macOS ARM64 target, subject to the
installed Unity editor, IL2CPP module and NativeAOT prerequisites. Record the
actual versions; do not infer availability from documentation. Mobile and browser
comparisons are separate future target matrices. Never compare different CPUs
or architectures as though a difference came solely from the compiler.

## Shared workloads and library preparation

A workload is a named operation with fixed inputs, expected observable results
and an explicit state lifetime. Keep its library independent of timing APIs,
Unity APIs and backend-specific exports. Put those concerns in adapters.

Prefer one identical managed workload DLL where the backends accept the same
framework surface. A small .NET Standard target is a candidate to investigate,
not a compatibility guarantee. If separate builds are required, pin the same
source and compiler options, retain both IL listings and disclose conditional
compilation, framework references and library implementations. Differences in
framework implementation belong to the runtime comparison, not automatically
to the translator.

| Library source | Preparation method | Evidence to retain |
| --- | --- | --- |
| Existing class-library project | Build at a pinned revision with locked dependencies | Source revision, project, lock files, DLLs and portable PDBs |
| NuGet package | Resolve the selected framework and runtime assets plus transitive dependencies | Package versions, hashes, chosen implementation assets and native assets |
| Logic inside an application | Extract a conventional class library; pass host services through explicit interfaces | Original and extracted tests, dependency changes and adapter costs |
| Small upstream algorithm | Import a reproducible source subset | Upstream revision, selected files, license and exact patches |

NuGet reference assemblies describe an API and cannot substitute for executable
implementations. Do not manually copy a few methods from a DLL and assume the
result contains its static initialization, callbacks or generic dependencies.
Keep the input intact and select entry roots; let each compiler determine its
required dependency closure, meaning all code and data needed by those roots.

Each proposed workload manifest records source/package provenance, hashes,
entry signatures, dependencies, input seeds and sizes, expected results,
initialization/reset rules, required semantics, resource limits and permitted
backend adaptations. Record Cilica's selected profile and any explicit library
mapping. Existing contract audits and mappings are intake evidence; they do not
prove that library bodies execute correctly. See the
[library compilation view decision](decisions/0091-library-compilation-views.md).

Admission has separate outcomes: unsupported, build failed, incorrect result,
resource limit reached, or eligible for measurement. Preserve failures in the
coverage report. A reduced compatible subset gets its own name and scope; it
must not be presented as the whole original library.

Start with integer arithmetic, loops and direct calls. Next admit narrowly
supported numeric conversions, struct copies, class/interface dispatch, boxing
and allocation/collection cases. Later select a real deterministic rules or
search library through the same intake. Collection-heavy libraries, parsers and
serialization libraries remain candidates until their reachable requirements
pass. Never alter game rules to conceal a compiler defect.

## Artifact extraction and inspection

Use three explicit meanings of extraction:

1. **Artifact collection:** retain generated source, native objects, symbols,
   metadata, build commands and final binaries. This is the primary approach.
2. **Code inspection:** identify methods and recover disassembly, call
   relationships and runtime observations. This does not produce a reusable library.
3. **Standalone extraction:** build code and its complete required runtime into
   an independently runnable component. This requires separate validation.

### NativeAOT artifacts

NativeAOT compiles managed IL to native code; do not expect an IL2CPP-style C++
source tree. Retain the published binary and its matching debug information:
normally a dSYM directory on macOS, PDB on Windows or DBG file on Linux.
`StripSymbols=false` can retain debug information in Unix binaries. Preserve the
optimized measurement binary and its matching symbols before producing any
separate diagnostic build. [NativeAOT deployment documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)

Build a thin executable adapter for standalone process tests. For native-call
tests, publish a class-library adapter with explicitly named
`UnmanagedCallersOnly` exports. NativeAOT officially supports shared libraries;
static libraries and unloading a NativeAOT library are not supported production
assumptions. Use a fresh process for isolation rather than unload/reload cycles.
[NativeAOT native libraries](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/libraries)

Proposed project fragment for that shared-library adapter:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <PublishAot>true</PublishAot>
  <NativeLib>Shared</NativeLib>
</PropertyGroup>
```

The adapter references the pinned workload. Start with integer arguments and an
explicit status/result contract. Export wrappers handle supported managed
exceptions without letting them cross the C boundary. Introduce buffers and
opaque handles only with defined ownership and lifetime tests. Adapter code is
backend-specific; the algorithm and input sequence remain shared.

An illustrative future command, after that project exists, is:

```sh
dotnet publish benchmarks/Comparative/NativeAotAdapter/NativeAotAdapter.csproj \
  -c Release -r osx-arm64 -o artifacts/comparative/example/nativeaot
```

For structural investigation, retain compiler/linker logs, response files and
intermediate native objects when available. In a separate diagnostic configuration,
try these version-dependent NativeAOT compiler properties:

```xml
<PropertyGroup>
  <IlcGenerateMapFile>true</IlcGenerateMapFile>
  <IlcGenerateDgmlFile>true</IlcGenerateDgmlFile>
  <IlcGenerateMstatFile>true</IlcGenerateMstatFile>
</PropertyGroup>
```

The compiler's troubleshooting documentation describes an object-layout map,
dependency graphs, and size records for emitted types, methods and blobs.
Validate the switches and parsers against the pinned SDK. Its generated-IL dump
option concerns compiler-created method bodies such as marshalling helpers;
it is not a reconstruction of every native function.
[NativeAOT compiler diagnostics](https://github.com/dotnet/runtime/blob/main/src/coreclr/nativeaot/docs/troubleshooting.md)

Identify the named exports, follow their calls with symbols, and extract
annotated disassembly. Correlate size records with the final linked image;
pre-link records need not reflect final folding or dead stripping. Dependency
graphs explain why code was included and are not runtime call-frequency profiles.
The published shared library is the reusable output. A function's extracted
bytes or intermediate object alone do not include its runtime dependency closure.

### IL2CPP artifacts

Create a minimal Unity project importing the workload assembly. Use a uniquely
named entry such as `CilicaBench_Conversions_CheckedNarrow_V1` and call it directly
from the player adapter. This gives a recognizable path through IL, C++ and
native symbols. Add narrowly scoped preservation rules only when stripping
otherwise removes required code; record them because extra roots affect size.

Build an IL2CPP player, not an Editor Play Mode measurement. Unity's pipeline
compiles C# to managed assemblies, strips managed code, generates C++, and uses
a native compiler to produce the platform output. Pin the editor, target,
stripping level, code-generation mode and native compiler configuration. Disable
Burst for the comparator so that it measures IL2CPP compilation.
[Unity IL2CPP pipeline](https://docs.unity.com/en-us/engine/6000.5/manual/scripting/compilation-and-code-reload/script-compilation/backends/il2cpp/il2cpp-introduction)

Collect these artifacts immediately after the build:

- Original workload DLL/PDB, and post-stripping assemblies when available.
- Generated C++ and headers, metadata/registration files, and runtime metadata.
- Native objects, archives, linker maps and compile/link commands when available.
- Final executable/shared modules and matching native debug symbols.
- Player settings, editor/build logs and hashes of the complete collected set.

Discover locations from the pinned build's logs and generated project. Unity
changes artifact directories across versions and targets; do not treat old
`Temp/StagingArea` paths as a current contract. Preserve generated output before
another build overwrites it. Locate the marker, then trace calls, registration
entries and referenced data; a C# assembly need not correspond to one C++ file
or one native module. Unity's generated-code explanation documents the runtime
interface and metadata dependencies, but its historical layouts are background
only. [Unity generated-code internals](https://unity.com/blog/engine-platform/il2cpp-internals-a-tour-of-generated-code)

Symbol handling is target-specific. For example, Android builds provide symbols
for modules including `libil2cpp`; archive the matching metadata and final binary
too. A script-containing module is not automatically a standalone library.
[Unity Android symbols](https://docs.unity3d.com/6000.0/Documentation/Manual/android-symbols.html)

### Marker classes and live memory inspection

Use the marker as a navigation aid. A class instance contains data and runtime
references; its executable methods can be elsewhere, inlined, folded together
or shared by generic instantiations. Searching for a distinctive string can
locate data without locating the method that uses it.

First inspect the exact optimized binary statically. Then attach a native
debugger in a diagnostic run, load matching symbols, break at the identified
function and inspect arguments, stack frames, dispatch tables and object fields.
Record module identity and relative addresses, accounting for address-space
randomization. Preserve both original symbols/addresses and normalized reports.

Illustrative commands, with actual file paths and symbols substituted:

```sh
llvm-objdump --syms --demangle <native-binary>
llvm-objdump --disassemble --demangle <native-binary>
```

```text
(lldb) image list
(lldb) image lookup -n <resolved-native-symbol>
(lldb) breakpoint set --name <resolved-native-symbol>
(lldb) disassemble --name <resolved-native-symbol>
(lldb) memory read --format x --size 8 --count 16 <validated-address>
```

The LLVM tools support symbol/disassembly inspection and LLDB provides the
debugger operations above. Check local tool versions and available symbol names.
Do not presume an inlined method has a separately breakable body.
[llvm-objdump](https://llvm.org/docs/CommandGuide/llvm-objdump.html),
[LLDB command reference](https://lldb.llvm.org/use/map.html)

Use heap snapshots to study object counts, retention and reference paths where
the profiler exposes them. Unity's memory snapshot API captures memory state
for analysis; it is not an exporter for relocatable executable libraries.
[Unity memory snapshots](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/profiling/memory/memoryprofiler/takesnapshot)

Disassembly and decompiler pseudocode are evidence about native output, not
recovered authoritative source. Debugger pauses and snapshot costs belong to
diagnostic runs. Dumped code bytes retain dependencies on relocations, globals,
runtime metadata and initialization; do not execute them as an extracted library.

### Experimental standalone IL2CPP extraction

Time-box this as an optional investigation after the player comparator works:

1. Start with a pure integer entry and capture its generated translation units,
   headers, build flags and referenced symbols.
2. Attempt to link a minimal native harness using the original generated objects
   or rebuilt C++ and the required runtime components. Preserve initialization,
   metadata registration and calling conventions.
3. Follow unresolved references and registration dependencies to a fixed point;
   a successful link alone does not establish correct initialization or execution.
4. Validate fresh-process startup, repeated calls and teardown behavior. Then add
   allocation, collection, virtual dispatch, a closed generic and exceptions.
5. Record runtime/engine components retained, startup requirements, total size
   and a repeatable build recipe. Review applicable redistribution terms before
   shipping extracted vendor artifacts.

Stop if success requires an expanding unsupported reconstruction of Unity's
startup or engine internals. Keep player measurements usable independently.
The documented Unity-as-a-Library route embeds Unity runtime components on its
supported targets; it does not establish arbitrary generated-method portability.
[Unity as a Library](https://docs.unity3d.com/6000.0/Documentation/Manual/UnityasaLibrary.html)

Any independently linked experiment is a separate comparator labelled with its
modifications and retained runtime. Do not present it as the stock Unity player,
or use manual edits to generated algorithms in the primary comparison.

## Benchmark architecture and fairness

Use one bounded orchestrator with backend adapters. Each adapter prepares and
builds artifacts, runs correctness checks, launches a measurement child, and
returns the same versioned result format. Translation treats assemblies as data;
execution happens only in explicitly launched workload processes. The .NET
reference runner should execute registered workloads, not load arbitrary input
assemblies into the compiler process.

The common lifecycle is prepare, initialize, warm up, reset as specified, run a
batch, verify results, and dispose. State whether setup, reset and cleanup are
inside or outside each measurement. Never silently accumulate retained state
between samples. Allocation/collection workloads must include their natural GC
costs; forced collection between samples is a separate labelled experiment.

| Comparison | Timed work | Interpretation |
| --- | --- | --- |
| Warm library operation | Identical input sequence and batch loop within each backend | Compiled workload plus its runtime and stated harness overhead |
| Native boundary | Common native caller invoking equivalent scalar/status exports | Boundary and lifecycle cost; initially Cilica and NativeAOT |
| Fresh process | Launch to ready signal and first verified result | Whole deployment startup, including its host |
| Long-lived workload | Repeated operations with a bounded live set | Allocation, retention, collection and steady-state behavior |
| Full integration | Host, serialization, runtime and library together | Future application behavior; Expo and public object ABI are not implemented |

For IL2CPP, warm batches execute within the built player after initialization,
without per-frame scheduling or logging inside the timed region. Report process
CPU and memory as player measurements because engine/background work can remain.
Use an empty player with the same settings as contextual baseline, and report
absolute and incremental observations. Baseline subtraction cannot precisely
isolate workload memory or CPU because runtime behavior is nonlinear.

A common native-boundary comparison with IL2CPP requires a validated supported
bridge or the standalone extraction experiment. Until then, mark that cell
unavailable instead of equating player-internal calls with native exports.
Include Cilica's private frame, budget and root-management costs; removing them
would change the backend being measured.

### Measurement protocol

1. Build Release artifacts once per configuration, outside measured execution.
   Keep sanitizers, deep profiling, debugger attachment and development-only
   instrumentation out of performance runs. Retain symbols separately.
2. Verify normal and edge-case results against .NET before timing. Compare exact
   outputs, mutations, specified identity and exceptions for conformance; timed
   checksums supplement these checks and do not replace them.
3. Supply runtime inputs and consume outputs. Use the same batching algorithm
   and iteration count per compared case. Calibrate the count outside reported
   samples so even the fastest backend exceeds timer resolution comfortably.
4. Warm each backend explicitly, including metadata/static initialization. Record
   JIT tiering and ReadyToRun settings for .NET. Cold-start tests use fresh
   processes and receive no warmup. Fresh process does not imply cold OS caches.
5. Interleave backend order using a recorded seed and repeat in independent
   processes. Start with three processes and five batches per backend/workload,
   aiming for roughly 200 ms or more per batch where the execution budget permits.
6. Preserve all raw samples and failures. Report median and spread, and summarize
   across processes; repeated batches in one process are correlated. Use more
   independent runs only for a concrete unresolved question. Small pilots cannot
   support precise tail-latency or confidence claims.
7. Record CPU, architecture, OS, memory, power/thermal conditions, toolchains,
   flags, GC settings, heap limits, build hashes and source revisions. Run without
   competing builds or agents. Record noisy runs rather than selecting only wins.

Keep ordinary optimization enabled in the headline comparison. A separate
diagnostic variant may prevent inlining to expose a call body, but its timing
cannot substitute for the normally optimized result. Record LTO and any profile
guided optimization, including training inputs. Use comparable native toolchains
for Cilica/IL2CPP where feasible; NativeAOT uses its own code generator, so this
is a compiler/runtime-system comparison rather than an identical Clang experiment.

Keep required null, bounds and overflow behavior enabled. IL2CPP exposes options
to remove some runtime checks; results with those options are a separate semantic
configuration, not evidence of an equivalent optimization.
[Unity runtime checks](https://docs.unity.com/en-us/engine/6000.6/manual/scripting/compilation-and-code-reload/script-compilation/backends/il2cpp/il2cpp-runtime-checks)

### Metrics and their limits

| Metric | Collection and reporting rule |
| --- | --- |
| Elapsed time | Monotonic clock around a batch; divide by its verified operation count |
| CPU time | OS process user/system CPU deltas over the same phase; thread CPU is an optional separately named metric |
| Startup | Controller launch timestamp, child ready signal, then first verified result; state whether input parsing is included |
| Resident memory | OS measurement at ready state, during workload and after cleanup; report units and sampling interval |
| Peak memory | Distinguish process-lifetime high-water mark from sampled workload peak; fresh processes separate phases where needed |
| Managed memory | Distinguish allocation traffic, live objects, committed heap and reserved capacity; absent counters are unavailable |
| GC behavior | Collection count and pauses where trustworthy instrumentation exists; gather intrusive traces separately |
| Native code and metadata size | Symbol/linker/section evidence with shared code identified; retain total deployment size too |
| Deployment size | Binary, required runtime/data/dependencies and compressed package separately; debug symbols separately |
| Build duration | Restore, managed compilation, translation, native compilation and linking where separable; clean versus incremental |

Do not compare Cilica's internal logical heap counters directly to another
runtime's process RSS. OS memory definitions also differ across platforms.
Sampling can miss brief peaks. Optional hardware counters can investigate
instructions, cycles and cache/branch misses where available, but event names,
multiplexing and permissions must be recorded. CPU percentage and instruction
counts alone do not explain performance.

Measure an empty adapter to expose harness overhead; avoid automatically
subtracting two noisy tiny timings. Distinguish one-call latency from batched
throughput. Whole-player size and startup answer deployment questions, while
method code size answers a narrower compiler question.

## Structural analysis and optimization experiments

For every probe, assemble a traceable inspection record containing source,
input assembly hash and method signature/token, decoded IL, Cilica IR/C++,
IL2CPP C++, NativeAOT diagnostic records, and each final native disassembly.
Include the build IDs and a mapping of methods to symbols or inlined ranges.
Generic sharing and inlining make that mapping potentially many-to-many.

Start the chain at Roslyn's actual emitted IL. Preserve the Release input DLL,
its SHA-256/MVID, matching portable PDB and its compiler-version/options record,
plus the build recipe (optimization, target framework, language version and
conditional symbols). Read IL from that exact DLL rather than reconstructing it
from decompiled C#. Match methods by assembly hash, token and signature; use RVA
and IL offsets to join disassembler output to Cilica's generated method/offset
comments. Keep pre-link input and Unity's stripped assembly identities separate.

For each sampled path, distinguish source operations, Roslyn lowering, selected
core-library implementation, Cilica IR/emission, native compiler output and runtime
bookkeeping. Inspect property/accessor calls, constrained calls, boxing, temporary
locals/addresses, closures, iterators, exception regions and loop branches. An IL
`callvirt` to a nonvirtual accessor must preserve null behavior but does not by
itself require dynamic dispatch. Compare with final native symbols/disassembly
before concluding that an emitted abstraction costs time. JIT and NativeAOT
optimizations require their own runtime/compiler evidence; Roslyn emits IL and
cannot explain those backends' final machine code by itself.

When the lowering choice is unclear, inspect Roslyn's lowering/code-generation
source at the compiler revision recorded in the PDB. REA's managed artifact/member
inspection can independently index the compiler assembly and its calls/CIL without
executing the inspected assembly. Read repository source directly; REA does not
currently provide general C# repository-source navigation. If the recorded revision
is unavailable publicly, record that gap and label another pinned source revision
as comparative evidence, never an exact source match. No global MCP registration
or long-running native-analysis service is necessary for static managed inspection.

Describe equivalent logical operations rather than diffing symbol names or
raw addresses. Compare control-flow blocks, call sites, guards, initialization,
frame setup, copies, allocations and exception paths. Correlate these observations
with sampled hot paths and measurements. A verbose C++ expression may disappear
entirely in native output; fewer source lines are not a performance result.

| Probe | Structural question | Candidate Cilica experiment | Correctness gate |
| --- | --- | --- | --- |
| Checked/unchecked numeric conversions | Which masks, extensions and range checks survive? | Simplify redundant conversions using known value ranges | Signed/unsigned boundaries, checked overflow and exception order |
| Direct calls and tiny loops | What remains of frames, argument packets and failure checks? | Reduce proven redundant setup or batch helpers | Depth/storage budgets and failure propagation |
| Struct parameters and returns | Where are copies, spills and hidden result buffers? | Eliminate dead copies or simplify caller-owned results | Aliasing, mutation order and embedded-reference roots |
| Class/interface dispatch | Is the receiver proven exact, and how many lookups remain? | Resolve proven targets directly or reduce lookup work | Null behavior, slot identity, overrides and reachable targets |
| Closed generics | Which bodies are specialized or shared? | Investigate sharing when specialization materially inflates size | Layout, value/reference distinctions and dispatch semantics |
| Boxing and unboxing | Where do allocations, copies and type tests occur? | Eliminate only proven unobservable temporary boxing | Identity, aliasing, casts, exceptions and allocation semantics |
| Arrays and loops | Which bounds checks repeat and can the array reference change? | Hoist or remove checks only with a valid proof | Negative indexes, covariance, fault order and body side effects |
| Object allocation and roots | What work occurs for every allocation/call? | Reduce redundant root publication or bookkeeping | Live references across safepoints and forced collection |
| Exceptions and metadata | What work remains on the nonthrowing path? | Move proven cold work or cache valid initialization results | Handler/filter/finally semantics, initialization and fault order |

Rows are hypotheses, not promised gains or claims that every form is supported.
Only run the precise forms admitted by the compatibility manifest. Compare
runtime data structures using definitions/debug information plus validated
observations; never assume object headers or field offsets match across engines.

Use this experiment sequence:

1. Identify a measured cost in Cilica and the corresponding generated/native
   structure. Record the comparator observation and at least one alternative
   explanation, such as compiler flags, framework code or host activity.
2. State a falsifiable hypothesis: the specific redundant work, the conditions
   under which it can be removed, and the metric expected to improve.
3. Implement one coherent Cilica change using its own IR/runtime abstractions.
   Preserve before/after commits and configuration. Avoid copying vendor source;
   explain the independently implemented algorithm and evidence.
4. Run focused .NET differential checks and native O0/O2 sanitizer tests where
   appropriate. Include exception order, identity, boundary ownership and
   forced-collection cases affected by the change. Add a reduced regression
   for any discovered correctness bug.
5. Compare baseline and candidate using the same harness and input corpus.
   Include an adjacent workload that could regress and the relevant size,
   memory, startup or build-time cost. Use an on/off variant when practical to
   isolate causality without maintaining a permanent new public option.
6. Review the implementation and evidence before committing. Accept a gain only
   when it exceeds observed noise and the trade-off fits the product priorities.
   Record inconclusive and rejected experiments as well as accepted ones.

Report both absolute values and ratios. Speedup is baseline elapsed time divided
by candidate elapsed time; time reduction is `(baseline - candidate) / baseline`.
Use those names consistently. Do not sum percentages from separate changes or
promise an end-to-end gain from an isolated helper. As a planning illustration,
halving a component that occupies 20 percent of runtime reduces total time by
10 percent, assuming other costs remain fixed. This is arithmetic, not a result.

Keep a proposed experiment ledger with hypothesis, exact builds, correctness
evidence, raw sample locations, structural evidence, regressions and decision.
Update architecture decisions and compatibility documentation only when a real
implementation change or new validated capability warrants it.

## Proposed implementation slices

| Slice | Deliverable | Exit evidence |
| --- | --- | --- |
| 1 Shared benchmark contract | Manifest, adapter lifecycle and versioned results built around the existing harness | Existing workloads retain checksum verification; failures and unavailable metrics are explicit |
| 2 NativeAOT comparator | Executable/shared-library adapters, artifact capture and initial time/CPU/memory measurements | Same inputs match .NET/Cilica; symbols resolve an entry; raw local results reproduce |
| 3 Unity IL2CPP comparator | Minimal player adapter, empty-player baseline, artifact discovery and preservation | Marker maps to C++/native output; results match; host costs and missing metrics are disclosed |
| 4 Structural probe corpus | Numeric conversions, calls and supported struct probes, then other admitted cases | Traceable IL-to-native records explain at least one measured difference without speculative speed claims |
| 5 First optimization | One Cilica improvement selected from measured cost | Differential/sanitizer evidence, before/after performance, adjacent regression checks and slice review |
| 6 Representative libraries | Pinned real-library intake and deterministic scenarios | Coverage/failure report plus verified measurements; no hidden behavior changes |
| Optional extraction investigation | Standalone IL2CPP feasibility record | Repeatable independently running artifact with declared dependencies, or a documented stopping reason |

The optional investigation follows slice 3 and does not block slices 4 through 6.
The first delivery should demonstrate one complete, repeatable comparison rather
than implement every counter and platform. Existing execution-frame results keep
their original scope when incorporated into broader reports.

Proposed organization, not files or commands already implemented:

```text
benchmarks/Comparative/
  Workloads/          shared kernels and library scenarios
  Manifests/          pinned inputs, capabilities and expected results
  Runner/             bounded build and execution orchestration
  CilicaAdapter/      compiler-library integration and private benchmark wrapper
  NativeAotAdapter/   executable and shared-library entry projects
  UnityAdapter/      minimal project and explicit build entry
  Inspection/        artifact discovery and normalized analysis

artifacts/comparative/<run-id>/
  manifest.json       complete configuration, hashes and outcome
  inputs/            provenance and immutable input inventory
  builds/            captured outputs, symbols and commands by backend
  correctness/       expected and actual results
  samples.jsonl      raw measurements with phases, units and process identity
  inspection/        mappings, disassembly and structural findings
  report.md          comparisons, limitations and experiment decision
```

Use a new output directory for each run. Failed/incomplete runs retain their
status and cannot be mistaken for current successful results. Keep large generated
outputs outside source control under the repository's artifact policy; commit
recipes and concise evidence records. The manifest links original and normalized
artifacts by hash and records symbol matching, not merely matching filenames.

## Resource limits and delivery review

All work is local and opt-in by default. Keep normal tests free of performance
thresholds and expensive comparator builds. Do not enable hosted workflows,
purchase services, install paid tooling or start additional agents under this
plan. A future push capable of hosted work needs an aggregate runner-minute
estimate and an explicitly authorized budget.

Before each execution session, choose the workload subset and record build,
runtime and artifact-storage limits. Begin with one Release build per backend
and at most one diagnostic configuration when needed. Reuse identical hashed
artifacts. Default to the existing 60-second measurement-child limit; establish
separate explicit build limits after checking toolchain needs. Count all repeats,
warmups and calibration toward runtime limits. Long-lived GC cases need their
own declared limits and cannot silently extend a pilot.

Inspect failures before repeating expensive builds. A second attempt needs a
concrete fix or hypothesis; repeated environment failures stop that slice.
If profiling permissions, symbols or tooling are unavailable, keep unaffected
work usable and report the gap. Harness-owned disposable test processes remain
bounded and are cleaned up by that harness; any persistent development services
must follow repository Cawtex rules.

Review each completed slice for managed semantics, C++ undefined behavior,
rooting across allocations, native ownership and measurement fairness. Commit
coherent reviewed slices without including unrelated work. Inspect main before
integration. A final report must identify the exact source/builds, supported and
unsupported workloads, correctness failures, sample counts, absolute metrics,
ratios, host/runtime overhead, missing counters and practical limits of each claim.
