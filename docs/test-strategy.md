# Cilica test strategy

Recorded 2026-09-29. This is the testing policy and implementation roadmap,
not a claim that all the tests below exist or that the planned runtime works.

## What the tests must establish

Cilica must preserve managed behaviour and report unsupported reachable
behaviour explicitly. Tests should establish observable contracts independently
of the implementation: what an input means, what the compiler includes, what a
program does, and what the user learns when compilation cannot proceed.

The current explorer has useful metadata and IL fixtures, structural
regressions, output snapshots, seeded corruption tests, assembly-loading
protection and real-library inspection checks. The corrective slice after 1b
passed 103 local tests. That is a baseline, not a completeness metric: the
review found errors despite all 72 previous tests passing.

The main improvement is to test acceptance and rejection together, then test
interactions between rules. A validator that rejects every input is incorrect
even if all its malformed-input tests pass.

## Evidence at each layer

| Layer | Evidence and limits | Status |
| --- | --- | --- |
| Metadata and IL rules | Small inputs with independently specified expected results and diagnostics. Cover both valid and invalid forms. | Present; expand with each rule or defect. |
| Rule interactions | Prefixes at branch/exception boundaries, nested handlers, stack joins and filter control flow. | Started in the 1b corrective slice; not exhaustive. |
| Report and CLI contracts | Reviewed text/JSON snapshots, deterministic ordering, schema version, exit codes and useful locations. Snapshots establish presentation, not semantic correctness. | Present for `inspect`, `il` and `audit`. |
| Malformed-input robustness | Targeted corruptions plus small, seeded mutation runs. A run without crashes does not establish that all invalid inputs are rejected. | Present; larger runs remain opt-in. |
| Resolution and reachability | Small assembly graphs with known resolutions, included/excluded members and inclusion reasons. | Binding, ordinary graphs, bounded dispatch (including closed named constrained instance calls) and audit reporting present in 1c; the initial local application audit records unresolved closure gaps. |
| Real-library inspection | Checks for false rejection and practical scale on pinned compiler output. Acceptance alone does not prove complete verification or executable support. | Present, including CoreLib; wider/private runs are task-specific. |
| Executable conformance | Compare controlled programs on .NET and Cilica, including state and exception traces. | Native Int32 results and division/remainder/checked-arithmetic fault propagation present; full managed exception traces remain planned. |
| Detached object/reference contracts | Independent layout fixtures and caller-forged typed IR; acceptance/rejection, joins, exact identities, closure, fault edges and aggregate limits. These checks do not prove reference execution. | Present in 3a/3b-a; object bindings/operations extended in 3b-b. |
| Runtime and embedding | Allocation stress, native sanitizers, boundary ownership, lifecycle and native/browser parity. | Planned with the relevant milestones. |

## Designing a useful regression

For a new validation rule or discovered defect:

1. State the observable rule and its source: the design contract or the relevant
   CLI rule. Do not derive the expected answer by calling the implementation
   under test a second time.
2. Reduce the input to the smallest meaningful example. Use compiled C# for
   ordinary patterns and synthetic metadata/IL for shapes C# cannot express.
3. Include a rejected case and a nearby accepted case. Vary the boundary that
   matters: first/last valid token row, first/interior prefix, enclosing/nested
   handler, cycle/acyclic backward branch.
4. Assert the result, diagnostic identifier and relevant entity/IL location.
   Assert exact prose only where stable wording is the contract. Check that
   inspection preserves unaffected results after a recoverable error.
5. Add an interaction case when the rule shares state with another subsystem.
   A rule tested alone may fail when combined with exception regions, generic
   signatures or dispatch.
6. Confirm the regression detects the original defect when practical, then
   review the fix and its positive cases before committing.

Keep tests focused on public behaviour. Avoid tests that merely repeat helper
logic or pin private data structures. Code coverage can identify untested
branches, but neither a coverage percentage nor a large test count is an exit
gate. A small, targeted mutation of a critical condition can help check whether
tests would detect its removal; a broad mutation-testing campaign is not part
of the default resource budget.

## Slice 1c: dependency resolution and reachability

Build independent fixture assemblies and temporary directory layouts. These
tests must not require Alchemy, download packages during inspection, or probe
the developer's installed runtime implicitly.

The declaration-binding tests now include generic overloads that coincide only
after substitution, caller/target generic-variable separation, inherited generic
members and distinct same-named types from different assemblies. Authored C# test
cases provide compiler-produced member tokens and run on .NET to check expected
behaviour. The compiler still reads their assembly as data; only the test runner
executes its own fixtures. These are binding probes, not differential evidence
for a Cilica execution backend. Declaration/body checks alone do not establish
dispatch closure. Accepted/rejected nesting boundaries and seeded blob
corruption cover the bounded parser's diagnostic behaviour.

Ordinary graph tests now assert exact method sets and disconnected exclusions,
initialization/address/type dependencies, caller generic-context swaps, cycles,
limits and every node's explanation back to a root. Root permutations and copied
images retain the same graph. Reference-only and missing bodies/dependencies,
open generics and unsupported dispatch leave explicit issues. Seeded body
corruption exercises the integrated diagnostic boundary.

Bounded dispatch tests now cover receiver discovery before and after call sites,
including a target body that reveals another receiver, and receiver evidence on
implementation edges. They distinguish newslot hiding from ordinary overrides,
preserve generic variables during matching and exercise renamed/inherited
explicit interface bodies. Default/variant interfaces, class slot remapping,
synthetic receiver forms, cycles and limits retain diagnostics. A C#/.NET probe
checks compiler-produced override and explicit-interface targets. These tests
establish the documented bounded profile, not an unrestricted dispatch algorithm
or the filter audit gate.

| Area | Required cases |
| --- | --- |
| Explicit resolution | Resolve from supplied directories; diagnose missing, unreadable and malformed candidates. Put a dependency beside the input or in the working directory without supplying that directory and verify it is not silently found. |
| Assembly identity | Distinguish same-named assemblies by version, culture and public-key token. Exercise the documented duplicate/conflict policy and any explicitly configured framework mapping. Never silently bind user types by short name. |
| Facades and forwarding | Direct, chained and nested type forwarding; missing targets; forwarding cycles; matching type names in different assemblies. Cycles terminate with a useful diagnostic. |
| Member resolution | Overloads, generic arity/substitution, inherited members and explicit interface implementations. A missing member names the requesting member and dependency. |
| Roots and ordinary edges | Explicit entry types and public-surface roots; calls, constructors, field/type dependencies, static initialization and delegate targets. Include a disconnected method as a negative control. |
| Dispatch expansion | Overrides and interface implementations, including an implementation discovered after its call site. Iterate until no further reachable members are found. Do not test only traversal orders that happen to discover types first. |
| Runtime type mapping | Select the library explicitly; test all primitive names, forwarding, namesake exclusion, conflicting hashes, invalid shapes and reader ownership. Assert exact primitive constrained targets and string discovery from literals and signatures. Keep arrays, reference-only bodies and budget exhaustion visibly incomplete. |
| Array receivers | Preserve vector/rank/element/bounds identity, nested and duplicate shapes, every signature/newarr discovery source, inherited and explicit base-interface bodies, reference gaps, open variables, intrinsic owners and edge rollback. A generic helper gap survives a successful named receiver and a generic interface constraint on a class operand. Verify against an authored .NET array enumeration probe. |
| Generic expansion | Shared and distinct instantiations, recursive calls, and unbounded expansion. A bounded diagnostic includes the instantiation chain instead of hanging or silently truncating results. |
| Inclusion explanations | Every non-root included member has a valid reason back to a root. Exercise multiple possible paths and cycles; output follows a documented deterministic choice. Mark conservative dispatch edges as such. |
| Feature reporting | Known reachable and unreachable examples of filter kinds, unsafe operations, threading, async, floating-point/decimal, reflection and generic virtual methods. Feature presence is an audit observation, not a support claim. |
| Incomplete analysis | Missing dependencies, reference-only bodies, runtime-provided methods and unsupported dynamic edges remain visible. An incomplete closure must not look like evidence that a feature is absent. |
| CLI and determinism | Text/JSON results, selected roots, explicit reference directories, diagnostics and exit codes. Repeat from different paths and directory enumeration orders. Test input-order invariance only where order is not part of the documented binding policy. |

For a graph with only direct edges, assert the exact reachable set. For
conservative dispatch, assert the required set and documented allowed extra
targets, plus exclusion of unrelated members. Including everything must not
pass as a useful reachability implementation. Conversely, a small result must
not pass if it silently omits required implementations.

Reference assemblies describe APIs; their bodies are not evidence about library
implementation behaviour. An audit using only reference packs cannot establish
that the implementation contains no general filters. Record the root set,
assembly identities and content hashes, reference/implementation roles,
binding policy and analysis limitations with audit evidence.

The compiled primitive probe validates reference behavior, constraint/declaration
identities and the concrete Int32 implementation reached before its broader closure
limit. Reduced graphs independently exercise both directions of scheduling pressure
and fairness between sites. They verify body observations, valid inclusion chains
and deterministic partial reports with small budgets. Do not convert a successful
target or a budget limit into a successful completeness assertion.

The local Alchemy run comes after the independent graphs pass. Keep its reports
under ignored `artifacts/` and commit only non-private conclusions. Inspection
of Microsoft's runtime binaries can identify porting risks, but does not prove
the closure of Cilica's future ported library. Slice 1d validates that closure
against the trial library build. General filters in the required ported closure
make ADR 0004 stage B a prerequisite for milestone 6; incomplete analysis leaves
the question open rather than clearing the gate.

## Initial Int32 IR translation

Slice 2a independently tests ordered typed values and source provenance, constant
and argument encodings, stack/return failures, unsupported instructions/signatures,
initializer and reference/native obligations, method-input bounds and session
ownership. Its fixture-only IR evaluator compares an authored arithmetic method
with .NET at normal and wraparound boundaries. This is translation evidence;
slice 2b now compares compiled native output directly with .NET. Keep the evaluator
out of production compiler code and never execute arbitrary inspected input.
The banned-API analyzer and binary scan cover `Cilica.Compiler` as well as metadata
and CLI packages. See [ADR 0014](decisions/0014-straight-line-int32-ir.md).

## Object layout foundation

The object foundation is tested separately in `ObjectModelTests` and
`TypeInputTests`. Independent metadata graphs verify field order/readonly flags,
logical reference-slot maps, primitive OBJECT selection, cyclic fields,
cross-image namesakes, forwarding and detached lifetime. Tests reject unsupported
type/storage shapes, nested generic owners, malformed signatures and foreign
readers. Exact/excess type, aggregate field, metadata-work and owner-depth limits
are exercised. Caller-forged graphs must fail for missing/unrelated targets,
invalid tokens/owners, duplicate image-scoped FieldDefs and unknown storage.

These are layout evidence, not reference execution or GC evidence. Future typed
IR needs reduced join/access/null/fault-order cases; native allocation needs
.NET differential fixtures plus root-frame lifetime, instance isolation and
sanitizer checks. GC stress remains a milestone 4 gate. Keep the existing Int32
native path intact. See [ADR 0022](decisions/0022-detached-object-layouts.md).

## First native execution

`CppEmissionTests` rejects malformed caller-constructed IR, bounds values and
parameters, checks deterministic output under a different culture, and keeps
untrusted metadata display names out of C++ syntax. CLI cases cover exact roots,
source/file output, write errors, unsupported operations and invalid/missing
inputs without destroying an existing output artifact.

`NativeExecutionTests` translates 32 independently authored C# entry methods and
five reduced IL probes, checks their CLI source against the emitter, compiles one
translation unit per configuration, and compares 16,252 native results with direct .NET fixture calls
and the zero-local probe's independent expected constant.
The test runner owns the authored reference execution; production parsing and
emission never load or invoke input code. There are 100 boundary combinations
and 256 seeded tuples per method. Add/subtract/multiply, a compound expression,
signed boundary constants, unused parameters, signed/unsigned comparisons,
branches, loops, stack joins, shared helper closures, cross-assembly calls, argument
capture before mutation, bounded direct/mutual recursion and bitwise/shift
composition run at `-O0` and `-O2` with undefined-behaviour sanitizer on the optimized build. Require exact result count
and order, zero exit status and empty runtime stderr.

Native processes use argument lists rather than shell interpolation, a default
30-second timeout, and at most 262,144 captured characters per output stream.
Timeout/overflow tests terminate only their own process handles, wait for exit
and allow temporary-directory cleanup. A missing compiler fails the tests;
do not silently skip them while claiming native evidence. Failures include
compiler version and flags. The recorded toolchain/target and current support
boundary are in the [compatibility manifest](compatibility.md) and
[ADR 0015](decisions/0015-int32-cpp-emission.md). Other targets remain untested.

`PrimitiveExampleTests` runs the documented example script with prebuilt inputs
in a bounded, test-owned process. Unlike the larger in-process CLI probes, this
checks the external CLI, derived root/entry identifiers, prepared headers, native
driver and exact .NET output comparison together. An output directory containing
spaces tests argument quoting. Four fixed expected outcomes pin normal execution,
helper overflow, recovery after a fault and conditional avoidance independently
of the generator. The helper runs only its authored build-time kernel; compiler
inputs remain data. The Release build is also checked locally using cached
restore inputs. This integration test supplements the boundary/seeded native
corpus; it does not replace arithmetic conformance or establish broader runtime
compatibility.
Reduced shell cases intercept the initial build with a disposable tool stub,
checking both normal and offline argument lists without contacting NuGet. They
cover a Bash 3.2 `set -u` failure discovered with an empty options array; the
runner now initializes that array with its required build flags.

Control-flow/call probes bound C# loop and recursion counts to -32..32 and the
reduced live-stack loop to 0..31. An optimized fixture project avoids Debug C# Boolean bookkeeping
locals, which remain outside the Int32 storage profile. Existing inspection
fixtures retain their original optimization setting. The harness's case table
keeps each input root and .NET oracle together rather than coupling them through
unexplained numeric indices.

`Int32ControlFlowTests` covers local encodings/initialization, rejected local
types, depth and operand failures, malformed branch destinations, incompatible
join depths, live stack backedges and every conditional encoding. Caller-forged
IR cannot bypass edge arity/type/target, local, predicate or reachability checks.
Exact accepted/rejected block, aggregate statement and conditional edge-copy
budgets include synthetic stack-input values. The profile only produces Int32
stack values; these checks do not establish a general CLI verifier for other types.

`Int32CallTests` checks content-scoped targets, shared/cyclic deduplication,
detachment and deterministic method ordering, explicit-directory resolution,
missing dependencies, transitive diagnostic paths and preserved output on failure.
Reduced graphs reject invalid call tokens, stack underflow, unsupported callee
bodies/signatures, generic owners/instantiations, access rules and inherited owners.
Exact method/instruction, aggregate statement and call-argument bounds have paired
accepted/rejected cases, including caller-constructed programs with small value
counts but many repeated argument references. Declaring-owner snapshot bounds,
forged targets/arity/operands/closures and unreachable methods are checked.
See [ADR 0017](decisions/0017-bounded-static-call-closure.md).

`Int32BitwiseTests` covers six binary opcode mappings/operand order, unary
complement, every missing-operand shape, forged enum/operand records and continued
rejection of short/wide argument stores. Native bitwise composition exercises
calls and initialized locals in loops. Seven shift probes add ten boundary values
by 44 counts, covering every count 0..33, negative counts, wrapping boundaries
and Int32 extremes. Three reduced IL methods omit C#'s explicit mask; independent
.NET DynamicMethods emit only their authored load/load/shift/return instructions
and serve as the runtime oracle. They never decode/load compiler input bytes.
This checks backend masking directly rather than only observing the C# mask.
See [ADR 0018](decisions/0018-int32-bitwise-and-shifts.md).

Sub-slice 2e-b adds a separate authored fault harness with ten entries and 3,630
outcomes per native configuration. It compares direct signed/unsigned division,
transitive/cross-assembly propagation, competing faults in both evaluation orders,
loops, bounded recursion, conditional avoidance and a pure status entry. Expected
values or DivideByZero/Overflow kinds come from direct authored .NET calls;
unexpected exceptions fail the test. Origin hash/MethodDef expectations are
independent of translation, with a literal division offset 2. Faults must preserve
a sentinel result; success after a fault must clear stale origin/status in the
same process. All four null output pointers reject before execution or writes.
Both native builds disable C++ exceptions/RTTI, and the optimized build keeps
UBSan. Input batches keep richer output within the existing capture limit.
Reduced tests reject missing/unknown exceptional edges and pure definitions with
forged edges, verify division underflow/order and exercise both pure and throwing
recursive analysis cycles. See [ADR 0019](decisions/0019-primitive-pending-faults.md).

Sub-slice 2e-c extends this harness to 25 entries and 9,325 outcomes per native
configuration. All six checked add/subtract/multiply forms compare with authored
.NET code, whose unsigned casts are explicitly unchecked so conversion faults
cannot mask arithmetic results. Boundary vectors include signed product limits
46340/46341, unsigned limits 65535/65536, negative input bit patterns and full
UInt32 products exceeding Int64Max. Checked calls compose with loops, recursion,
conditional avoidance, other assemblies and division. Origin checks distinguish
first faults of the same kind at different methods, and two-operation compounds
at literal IL offsets 2 and 4. Existing sentinel/reset/null-output checks remain;
batches use 50 input tuples to preserve the capture bound. Reduced tests now
cover underflow, operation mapping, required failure edges and recursive effects
for each checked operation. See [ADR 0020](decisions/0020-checked-int32-arithmetic.md).

Sub-slice 2e-d adds remainder: 37 native entries now yield 13,986 outcomes per
configuration. Signed/unsigned direct operations, checked/unchecked C# contexts,
helpers, cross-assembly calls, loops/recursion, conditional avoidance, competing
overflow faults and chained remainder retain the full native path. Five new
tuples cover nonzero results in all sign combinations and a second-operation
zero divisor. Origin expectations include literal offsets 2/4; sentinel/reset
checks and the existing 50-tuple output batches remain.

Eighteen fixed contract cases also compare checked/unchecked C# and raw authored
.NET DynamicMethods with independent expected values/faults. They pin signed
MinValue rem -1 to Overflow on the selected .NET 10.0.11 ARM64 profile, even
though evaluating in a wider type would yield zero. Unsigned bit-pattern
interpretation has no signed overflow case. These oracles never decode/load
compiler input bytes. Reduced IR tests extend mapping, underflow, failure-edge
validation and recursive effects to both remainder forms. See
[ADR 0021](decisions/0021-int32-remainder.md).

These checks validate primitive propagation and ABI behaviour. They do not prove
managed exception objects, messages, identity, catches, filter or finally order.
Future slices add ordered exceptional traces as those semantics exist.
Preserve this complete native path; an IR evaluator alone is no longer sufficient
execution evidence. See [ADR 0016](decisions/0016-int32-control-flow.md).

## Executable semantics and the managed runtime

As each operation becomes executable, compare a controlled fixture on .NET and
Cilica. The reference process is separate from the compiler and has a timeout;
the compiler never loads or executes inspected input assemblies. Use a pinned
runtime/profile and record it with failures. Do not treat .NET accepting an IL
body as proof that it satisfies every verification rule.

Compare return values, mutations and ordered event traces, including exception
types, identity, filter selection, `finally`/`fault` order and rethrows. Include
overflow, division, null and bounds cases at the point they should fail. Match
library configuration such as invariant globalization. Exclude unspecified
behaviour such as addresses, and document exclusions such as stack-trace text.
Do not exclude an inconvenient specified result just to make a test pass.

When available, run the same reduced cases with optimizations enabled/disabled
and allocation stress enabled. GC tests exercise cycles, live roots across
allocations and exception propagation, interior references and stale host
handles. Generated C++ gains sanitizer checks for undefined behaviour and
ownership errors. Add platform configurations as their implementations arrive;
do not create an expensive cross-product before there is behaviour to validate.

Use deterministic generated valid programs later to explore combinations and
shrink failures. Keep generation and execution bounded. Every useful generated
failure becomes a small permanent regression, independent of the generator.

## Local execution and resource policy

- During development, run the affected tests. Before completing a code slice,
  run the applicable local suite, inspect the diff and record the results.
  After a successful final run, repeat only for a relevant change or unresolved
  concern. Documentation-only changes need link/diff review, not runtime tests.
- Keep the default seeded corruption runs small (currently 500 iterations per
  corruption test). Use `CILICA_FUZZ_ITERATIONS` only for a deliberate larger
  investigation; record the seed, input and toolchain and reduce each failure.
- Golden updates require an intended report change and a reviewed diff.
  `CILICA_UPDATE_GOLDEN=1` must not become a way to approve unexpected output.
- Inspect failure logs before retrying. An environment failure needs an
  environment fix or another supported local runner, not repeated blind runs.
- Hosted CI remains disabled. A future authorized hosted run needs an aggregate
  runner-minute budget covering every job, matrix entry and retry. Do not add
  services, paid plans or spending increases as part of this strategy.

See the [development commands](../README.md#development) for normal build/test
commands and the local runner fallback. No tests in the present explorer suite
require a long-running development service. Future services follow the Cawtex
rules in [AGENTS.md](../AGENTS.md).

## Evidence required to finish a slice

A completed slice has reviewed accepted/rejected cases, reduced regressions
for discovered defects, relevant integration coverage, passing local checks,
and documented limitations. CLI changes also need output/exit-code checks.
Semantic claims need differential evidence when executable support exists.
Record the input/toolchain and scope of wider smoke runs so historical evidence
is not mistaken for coverage of a different library version or configuration.

Review and commit coherent changes, update the milestone record, and keep
private artifacts out of Git. Test success supports the stated scope; it does
not expand the advertised compatibility profile automatically.

## Typed reference IR and the next execution checkpoint

Slice 3b-a tests detached value-flow contracts independently of the future CIL
translator. Pair accepted null/class-to-object assignments with rejected
integer/reference mixes, sibling assignments, implicit downcasts and retagged
loads/call results. Exercise both edges of joins, typed backedges, recursive
calls, exact content identity despite equal names, missing fault edges and
catalog/argument/stack-copy bounds. Validate forged callee signatures without
allowing a null member to crash the compiler API. The primitive emitter must
reject reference/null IR before an artifact is returned.

These are structural validation claims, so no reference evaluator or native
object execution is claimed. Keep the existing .NET/native Int32 differential
corpus passing while the shared IR evolves. Slice 3b-b must add independent CIL
fixtures and reduced access/readonly/constructor/evaluation-order regressions;
slice 3c must compare a created, mutated class passed through calls against .NET,
with native sanitizers and root/lifetime checks. A passing IR validator cannot
substitute for either checkpoint.

## Slice 3b-b: object operation translation

Use synthetic IL for constructor and field boundaries, and authored optimized
C# for a complete small object program. Pair valid constructor initialization,
readonly stores and private same-owner access with missing/repeated base calls,
readonly writes outside the constructor, local receiver aliases, cross-owner
private access, wrong receiver/value types and invalid declaration flags. Forge
operation IR to verify targets, argument arrays, types and every fault edge.
The initial slice rejects virtual/native/reference bodies, initialization,
generics and unsupported control flow explicitly. Class virtual calls are expanded
under slice 3d-d-b below; other unsupported cases remain explicit diagnostics.

The current authored `ProbeNode` probe compares .NET with a bounded test-only IR
evaluator for allocation order, Int32 mutation/wrapping, readonly initialization,
reference fields, void calls and a null field load. Authored raw IL in the test
process checks the direct-null-call/nonvirtual-callvirt distinction and null
identity comparisons. No inspected input executes in compiler packages. This
checks translation, not native allocation, instance ownership, root frames or
collection; slice 3c still requires the actual native path and sanitizer evidence.

Reduced resource regressions cover exact/excess allocation plus void argument
copies, aggregate fields despite cached snapshots, bounded member candidate
scans, generic enclosing method owners and member formatting before excessive
owner recursion. A shared metadata ledger prevents repeated binding scans from
bypassing work limits. Preserve the complete primitive native corpus while the
shared IR and input reader change.


## Slice 3c: native objects and lifetime

Compile authored C# through the object translator/emitter and compare results and
fault kinds with .NET at O0/O2 under ASan/UBSan, with native exceptions/RTTI disabled.
Check exact reported IR origins as a separate emitter contract. Cover receiver
and argument roots in allocating methods, reference returns before later allocation,
new objects rooted before allocating constructor entry, and children retained by
reference fields after constructor frames have popped. Observe root/field maps
at allocation points rather than relying on successful execution in a retained heap.

Native checks cover distinct identity, zeroed fields, target descriptors, foreign
instances/arbitrary pointers, invalid roots, byte/count limits, partial constructor
failure, normal/fault frame cleanup, teardown callbacks and unchanged boundary
results on failure. Raw authored IL covers direct null calls versus nonvirtual
callvirt, null/same/distinct comparisons and the shared integer self-comparison
regression. Test bounded recursion against the native resource contract without
running an unbounded .NET oracle. Pair exact/excess root-frame limits and reject
valid IR control flow before source is returned.

The heap currently retains objects until teardown. These tests prove publication
and ownership in that profile; they cannot replace future collect-at-every-allocation
and poisoned-reclamation stress. Preserve all primitive/native regressions after
sharing arithmetic lowering. Slice 3b-c next adds reference control flow; milestone
4 remains responsible for precise collection. Wider ADR 0001/0002 experiments
and browser/mobile claims remain gated on their own toolchains and evidence.


## Slice 3b-c: reference branches and loops

Pair null/class and sibling-to-Object joins with incompatible depths/kinds and
uses that become invalid after a backedge widens a provisional type. Check
revisited calls near the 65,536-argument limit without charging them twice, and
inference-work rejection for methods whose input/final IR otherwise fit. Constructors, ordered reference
branches, argument stores, switches and exception transfers remain explicit gates.
Root keys distinguish inputs and value IDs from different blocks; existing native
frame limits apply to their aggregate.

Run independently authored C# object selection/traversal/identity/arithmetic loops
and raw IL with stack-carried reference swaps against .NET at O0/O2 under
ASan/UBSan. Bound authored loop counts independently of translation. Inspect
published references at allocations, validate fault origins and unchanged boundary
results, and force allocation failure from within a loop to check frame teardown.
The primitive CFG/native corpus remains required after sharing branch partitioning.
These retained-heap tests do not replace future reclamation/poisoning stress.

## Slice 3d-d-b: class virtual execution

Compare independently authored class virtual calls with .NET at O0/O2, including
ordinary/inherited/sealed overrides, newslot hiding, nonvirtual hiding, overloads,
property getters and direct base calls. Cover Int32/reference/void returns,
argument-before-null faults, exact method/IL origins and unchanged boundary results.
An allocating override must introduce a receiver class absent from the first
closure pass; verify its newly selected body is translated and executed.

Observe returned references in caller roots across later allocating overrides.
Force every allocation failure stage, exact/short frame budgets and depth limits;
verify cleanup and reuse. Run virtual recursion on a bounded native thread stack
with ASan/UBSan and the existing 2 KiB frame warning. Keep descriptor lookup off
the live invocation chain. Distinguish equal-name/equal-token slots and bodies
across images, and preserve direct null calls where the body does not dereference.

Forge incomplete/duplicate/unknown receiver maps, wrong implementations, slot
identities/names/final flags and missing null/dispatch/fault facts for value and
void calls. All must fail before C++ is returned. A reachable unsupported override
must produce a build error containing its virtual inclusion path. Preserve the
existing native/object/reference/primitive corpus. Interface/abstract/generic/
MethodImpl support and actual reclamation require separate evidence.

## Slice 3d-d-c-a: exact interface metadata

Use authored nongeneric C# interface maps and test-only dynamic calls as an
independent oracle for exact target selection. Check inherited mappings through
hiding, ordinary virtual overrides, reimplementation, private explicit bodies,
same-signature independent contracts, overloads, property getters, inherited
nonvirtual public bodies and diamonds. Compare canonical class slots separately
from effective MethodDefs. Reject unrelated contracts even when names match.

Raw metadata must cover duplicate members/edges/mappings, wrong explicit body
owners/signatures, cyclic/deep/wide interface graphs, generic/enclosing-generic,
default/static/abstract-class shapes, class/interface remapping and foreign
readers. Pair exact and one-unit-short shared ledgers and exact/excess graph
limits. Cross-image equal names/tokens must retain distinct contract/body identity.
Preserve exact class and conservative/constrained dispatch regressions after
sharing slot-walk code, including existing native class-dispatch checks.
These are metadata checks: interface IR/native support still needs independent
reference-flow, ownership, roots, fault-order and execution evidence.


## Slice 3f-b: string metadata and intrinsic translation

Verify selected-library String identity through forwarding and reject equal-name,
equal-token lookalikes in other images. Cover primitive/named String references in
fields, locals, vectors, calls, class/interface signatures and cast/test targets.
Intrinsic recognition must compare the resolved owner, full signature and call
form without reading its method body; authored unsupported bodies establish that
boundary. Initializer/access/generic/overload failures must remain diagnostics.

Read user-string sizes before allocating their UTF-16 snapshots. Test empty values,
embedded zeros and lone surrogates, malformed lengths/flags/tokens, exact/excess unit
and distinct-content limits, cross-method occurrences and stack-inference revisits.
Pair exact/short metadata and combined method budgets. The native rejection gate
at that checkpoint is superseded by 3f-c. A test-only evaluator can compare
translation wiring and null/index fault order with authored .NET calls. Native
string identity, interning and immutable storage need the actual native path in
3f-c; roots across reclamation need the later collector milestone. Keep existing
array/object/primitive regressions.


## Slice 3f-c: native strings

Run the authored StringProbe against .NET and emitted C++ at O0 and O2 with
ASan/UBSan, warnings as errors, and static frame measurements. Cover raw UTF-16
including zeros/surrogates, null/content equality, literal identity across methods,
fresh concat identity and null/empty reuse, fields/vectors, virtual/interface calls,
casts and reference joins. Check fault kind and method/IL origin, including index
argument evaluation before null, and packed-boundary result preservation.

Private harness checks exact/one-short header-plus-buffer budgets, object limits,
combined-length guards before buffer access, cache reuse at exhausted budgets,
instance separation, allocation failure without partial publication, and teardown.
Observe published concat operand roots before allocation. Check exact/one-short
execution arena budgets and clean frames after faults/retries. Reject foreign,
unreadable and copied-descriptor references before payload access. A reduced
metadata regression rejects String/framework-interface casts and covariant stores;
interface ancestry reads consume the shared metadata budget, including generic
parents whose nongeneric ancestors are unmodeled. Keep unrelated interface string
signatures working. These tests do not demonstrate roots across actual collection.


## Slice 3g: composed object example

Run the documented Objects script from the test harness at native O0 and O2,
including an output path with spaces. Compare the authored .NET oracle and native
output exactly and independently assert each expected success/fault line. Include
zero/maximum turns, different receiver selections, null/bounds/overflow failures,
and successful calls after failures. The oracle verifies its tiny fault helpers'
IL instruction shapes so method/IL expectations are independent of generated IR.
Keep arbitrary assembly execution out of the metadata-only emitter process.

Review generated arithmetic, roots around string-allocating virtual calls and
concat, field/vector stores and packed-boundary ownership. Validate the default
Release script separately from Debug test outputs. Use ASan/UBSan, warnings as
errors and the established native frame warning. Retain the primitive example
regression. No hosted jobs, benchmarks or collection claims follow from this
integration check.


## Slice 4a: precise reference visitation

Assert exact callback edge values and provenance for inherited fields, null/alias
slots, reference arrays, nested frames and literal-cache roots. String code units,
integer fields/vectors, ownership links and the heap allocation list must never
appear as managed edges. Compose the two visitors with a test-owned iterative
consumer over a cyclic graph; after removing frame roots, retained garbage must
be absent while cached literals remain.

Exercise complete/stopped/invalid outcomes, callback absence, foreign/unreadable
contained values, copied descriptors, owner mismatch, malformed readable frame/cache
chains, null slot buffers, and exact/excess frame/root/literal limits. Verify that
visits preserve heap counters, execution storage and pending fault/origin state.
The tests do not make arbitrary corrupted native backing storage valid input.

Observe every allocation in an authored .NET/native graph probe. Walk real caller
and callee frames, including unfinished receivers and allocating string calls;
verify the expected reachable graph before allocation. Force each allocation stage
to fail and inspect cache roots after frame unwinding without overwriting the fault.
Run O0/O2 ASan/UBSan and static frame checks, then relevant existing native/example
regressions, including programs with no String catalog. These are enumeration
checks; actual mark/sweep and poisoned reclamation stress remain later work.


## Slice 4b: bounded marking

Compare native classifications with an explicit expected graph and the test-owned
iterative visitor consumer. Include cycles, aliases, inherited fields, arrays,
nulls, literal-only roots and retained garbage; change roots between passes and
ensure no marks persist. Check counts and callbacks only appear after a full trace.
Force every small-graph edge-budget failure, exact/short object budgets, zero
limits, diagnostic stop and nested marking. Corrupt reachable values and readable
inventory metadata separately, then repair and retry. Preserve synchronization,
heap counters, published frames and an already pending managed fault/origin.

A 4,096-node aliased cycle verifies deep iterative traversal with exact work bounds.
Run marking at real allocation observers in the authored .NET/native graph fixture,
including partially initialized objects, nested calls, strings and forced allocation
failures. O0/O2 sanitizer and frame checks plus native/example regressions cover
the header-size change. These tests exercise marking, not sweep or collection stress.


Milestone 4 collection checks use real reclamation under O0/O2 ASan/UBSan. The
NativeCollectionTests corpus runs ten authored programs twenty times in one
instance with collection before allocation, compares .NET values and ordinary
native fault origins, and verifies post-unwind collection/accounting. NativeTracingTests
also verifies reclaimed header/buffer poisoning and stale-root rejection. Native
harness borrowers publish roots across pressure; raw locals are not GC roots.


The composed Objects example also checks the same probes in a persistent private
instance with collection before allocation. Allocation-budget regressions compare
against the live set at each allocation, allowing a correct .NET-equivalent result
when dead temporaries make room, while preserving below-budget failures and cleanup
checks. Final milestone 4 validation passed 196 selected local checks; details and
explicit evidence limits are in ADR 0054. The host is not deliberately exhausted to
force an operating-system allocation failure.

Milestone 5 scalar-location tests use authored IR with the existing object backend
and a separate C# oracle. O0/O2 ASan/UBSan runs retain the 2 KiB stack gate and check
local/argument/heap mutations, allocating synchronous borrowers and precise owner
roots under collection before allocation. Runtime carrier probes test expired-frame
and foreign-storage rejection separately. Native tests assert complete root/borrow/
arena cleanup on success and faults; detached tests cover access rights, invariance,
readonly/escape restrictions and exact resource bounds. This evidence does not
establish CIL byref translation or whole-struct mutation/returns.

Mutable value expansion adds O0/O2 oracle checks for nested struct mutation,
by-value isolation, snapshot swaps and self-aliasing. The native reference harness
supplies live objects, swaps embedded slots and checks reachability/reclamation;
it separately injects an invalid final borrow to verify rejection before mutation.
These authored value programs do not allocate managed objects themselves. Empty
layouts, readonly views and expanded carrier/statement budgets have focused
detached regressions. Struct returns and CIL translation remain separate gates.

Caller-owned return-packet probes now compare nested/chained struct returns with
C# at O0/O2, including divide-by-zero and signed-division overflow. They check the
packed output stays unchanged on faults, live returned references survive through
caller roots, invalid destinations fail before any write, and exact measured arena
capacity succeeds while one byte less fails cleanly. Empty returns and generated
parameter/local/root limits have detached tests. CIL and allocating value source
programs remain later gates.

Value CIL discovery tests read real return/borrow fixtures as metadata and use
reduced assemblies for private/hidden/enclosing-type access, private-scope fields,
readonly writes, unsupported signature shapes and per-method read limits. They
distinguish metadata discovery from executable stack validation (ADR 0064).

Bounded value CIL tests now feed real C# return/borrow/loop bodies through metadata
discovery, typed stack translation, mutable scalar expansion and native emission.
O0/O2 ASan/UBSan probes compare outputs/faults with .NET, including original IL
fault offsets and unchanged packed output on failure. Loop output observes loop
work. Reduced `cpobj` IL checks self-aliasing, disjoint copy and clearing the source.
Negative fixtures cover hidden type operands, readonly indirect mutation, exact
field/token types, invalid stack joins/indices/depth, unsupported boxing, dead
blocks and expanded statement limits. This evidence does not establish allocating
CIL value programs or collection with their live embedded references (ADR 0065).

Value/vector composition tests check allocation safepoints, embedded return roots,
scalar operand mapping, old-profile rejection and malformed allocation/call
contracts before expansion (ADR 0066). CIL/native stress remains separate evidence.

Value/vector CIL stress now translates actual C# struct programs with Int32 array
allocation into native code (ADR 0067). O0/O2 ASan/UBSan tests collect before every
allocation under an eight-object cap, compare normal/overflow/bounds outcomes with
.NET and verify original IL fault locations. A private caller removes its input
root before an allocating callee, verifies the sole parameter root transfers into
a rooted return destination, then checks final reclamation. Exact and one-byte-
short heap/arena budgets and a nested allocation failure check full scope cleanup
and context reuse. Negative CIL tests cover fake element identities, invalid
array/index/element types, unsupported element addresses and unpaired length
conversion; operation-only vector catalog roots are tested independently.

Value/object IR composition reuses translated class constructors inside authored
struct returns. Regression tests ensure missing/repeated base calls, readonly writes
outside constructors and malformed allocations fail before expansion. Earlier
profiles remain closed to class allocation (ADR 0068).

Value/class CIL probes now translate base/derived constructors, inherited fields,
readonly initialization and scalar heap borrows alongside static struct methods
(ADR 0069). Reduced assemblies reject malformed/missing/repeated base calls,
uninitialized receiver access, inaccessible constructors/fields/types, readonly
mutation, ordinary instance calls, inline heap values and constructor value flow.
A combined cached-class/value-field regression checks the shared catalog budget.

O0/O2 ASan/UBSan tests collect before every allocation, including allocations inside
constructors. Nested copies, borrowed replacement and struct returns preserve a
six-object graph; a composed success performs 33 allocations under a 16-object cap.
A sole callee-parameter root transfers to checked caller-owned output locations.
Clearing the last root reclaims the graph. Tests compare outputs and constructor
division faults with .NET, preserve original IL offsets and unchanged packed output,
check exact/one-byte-short heap and arena capacity, and reuse the context after
nested allocation failure. All scopes unwind and the 2 KiB stack gate stays enabled.

Detached boxing tests validate named-struct box allocation, exact unbox copies and
payload borrows through the shared verifier (ADR 0070). They check nested payload
reference paths, distinct copy/owner roots, allocation safepoints for empty boxes,
exact root limits, synchronous allocating helpers and readonly field projection.
Malformed operands, missing fault edges and escaping locations fail. Equal empty
layouts from distinct TypeDefs or images retain distinct identities. Null and
dynamically wrong box operands are accepted as faulting IR. Earlier profiles and
native lowering remain closed to boxing. These tests do not execute boxes; native
identity, copy isolation, fault order and collection need separate evidence.

Native boxed-payload helper probes now provide O0/O2 ASan/UBSan evidence (ADR 0071).
A .NET oracle distinguishes boxed identity, unbox copies and mutable payload
borrows. The native harness checks null-before-type status, exact descriptors,
foreign/invalid pointers, readonly capability and validation of the final reference
before any partial copy. Typed payload accessors use the same 2 KiB stack gate as
existing native code; oversized test harness functions are split, not exempted.

Collection stress keeps a cyclic boxed graph alive using only a borrowed owner,
including empty payload owners, and verifies reclamation after removing edges or
roots. Exact/one-byte-short heap budgets, object-count failure/recovery and busy
collection callbacks preserve the expected state. Detached tests reject mismatched
valid reference catalogs and combined class/box slot expansion above the native
limit. These tests exercise helpers directly; boxed IR emission and generated
fault-origin tests remain separate work.

Authored boxing IR now runs through scalar expansion and native emission (ADR 0072).
Verifier regressions reject malformed packet types/lengths, bad slot identities,
missing fault edges and old-profile access. Empty unbox checks retain owner roots;
an expansion regression enforces the target root limit even for zero-slot payloads.
O0/O2 sanitizer probes compare copying/mutation with .NET, preserve embedded
reference identity under allocation-time collection, forward empty borrows through
allocating static calls and check exact null/type fault origins. Packed output
stays unchanged on failure. Allocation failure and one-byte-short arena capacity
unwind all frames/borrows/roots; exact capacity and context reuse succeed.

Boxed-value CIL tests now translate real C# class graphs inside struct boxes and
reduced `unbox` mutation through an allocating static helper (ADR 0073). O0/O2
ASan/UBSan probes compare normal/null/type outcomes with .NET, verify callee/IL
origins and unchanged packed output, force collection during allocation and reuse
the context after box allocation failure. Negative cases check visibility,
readonly locations and operand/type shapes. Empty-struct metadata regressions
accept the C# fieldless sequential one-byte marker while rejecting nonempty sizes,
other sizes and packing. Earlier profiles still reject boxing.

Closed generic method regressions distinguish native instances of one MethodDef,
substitute nested calls and locals, deduplicate finite recursion and reject open,
constrained or expanding signatures with diagnostics. O0/O2 sanitizer probes compare
Int32/struct specializations with .NET, collect through live generic parameters,
box generic structs and empty values, and check specialized fault provenance,
unchanged failure output and full frame/root cleanup (ADR 0074).

Closed generic layout probes (ADR 0075) swap scalar/reference type arguments,
resolve fields and static methods on closed owners, nest layouts, borrow fields
and box distinct instantiations. Native O0/O2 sanitizer runs check .NET parity,
collection of embedded references and exact null/wrong-box fault origins. Reduced
fixtures reject hidden arguments, recursive containment and expanding layouts with
bounded inclusion diagnostics; older metadata-profile diagnostics remain covered.

Generic constraint regressions (ADR 0076) validate class/struct/default-constructor
requirements, nullable rejection, malformed flags, nested unused constrained
arguments and exact metadata budgets. O0/O2 native probes execute scalar-only
closures and constrained methods against .NET, checking collection and complete
frame/root/borrow/arena cleanup without constructor execution during compilation.

Closed generic reference probes (ADR 0077) combine two specialized class hierarchies,
readonly constructor fields, cycles, generic struct returns/boxes and mutable field
borrows. O0/O2 sanitizers check collection, arithmetic/fault parity, constructor
allocation failure and context reuse. Reduced tests reject base cycles and growing
reference layouts with bounded inclusion paths, retaining earlier catalog checks.

Delegate IR contract checks (ADR 0078) verify exact signatures and type identities,
private-target access, target-only reachability, captured receiver roots, void
invocation, explicit fault exits and phase A root/argument bounds. Earlier native
profiles must reject delegate catalogs until the native gate is implemented.
These checks do not establish native delegate execution.

Native delegate probes (ADR 0079) compare static and captured Int32/void calls with
.NET at O0/O2 under ASan/UBSan, including null capture versus invocation, target
fault origins, sole captured receiver retention, corrupt/foreign payload rejection,
fresh identity, unchanged packed output, allocation recovery and exact arena limits.

Delegate CIL checks (ADR 0080) validate bodyless intrinsic declarations, signature and
identity rules, paired construction within a block and explicit unsupported patterns.
Real C# O0/O2 sanitizer probes store captured delegates in boxed generic values,
collect cyclic receivers and compare successful calls plus null/target faults with
.NET. Null-only invocation covers an empty native target table.

Managed exception input checks (ADR 0081) cover scoped catch identities, nested
catch/finally/rethrow regions, general-filter rejection, exact metadata budgets and
illegal handler entry. Reduced filters distinguish current-evaluation local writes
from prior handler state, including branch joins and acyclic backward branches.

Authored exception IR tests (ADR 0082) check nested catches/finally, rethrow placement,
handler entry stacks, protected exits, exact runtime type maps, type visibility,
filter purity/cycles/prior state, and saved exception roots exceeding the 1024-slot
limit. Existing value/object/delegate/location verifier regressions remain covered.
