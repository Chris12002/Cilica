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
| Executable conformance | Compare controlled programs on .NET and Cilica, including state and exception traces. | Native Int32 results and primitive division-fault propagation present; full managed exception traces remain planned. |
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
