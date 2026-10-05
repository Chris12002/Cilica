# Milestone 6: Required library subset and real turn kernel

Status: in progress; slices 6a, 6b.1–6b.3 and 6c.1–6c.30 complete. Recorded 2026-10-05.

The exit gate is equivalent deterministic command traces from the real kernel
on .NET and Cilica's native runtime. Milestone 5 supplies bounded semantic
building blocks, not the whole library or all semantics used by the kernel.
Keep application rules in the external application checkout; public regression
fixtures and examples must be independent.

## Delivery slices

| Slice | Deliverable and gate | Status |
| --- | --- | --- |
| 6a | Explicit library-contract inventory, independent tests and private kernel dependency evidence | Complete; ADR 0088 |
| 6b.1 | Pin the reference/source baseline and validate explicit type correspondence | Complete for metadata plans; ADR 0089; no library source imported |
| 6b.2 | Apply the plan to structural member binding and preserve source/implementation evidence | Complete for metadata plans; ADR 0090; ordinary compiler binding is unchanged |
| 6b.2a | Explicit mapped compilation view, isolated from strict inspection, with a native probe | Complete; ADR 0091; mapped audit reporting remains a gate |
| 6b.3 | Validate build provenance and exact intrinsic grants at the compiler boundary | Complete for independently approved build attestations; ADR 0092; real build producer/outputs accompany 6d |
| 6c | Extend compiler semantics needed by the concrete kernel path: Int64, narrow scalars/enums, Guid-backed/inline values, value instance calls and closed generic interface/instance/array calls | In progress; 6c.1–6c.26 supply integer/narrow scalar, composed wide object/value, struct-constructor, direct class-instance and reference-vector CIL plus closed interface/class calls with values, same-image helper calls and generic memory operations; wide/narrow/value arrays, enum storage and broader instance/generic integration pending |
| 6d | Implement the required library members, beginning with list/read-only collection operations and their runtime prerequisites | Planned; expand to equality, dictionaries/sets, strings and formatting only as concrete roots require |
| 6e | Compile the unmodified kernel with a concrete adapter and run bounded deterministic command transcripts against .NET | .NET baseline captured from a fresh source build; native comparison pending |
| 6f | Audit the trial ported implementation closure, resolve the general-filter gate, run composed collection/exception stress and complete the milestone review | Planned; must precede milestone completion |

These slices are capability gates, not time estimates. Detailed requirements can
change when a concrete adapter supplies generic arguments and implementations.
Framework identity rules precede library imports; implementation-closure auditing
starts with the first trial library build, not only at the final review.
If required ported code reaches general filters, ADR 0004 stage B moves forward
before this milestone can close.

## Slice 6a outcome

`cilica audit --contract-only <assembly>` inventories application code while
deferring explicitly selected library bodies, layout fields and initializers.
It records exact identities/hashes, declarations, signatures, instantiations and
inclusion reasons. Library ancestry and direct application dispatch remain
visible. The report uses `explicit-library-contracts-v1`; no-issue status is
`contract-inventory`, never an implementation or compatibility verdict.
General-filter absence stays unresolved across library boundaries.

The existing external Release assembly snapshot produced the following private
inventories with the same default budgets and an explicit .NET 10.0.11 CoreLib
boundary. The source checkout revision and image hashes are recorded separately
in ignored artifacts; this checkpoint did not rebuild the application or claim
that its existing binaries matched that revision.

| Root selection | Nodes | Edges | Deferred nodes | Analysis issues | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| Kernel public surface | 732 | 3,808 | 302 | 579 | Incomplete: open generic environments, array helpers, interface/variance and dynamic-target gaps |
| Priority-state types | 153 | 777 | 96 | 0 | Bounded contract inventory; implementation and filter closure unresolved |

Neither inventory hit the node/edge limits. The smaller selection points to
list/read-only collection members, value IDs, generic interfaces and canonical
output dependencies. It is a conservative planning input, not a minimum member
set or an executable kernel trace. Private reports and reproduction notes live
under ignored `artifacts/`; no application source or member inventory is copied
into the public repository.

Fifteen independent boundary tests and related metadata/audit regressions pass
in a 203-check focused local run; 19 additional CLI checks also pass (222 total).
Review covered exact image admission, retained
application calls/signatures, deferred implementation uncertainty, partial-report
bounds and unchanged ordinary reports. No actionable findings remain in this
slice. No hosted CI, service, push or additional agent was used.

## Reference-profile inspection correction

During slice 6b reference-profile inspection, the inspector incorrectly decoded
locally defined `System.Type` attribute arguments as enums and printed null type
arguments as `typeof()`. A reduced fixture reproduced both defects. The correction
recognizes top-level local `System.Type` for attribute decoding and preserves null
type values, without granting compiler trust. Five regressions and the existing
inspection/CLI/corruption/no-execution checks pass (56 tests). The pinned .NET
Standard 2.1 reference image now inspects with zero diagnostics, down from 107
incorrect malformed-attribute reports. This changes inspection only.

## Slice 6b.1 outcome

The compiler-library metadata API now validates an explicit reference-to-
implementation type plan and projects TypeDef/TypeRef/TypeSpec identities through
it. It retains exact image hashes, names/nesting, generic arguments and structural
signature wrappers; unmapped selected types, changed image bytes and incompatible
headers/base types are diagnostic. Ordinary resolution and compiler behavior
remain unchanged. The plan grants no intrinsic permissions.

The [baseline record](../library-baseline.json) pins .NET Standard 2.1 declarations
and the .NET 10.0.11 source/oracle relationship. Imported-source and trusted-artifact
lists are empty. Twenty-two independent plan cases pass in a 173-test focused
binding/resolution/audit run; combined with the inspection correction, 229 distinct
checks pass. A local metadata-only probe also maps three real reference types to
independent implementation-shaped declarations. Review added changed-target-hash
coverage. No native output changed or hosted work ran.

See [ADR 0089](../decisions/0089-library-type-binding-foundation.md).

## Slice 6b.2 outcome

Explicit method/field binding now resolves the reference declaration first, maps
its owner and structural signature, and finds one compatible implementation.
Generic overloads are matched before substitution. Results retain original and
implementation identities and constructed signatures; missing or ambiguous
members, changed access/storage/dispatch flags and changed literal values fail.
Ordinary compiler binding remains unchanged; no intrinsic permission is granted.
Eighteen new cases and related regressions pass in a 112-test focused local run.
The slice review found no remaining actionable issue. See
[ADR 0090](../decisions/0090-library-member-binding.md). Compiler integration,
provenance validation and actual library behavior remain required next gates.

## Slice 6b.2a outcome

An explicit disposable resolver view now applies the plan during compilation,
including locals, fields and closed caller environments. It shares pinned images
without changing strict inspection or granting new intrinsic permissions.
An independent reference-call probe selects implementation IL and matches authored
.NET arithmetic under native O0/O2 UBSan. The focused scope/resolution/generic/audit
checks plus the closed caller regression pass (158 distinct tests). Review added
an audit guard until mapped reports retain binding evidence. See
[ADR 0091](../decisions/0091-library-compilation-views.md). Library imports,
provenance-backed grants and the real kernel trace are still unimplemented.

## Slice 6b.3 outcome

The compiler boundary now validates independently approved build attestations
against exact selected implementation images. Records include effective compiler
arguments, source/compiler/reference/options hashes and precise intrinsic grants.
Mapped string and exception substitutions require the applicable type/member
capability; a mapping or assembly name grants nothing. This validates an approved
attestation, not a proof that arbitrary source produced a binary. The actual build
producer and source provenance remain part of the first trial library import.

Thirty-five new admission cases and related regressions pass (137 local checks),
including native O0/O2 UBSan string execution and compiler-level missing exception
permission checks. Review added a signature-hash size bound. See
[ADR 0092](../decisions/0092-library-build-admission.md). The next delivery work is
the compiler/library behavior required by a concrete real kernel closure; no
library import or kernel/native equivalence has yet been established.

## Concrete kernel baseline and slice 6c.1

The external kernel and primitives were freshly built from their unmodified
component sources at revision `a8942127ee6d26545de6daa2259cc2793c13be8e`, using
an artifacts directory inside this repository. Fourteen source/project hashes,
build properties, output hashes and the command are recorded privately under
`artifacts/milestone6-harness/evidence.json`. Other external checkout changes
were outside these components and were left alone.

A private adapter supplies concrete state/command/event/context types. Ten
commands exercise boundary rejection, accepted immediate changes, adapter
rejection, frame resolution, priority rotation/passes, terminal resolution,
event ordering and input-state identity on rejection. The harness also observes
the kernel's actual canonical writer output. Two complete .NET 10.0.11 runs
match byte-for-byte; explicit scalar observations and canonical lengths pass.
This is an oracle baseline, not native equivalence.

The concrete root's contract inventory contains 650 nodes, 3,814 edges and 584
issues without reaching configured limits. Remaining analysis gaps include array
generic-interface helpers, variant interface conversion and unresolved dispatch.
General-filter status remains unresolved. An attempted compiler-library
translation correctly stops at a constructed array element operand. No game rules
or kernel source were altered to get around that diagnostic.

The concrete path confirms Int64 frame sequences and Guid-backed identifiers,
in addition to Boolean/enums, generic collections, inline values, string/number
formatting and canonical output. Slice 6c.1 now preserves `ldc.i8`'s complete
64-bit operand in detached compiler input; previously the decoder retained it
but the compiler-facing snapshot had only an Int32 operand slot. It does not yet
admit Int64 execution. Seven edge-value regressions plus related input/Int32
checks pass (61 tests). Review checked sign/width preservation, snapshot lifetime,
unchanged bounds and continued rejection by the Int32 execution profile.

Next scalar slices must establish Int64 CIL/native heap storage and checked
conversion semantics, then narrow scalar/enum storage. The kernel still requires
generic instance/interface/array dispatch, values in heap objects and the trial
ported library before its native transcript can run.

## Slice 6c.2 outcome

The explicit `integer-arithmetic-ir-v1` profile adds Int64 scalar operations,
width conversions and typed calls/locals/block edges using the existing IR,
validator and emitter. Native unsigned bit storage and guarded signed operations
avoid C++ overflow undefined behavior. The existing Int32 profiles remain bounded.
Thirty-four native probes over 288 operand pairs match .NET at both O0/O2 UBSan
(19,584 results); the focused run passes 46 tests. Review added wide transfer and
transitive-fault probes. This is authored IR support, not Int64 CIL or heap/array
support. See [ADR 0093](../decisions/0093-int64-scalar-ir.md).

## Slice 6c.3 outcome

The explicit integer CIL API now compiles static Int32/UInt32/Int64/UInt64
closures through shared bounded control-flow lowering. Widths are checked at
operations, locals, calls, returns and block joins; conversions remain explicit.
Thirty-one authored methods match .NET over 288 operand pairs at O0/O2 UBSan
(17,856 results). The focused regression run passes 243 tests; review added a
separate invalid-call-width regression. See
[ADR 0094](../decisions/0094-integer-cil-closures.md). The real kernel still needs
checked/narrow conversions, wide and inline heap storage, generic dispatch and
the ported library. This scalar gate does not establish native kernel equivalence.

## Slice 6c.4 outcome

All integer checked/unchecked conversion opcodes now have explicit IR and native
semantics, including narrow signed/unsigned stack results. Sixty-four C# casts
and 48 exact authored CIL probes match .NET at 42 numeric boundary values under
O0/O2 UBSan (9,408 results). Seventy-five focused checks pass. Review checked
range guards, unsigned magnitudes, sign extension, fault edges and profile
isolation; no actionable findings remain. See
[ADR 0095](../decisions/0095-integer-conversion-semantics.md). Narrow parameter/
local/field storage and wide heap/array/borrow integration remain subsequent work.

## Slice 6c.5 outcome

SByte/Byte/Int16/UInt16/Char/Boolean scalar parameters, locals and returns now
normalize at storage boundaries while retaining Int32 stack values. Seven C#
closures and six authored local-storage probes match .NET in 4,320 O0/O2 UBSan
results; 144 additional O2 call-boundary results also match. The focused checks
and review regression pass (121 distinct tests). Review found no remaining
actionable issue. See [ADR 0096](../decisions/0096-narrow-scalar-storage.md).
Enum identity and heap/array/borrow integration remain separate gates.

## Slice 6c.6 outcome

Explicit wide catalogs now preserve Int64 through native class fields, internal
call packets, frames and synchronous borrows. Narrow/wide borrow kinds remain
distinct and share the existing aggregate slot bound. Native v18 retains the
packed Int32 test boundary. Authored IR passes 288 repeated O0/O2 ASan/UBSan .NET
comparisons/fault observations with collection before allocations, plus invalid
borrow, allocation and cleanup checks. The 144-test focused run and 19-test
composed native follow-up pass. Review also covered value-lowering operand
remapping. See [ADR 0097](../decisions/0097-wide-object-storage.md). Wide CIL
discovery, inline/boxed payloads and arrays remain subsequent gates.

## Slice 6c.7 outcome

Explicit wide value catalogs preserve Int64 leaves through nested copies,
caller-owned return packets and boxed payloads. The native nested-value probe
matches .NET across 180 repeated O0/O2 ASan/UBSan results with forced collection.
It also checks packet rejection before writes, allocation failure and cleanup.
The new probes found and fixed a missing composition admission case; all three
then passed, alongside the other 119 focused checks. Review found no remaining
actionable issue. See [ADR 0098](../decisions/0098-wide-inline-and-boxed-values.md).
Wide CIL discovery and arrays remain the next integration gates.

## Slice 6c.8 outcome

The explicit `integer-managed-objects-cil-v1` compiler-library API connects wide
CIL arithmetic/conversions to class fields, nested structs, closed generic fields,
boxing, synchronous borrows and managed exceptions. Four authored C# closures
match .NET across 1,176 repeated O0/O2 ASan/UBSan results with collection before
allocation, exact frame budgets and cleanup checks. Generic metadata keys now
retain each integer primitive identity; special constraints classify them as
values. All 152 distinct focused checks pass; review found no remaining actionable
issue. Existing execution profiles remain bounded. See
[ADR 0099](../decisions/0099-wide-managed-object-cil.md). Arrays, narrow heap storage,
enums and broader value/instance/interface calls remain subsequent gates.

## Slice 6c.9 outcome

Closed reference-vector CIL now composes with values, generic class fields and
managed faults in the explicit integer-object profile. Generic element operands,
reference loads/stores, covariance and array casts share existing native array
checks and tracing. Four authored C# probes match .NET in 1,176 repeated O0/O2
ASan/UBSan results with forced collection and exact/insufficient frame budgets.
Tests found and fixed missing cast admission and unused fallback vector entries.
All 238 focused checks pass; review found no remaining actionable issue. A fresh
private kernel translation passes the original TypeSpec array blocker and now
stops at generic variance constraint admission (CIL3008); native kernel execution
remains pending. See [ADR 0100](../decisions/0100-composed-reference-array-cil.md). Value/wide/narrow
array elements, element borrows and generic array-interface helpers remain pending.

## Slice 6c.10 outcome

Closed interface snapshots now accept one declared variance direction while
preserving independent special constraints and rejecting invalid flag/owner
combinations. Tests cover authored metadata, actual `IReadOnlyList<long>` closed
inheritance and continued rejection of a covariant C# type test until executable
variance is implemented. See [ADR 0101](../decisions/0101-closed-interface-variance-declarations.md).
All 84 focused checks pass; review found no remaining actionable issue. The
private kernel now reaches a value constructor (CIL2001 at IL_006b) before
translation stops. This is metadata admission, not native interface assignment
or dispatch support.

## Slice 6c.11 outcome

Exact struct constructors now use writable checked receivers in the explicit
integer-object CIL profile. `newobj` initializes bounded temporary inline storage;
direct constructor calls preserve supplied storage. Readonly field initialization
is restricted to the declaring constructor's direct receiver before lowering to
private static calls. Five C# probes match .NET in 1,470 repeated O0/O2 ASan/UBSan
results, including generic/nested values, allocation, exceptions, empty structs
and borrowed arguments. See [ADR 0102](../decisions/0102-inline-value-constructors.md).
All 141 distinct focused checks pass; review found no remaining actionable issue.
The private kernel now passes constructor-kind admission and stops on a
constructor storage signature (CIL2002). General value instance calls, narrow/enum
storage and library integration remain subsequent gates.

## Slice 6c.12 outcome

Narrow scalar parameters, locals and returns now normalize in the composed CIL
profile, including generic arguments and constructor signatures. Eighteen authored
IL boundary probes and two C# composition probes match .NET in 2,640 repeated
O0/O2 ASan/UBSan results. Tests exposed C#'s direct constructor receiver crossing
conditional branches; a bounded receiver reload now preserves its identity and
readonly initialization permission. Other borrow joins and narrow storage addresses
remain unsupported. See [ADR 0103](../decisions/0103-composed-narrow-scalar-boundaries.md).
All 105 focused checks pass; review found no remaining actionable issue. The
private kernel passes the narrow constructor signature and now stops at an
ordinary `callvirt` (CIL2001 at IL_00f9). Narrow fields/borrows/arrays, enums and
broader instance/library integration remain subsequent gates.

## Slice 6c.13 outcome

Nonvirtual class instance calls now compose with value arguments/returns and
synchronous borrows in the explicit integer-object profile. Closed generic owners
and ordinary property bodies retain exact identities. Struct-return packet
lowering preserves `callvirt` null checks after argument evaluation. Two C# probes
match .NET in 588 repeated O0/O2 ASan/UBSan results with forced collection and
bounded frame checks. See [ADR 0104](../decisions/0104-composed-class-instance-calls.md).
All 146 focused checks pass; review found no remaining actionable issue. The
private kernel now reaches a bodyless interface member (CIL2001 at IL_0162),
confirming that generic interface dispatch is the next concrete call gate.
Virtual/interface dispatch and broader struct instance/storage behavior remain
subsequent gates.

## Slice 6c.14 outcome

Closed generic interface catalogs now retain exact argument identities and apply
reference covariance/contravariance through a bounded closure shared with vector
covariance. Metadata variance positions and detached catalog facts are validated;
invariant/value arguments remain exact. Four C# probes match .NET in 1,176
repeated O0/O2 ASan/UBSan results. All 173 focused checks pass. Review found no
remaining actionable issue; local main remains an ancestor. See
[ADR 0105](../decisions/0105-closed-interface-assignments.md). Interface method
dispatch and array interface helpers remain subsequent gates.

## Slice 6c.15 outcome

An opt-in metadata resolver now selects exact closed interface targets through
generic class hierarchies, reimplementation, explicit MethodImpl mappings and
virtual slots. Forty-eight mappings and authored calls agree with .NET, including
exact/insufficient budget checks. All 147 focused checks pass. Review found no
remaining actionable issue; local main remains an ancestor. See
[ADR 0106](../decisions/0106-exact-closed-interface-targets.md). This establishes
metadata choices; composing executable dispatch, variance-only targets and array
helpers remains pending.

## Slice 6c.16 outcome

Explicit value/delegate/exception IR now retains scalar/reference interface plans
through shared validation, rooting and native lowering. Four authored compositions
match .NET in 1,176 repeated O0/O2 ASan/UBSan results, including caught argument
versus null-receiver faults, allocating reference returns and wide value/box/borrow
flow. All 75 distinct focused checks pass. Review tightened catalog-capability and
closed-identity agreement; no remaining actionable issue was found. See
[ADR 0107](../decisions/0107-composed-interface-ir.md). Automatic closed interface
CIL discovery, struct signatures, variance-only targets and array helpers remain
pending. Local main remains an ancestor.

## Slice 6c.16a review fix

Dispatch integration review found that arrays could reach nominal interface type
tests without their runtime-supplied contracts in the catalog. A reduced
`int[] is IList` regression first reproduced erroneous admission. Metadata-backed
catalogs now diagnose selected Array interfaces and the five generic vector
contracts until helper relationships and methods are implemented. Runtime image
identity distinguishes them from application lookalikes; ancestry inspection is
bounded and charged to the shared ledger. All 124 focused checks pass. The fix
also covers the earlier nongeneric interface path; no native array-interface
compatibility is claimed.

## Slice 6c.17 outcome

`ObjectInterfacePlanBuilder.BuildClosed` now prepares scalar/reference dispatch
plans with exact closed owners, method signatures, mappings and slots. It reuses
the existing preparation session and shared bounded ledger. Forty-two mappings
match .NET interface maps; tests also cover same-width Int32/UInt32 identities,
JSON round trips, exact/short budgets and forged/open inputs. All 77 distinct
focused checks pass, including legacy native interface regressions. Review found
no remaining actionable issue; local main remains an ancestor. See
[ADR 0108](../decisions/0108-closed-interface-plans.md). CIL body discovery and
native closed-call conformance remain pending.

## Slice 6c.18 outcome

The explicit integer-object CIL profile now discovers exact scalar/reference
interface calls and their executable targets through repeated method/catalog
preparation. Closed identities, slot facts, null checks and managed faults survive
native lowering. Four C# probes match .NET in 1,176 repeated O0/O2 ASan/UBSan
results, including chained discovery and same-width signed/unsigned contracts.
All 136 focused checks pass; review found no remaining actionable issue. See
[ADR 0109](../decisions/0109-closed-interface-cil.md). The private kernel progresses
from its bodyless interface member to a class virtual `WriteCanonical` call at
IL_02ba. Full native kernel execution remains pending, along with broader class
dispatch, struct signatures, variance-only targets, array helpers and the ported
library. Local main remains an ancestor.

## Slice 6c.19 outcome

Exact closed class virtual target resolution now reuses the checked slot walk
and shared closed dispatch validation. All 53 focused checks pass, including
.NET callvirt/base-slot comparisons, exact/short budgets and input provenance.
Review found no remaining actionable issue; local main remains an ancestor.
See [ADR 0110](../decisions/0110-closed-class-virtual-targets.md). Composed class
CIL discovery and native conformance remain the next gate.

## Slice 6c.20 outcome

Scalar/reference class virtual calls now compose through discovery, exact slot
maps, value lowering and managed exceptions. Five C# probes match .NET in 1,470
repeated O0/O2 sanitizer results. All 140 distinct focused checks pass, including
interface, class-slot, value-instance and exception regressions. Review tightened
closed slot identity and the detached scalar-signature boundary; no remaining
actionable issue was found. See [ADR 0111](../decisions/0111-composed-class-dispatch.md).
The private kernel now reaches String character indexing at IL_02f1; native
kernel equivalence remains pending. Local main remains an ancestor.

## Slice 6c.21 outcome

Selected String indexing, two-string concatenation and inequality now compose
with values and managed exceptions through the existing UTF-16 runtime. All 101
focused checks pass; four probes match .NET in 1,176 repeated O0/O2 sanitizer
results. An inequality fault-edge defect found by the tests was corrected;
review found no remaining actionable issue. See
[ADR 0112](../decisions/0112-composed-string-operations.md). The private kernel now
reaches unsupported exception construction in its handler registry. Local main
remains an ancestor; full native transcript comparison is still pending.

## Slice 6c.21a review fix

Follow-on allocation review found that the source value-root plan omitted
StringConcat safepoints even though native lowering registered its roots. A
regression first reproduced the missing source allocation site; both source and
scalar plans now record it. The regression and 54 focused value-root checks pass.
No native ABI or allocation behavior changed. Review found no further issue.

## Slice 6c.22 outcome

Selected argument-exception constructors, parameter-name access and invariant
message resources now compose through native v19's traced payload. Five C# probes
match .NET in 1,470 repeated O0/O2 sanitizer results. Dedicated runtime checks
cover sole-owner parameter tracing, allocation failure, invalid references and
all thirteen cached resources. All 106 distinct focused checks pass. The tests
found and corrected a null-message resource fallback; the O0 test harness was
split to retain its 2 KiB stack limit. Review found no remaining actionable issue.
See [ADR 0113](../decisions/0113-argument-exception-payloads.md). The private kernel
now reaches a value-returning interface signature at registry-constructor
IL_0046. Full native kernel execution remains pending. Local main remains an
ancestor.

## Slice 6c.23 outcome

Class virtual signatures now carry inline values and synchronous borrows through
the existing scalar ABI, preserving rooted return packets and exact maps.
By-value starg writes were added after the fixture exposed the missing opcode.
Five probes match .NET in 1,470 repeated native sanitizer results; all 66 focused
checks pass. Review retained the explicit integer-object catalog gate and found
no remaining actionable issue. See
[ADR 0114](../decisions/0114-class-virtual-value-signatures.md). Interface value
plans/discovery remain pending; local main remains an ancestor.

## Slice 6c.24 outcome

Exact interface signatures now carry inline values and synchronous borrows using
shared scalar signature expansion and rooted return destinations. Detached plans
must retain the executable value layouts. Four probes match .NET in 1,176 repeated
native sanitizer results; all 69 distinct focused checks pass. Review found no
remaining actionable issue. See
[ADR 0115](../decisions/0115-interface-value-signatures.md). The private kernel
advances to an internal helper call in CommandId construction; native kernel
equivalence remains pending. Local main remains an ancestor.

## Slice 6c.25 outcome

Same-image method calls now accept assembly-accessible members and enclosing
types in the integer-object profile. Metadata and detached IR share the checked
access contract. All 84 focused checks pass; two probes supply 588 matching native
sanitizer results. Review found no remaining actionable issue. See
[ADR 0116](../decisions/0116-same-assembly-method-access.md). The private kernel
now reaches closed generic default initialization in PassPriority. Local main
remains an ancestor; native kernel execution is still pending.

## Slice 6c.26 outcome

Closed generic initobj/ldobj/stobj/cpobj now use exact admitted integer, reference
or value storage. Three probes match .NET in 882 repeated native sanitizer
results; explicit primitive copies/defaults add 588 expected results. All 102
distinct focused checks pass. Testing exposed and corrected a generated O0
driver stack overrun while retaining the 2 KiB limit. Review found no remaining
actionable issue. See [ADR 0117](../decisions/0117-closed-generic-memory-operations.md).
The private kernel now reaches a class constructor with value parameters; native
kernel comparison remains pending. Local main remains an ancestor.

## Slice 6c.27 outcome

Class constructors now carry value copies and synchronous borrows, including
value-returning base-argument helpers. Receiver escape and exactly-once immediate
base checks remain enforced. Three probes match .NET in 882 repeated native
sanitizer results; all 107 distinct focused checks pass. Review found no remaining
actionable issue. See [ADR 0118](../decisions/0118-class-constructor-value-signatures.md).
The private kernel advances to reference boxing in a generic null test. Local
main remains an ancestor; native kernel equivalence is still pending.

## Slice 6c.28 outcome

Closed reference box/unbox.any now preserve identity or perform a checked cast,
including bounded interface cast-message identities. Five probes match .NET in
1,470 repeated native sanitizer results; all 105 distinct focused checks pass.
Review confirmed that interface message keys are excluded from concrete runtime
types. See [ADR 0119](../decisions/0119-closed-reference-boxing.md). The private
kernel advances to a struct instance call in PlayerId equality; local main remains
an ancestor and native kernel comparison is still pending.

## Slice 6c.29 outcome

Direct ordinary struct instance methods use exact checked receiver borrows and
the shared value lowering. Four probes match .NET in 1,176 repeated native
sanitizer results; all 44 focused checks pass. Review found no remaining actionable
issue. See [ADR 0120](../decisions/0120-direct-struct-instance-calls.md). The private
kernel next requires interface implementation metadata on its direct PlayerId
equality call. Local main remains an ancestor; native equivalence is still pending.

## Slice 6c.30 outcome

Interface-bearing values retain checked declaration metadata for direct body
calls. Boxing these values remains an explicit diagnostic in source and detached
IR. Two probes match .NET in 588 repeated native sanitizer results; all 131
distinct focused checks pass. Review tightened retained-metadata boundaries and
found no remaining actionable issue. See
[ADR 0121](../decisions/0121-direct-interface-value-declarations.md). The private
kernel discovery now first stops at invariant numeric formatting; subsequent
layout and library gates, including runtime generic flags, remain unresolved.
Local main remains an ancestor; native kernel equivalence is still pending.

## Remaining completion evidence

- Pin imported source commits, licenses, local patches, reference identities
  and oracle runtime; never trust a library solely by namespace or type name.
- Differentially test each admitted member and semantic extension, including
  mutation, exception order, collection across allocations and ownership.
- Supply concrete exported roots and a real adapter so open public-surface
  declarations are not mistaken for an executable closure.
- Match repeated command transcripts, rejected commands, deterministic ordering
  and canonical state output on .NET and local native O0/O2 sanitizer builds.
- Audit the required ported-library implementations and resolve every gap needed
  by those roots, including the general-filter decision.
- Keep unsupported members actionable, update compatibility claims and review
  composed behavior before marking milestone 6 complete. Public embedding,
  mobile and browser evidence belong to later milestones.
