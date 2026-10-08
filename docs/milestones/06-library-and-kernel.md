# Milestone 6: Required library subset and real turn kernel

Status: complete for the bounded macOS ARM64 compiler-library profile; slices 6a,
6b.1–6b.4, 6c.1–6c.61, 6d.1–6d.23, 6e and 6f complete. Recorded 2026-10-06.
See [ADR 0177](../decisions/0177-stage-six-completion.md) for the reviewed exit evidence.

The exit gate is equivalent deterministic command traces from the real kernel
on .NET and Cilica's native runtime. Milestone 5 supplies bounded semantic
building blocks, not the whole library or all semantics used by the kernel.
Keep application rules in the external application checkout; public regression
fixtures and examples must be independent.

## Delivery slices

| Slice | Deliverable and gate | Status |
| --- | --- | --- |
| 6a | Explicit library-contract inventory, independent tests and private kernel dependency evidence | Complete; ADR 0088 |
| 6b.1 | Pin the reference/source baseline and validate explicit type correspondence | Complete for metadata plans; ADR 0089; initial source import in 6d.1 |
| 6b.2 | Apply the plan to structural member binding and preserve source/implementation evidence | Complete for metadata plans; ADR 0090; ordinary compiler binding is unchanged |
| 6b.2a | Explicit mapped compilation view, isolated from strict inspection, with a native probe | Complete; ADR 0091; mapped audit API added in 6d.3 |
| 6b.3 | Validate build provenance and exact intrinsic grants at the compiler boundary | Complete for independently approved build attestations; ADR 0092; real build producer/outputs accompany 6d |
| 6b.4 | Preserve generic type/method constraints across explicit library mappings | Complete for structural correspondence; ADR 0129; executable constraint proof remains separate |
| 6c | Compiler semantics required by the concrete kernel path | Complete for this closure; wide/narrow scalars, Int32 enums, closed values/classes/interfaces, invariant value vectors, switch and constructor body branches are tested; broader CLI semantics remain diagnostic |
| 6d | Required library members and runtime prerequisites | Complete for this closure; pinned owned core includes bounded formatting/Guid, collections and read-only views, array helpers, StringBuilder, exceptions and initialized delegate caches; broader library APIs remain outside this milestone |
| 6e | Compile the unmodified kernel with a concrete adapter and run bounded deterministic command transcripts against .NET | Complete; 10,752 exact O0/O2 normal/forced-GC comparisons, with sanitizers |
| 6f | Implementation/filter closure, composed stress and final review | Complete; exact compiled/source-body audit, composed filter/finally/collection test, full local regression review and targeted repairs |

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

## Slice 6d.1 outcome

The first managed-library source import retains pinned upstream formatting source,
license/notices and hashes. A marked adaptation implements four invariant integer
decimal text helpers using checked managed strings. All three tests pass with
8,192 managed text comparisons and 8,976 repeated native O0/O2 sanitizer results.
Review found no remaining actionable issue. See
[ADR 0122](../decisions/0122-managed-decimal-formatting-trial.md). System member
mapping, actual build provenance production and implementation-closure auditing
remain pending; the native kernel still does not run.

## Slice 6d.2 outcome

A separate local build tool now captures actual Roslyn source/reference/compiler
inputs, host/runtime evidence, effective arguments and output identity/hash. It
publishes complete new build directories and no intrinsic grants or approval.
All 39 distinct producer/admission checks pass, including deterministic bytes,
failure cleanup, bounded compiler output and existing-output preservation. The
formatter's actual build records 201 inputs. Review found no remaining actionable
issue; see [ADR 0123](../decisions/0123-library-build-evidence-producer.md).
Mapped implementation auditing, System member mapping and native kernel execution
remain pending.

## Slice 6d.3 outcome

Explicit mapped implementation audits now retain the pinned type plan, reached
method/field projections with constructed signatures and any approved build
digest. Implementation bodies, fields and initializers are expanded; missing
members/bodies and budget exhaustion remain unresolved. All 147 distinct focused
checks pass. Review found no remaining actionable issue; see
[ADR 0124](../decisions/0124-mapped-library-implementation-audits.md).
The actual required-library closure, general-filter decision and native kernel
comparison remain pending.

## Slice 6c.31 outcome

AllowByRefLike declarations now accept ordinary supported arguments while actual
stack-only values/arguments remain diagnostic. Marker checks include nested and
unused arguments and cannot be erased by a library type mapping. All 167 focused
checks pass; the direct interface suite supplies 882 repeated native sanitizer
results, including 294 for the real runtime IEquatable declaration. Review found
no remaining actionable issue; see
[ADR 0125](../decisions/0125-ordinary-arguments-with-byreflike-permission.md).
Constrained dispatch, framework member mappings and native kernel equivalence
remain pending.

## Slice 6c.32 outcome

The metadata API now selects exact direct interface bodies and Object/ValueType
overrides on closed ordinary values, retaining declaration and receiver evidence.
All 79 distinct focused checks pass; target mappings match .NET and unsupported
fallbacks, forged declarations and newslot impersonation are rejected. Review
found no remaining actionable issue; see
[ADR 0126](../decisions/0126-constrained-value-targets.md). Executable constrained
CIL integration follows; native kernel equivalence remains pending.

## Slice 6c.33 outcome

Exact constrained value calls now execute through checked receiver borrows in
the integer-object CIL profile. Three probes match .NET in 882 repeated native
sanitizer results; all 124 distinct focused checks pass. Prefix-control-flow,
receiver and access regressions remain diagnostic, and no boxes are introduced.
Review found no remaining actionable issue; see
[ADR 0127](../decisions/0127-constrained-direct-value-cil.md). Broader constraints,
private/readonly receivers, required library members and native kernel equivalence
remain pending.

## Slice 6c.34 outcome

Closed type/method snapshots now prove exact implemented-interface constraints,
including inherited generic interfaces, self constraints and both substitution
environments. Integer primitives also admit the selected unmanaged marker.
All 141 focused checks pass. Two additional C# helpers match .NET in 588 repeated native sanitizer results.
Review added malformed interface-encoding regressions and corrected a stale
AllowByRefLike expectation; see
[ADR 0128](../decisions/0128-exact-interface-constraints.md). Library mapping
correspondence, broader constraints and native kernel equivalence remain pending.

## Slice 6b.4 outcome

Library mappings now compare open generic constraint sets after explicit type
projection. Changed obligations, duplicates, malformed signatures and unmapped
identities fail before substitution. All 146 focused checks pass; review found
no remaining actionable issue. See
[ADR 0129](../decisions/0129-library-constraint-correspondence.md). This closes a
mapping prerequisite; it does not provide the required framework bodies or the
native kernel result.

## Slice 6d.4 outcome

The build producer now supports explicit zero-reference core builds and exact
intrinsic declaration requests, retaining both in hashed options. Missing,
ambiguous, duplicate and malformed requests fail without publishing output.
The combined library/core validation run passes 136 checks; selector budget
accounting was tightened during review. See
[ADR 0130](../decisions/0130-core-build-and-intrinsic-requests.md). Independent
approval remains required; core source and native conformance follow in 6d.5.

## Slice 6d.5 outcome

The owned Cilica.Core source now builds with zero platform references and maps
exact string/exception declarations to the pinned reference image. Three probes
match .NET in 882 repeated native sanitizer results; the combined 136-check run
passes. Review corrected emitted indexer naming and a sealed exception header.
The actual image records 200 inputs and 57 requested grants; production approval
remains separate. See [ADR 0131](../decisions/0131-owned-core-library-bootstrap.md).
The latest strict kernel recheck still stops at CanonicalWriter.Append on the
invariant-formatting path. System formatting APIs, providers, collections and the
native transcript comparison remain pending.

## Slice 6d.6 outcome

The selected CultureInfo.InvariantCulture getter now returns one instance-owned,
permanently traced provider. Exact type/member grants are required in mapped
compilation. Four mapped core probes provide 1,176 repeated native O0/O2 sanitizer
results, including 294 new provider-identity results; cache lifetime, ownership,
heap limits, detached IR and missing grants have focused regressions. The private
native profile advances to v20. Review split cached-root tracing to preserve the
existing 2 KiB native stack-frame limit. The platform CoreLib's shared
String/interface gate remains explicit. See
[ADR 0132](../decisions/0132-invariant-provider-identity.md).
Numeric formatting methods, general culture behavior and native kernel
equivalence remain pending.

## Slice 6d.7 outcome

The four selected 32/64-bit integer types now execute ordinary managed instance
bodies using checked scalar receivers. The owned core supplies exact GetHashCode
mappings; the additional probe matches .NET in 294 repeated native O0/O2 sanitizer
results (1,470 across the five core probes). The composed regression run passes
40 checks, followed by 11 focused checks (41 distinct); review added a duplicate-primitive-identity regression and tightened
primitive layout/stack-only checks. Unsigned ldind.u4 uses checked 32-bit storage.
See [ADR 0133](../decisions/0133-primitive-instance-bodies-and-hashing.md).
Numeric formatting APIs, primitive constrained dispatch and kernel equivalence
remain pending.

## Slice 6d.8 outcome

The four integer ToString(IFormatProvider) members now execute managed decimal
code with a directly proven invariant provider. A bounded traced [0, 300) cache
preserves shared numeric string identity and keeps it distinct from literals.
All 81 distinct focused checks pass. Eight core probes give 2,352 repeated native
results, plus 8,976 complete code-unit/length comparisons and 60 cache-boundary
comparisons. Full local solution build passes. Review corrected implicit interface
flags and the warmed-frame exhaustion test; see
[ADR 0134](../decisions/0134-invariant-integer-formatting-apis.md).
The actual reviewed fixture image records 202 inputs and 65 requested grants.
The first real mapped kernel recheck stops at missing IReadOnlyList`1
(token 0x020009b0); native kernel execution is still pending. General formatting,
provider aliases, StringBuilder and collections remain unsupported.

## Slice 6d.9 outcome

The owned core now declares the pinned read-only collection and enumeration
interfaces with exact inheritance, variance and member mappings. Six application
collection probes supply 1,764 repeated native O0/O2 sanitizer results, including
Int64 elements, reference identity, enumeration and exception order. All 33
focused checks pass; review found no remaining actionable issue. No compiler or
runtime profile change or new intrinsic grant was needed. See
[ADR 0135](../decisions/0135-owned-read-only-collection-contracts.md).
The reviewed fixture has 203 inputs and 65 grants. The actual kernel recheck
moves to missing System.Action`2 (0x02000007); collection implementations, array
helpers, variance-only dispatch and native kernel equivalence remain pending.

## Slice 6d.10 outcome

Closed generic delegates now preserve exact specialization identities and use
checked Int32 signatures, including the owned Action<int,int> mapping. Six new
probes supply 1,764 repeated native O0/O2 sanitizer results for captured cycles,
static actions, returns, null/fault order, exact casts and invalid-cast messages.
Delegate, generic-constraint, interface, binding and core regression suites pass;
review found no remaining actionable issue. The explicit CIL profile advances to
v2; detached/native storage is unchanged. See
[ADR 0136](../decisions/0136-closed-generic-delegates.md).
The reviewed fixture has 204 inputs and 65 grants. The actual kernel recheck now
stops at missing System.Guid (0x02000074). Reference-valued callbacks, collection
storage, array helpers and native kernel equivalence remain pending.

## Slice 6c.35 outcome

Readonly static reads without type/module initialization or explicit storage now
lower to scalar zero, null or independent inline defaults. Instance layouts omit
these default static declarations. Eight focused checks and 140 related regressions
pass, including 588 new repeated native O0/O2 sanitizer comparisons. Review found
no remaining actionable issue; see
[ADR 0137](../decisions/0137-default-readonly-static-fields.md). Mutable/initialized
statics, addresses and writes remain unsupported. The kernel's Guid dependency
still requires its managed library implementation.

## Slice 6d.11 outcome

The owned Guid implementation supplies numeric constructors, Empty, typed equality,
hashing, unsigned field-order comparison and D/N/B/P/X formatting. Pinned source
and resources are retained with hashes and adaptations. Selected SystemException,
ArithmeticException and FormatException resources preserve default, null, empty
and explicit message behavior and identity. Native profile v22 and exception IR
v2 describe that extension. See [ADR 0138](../decisions/0138-owned-guid-and-exception-resources.md).

The full local build passes, with 58 composed checks, 12 argument/resource checks
(overlapping Guid checks) and one subsequent grant-removal check. Seven new probes
provide 14,400 repeated O0/O2 sanitizer comparisons. Review checked managed word
packing, unsigned order, allocation rooting, exception ancestry and exact grants;
no remaining actionable issue was found. The reviewed fixture records 205 inputs
and 68 grants. The actual kernel advances to missing Dictionary`2 (netstandard
0x020009a2). Boxed Guid tests, collection storage, StringBuilder and complete native
kernel equivalence remain pending.

## Slice 6c.36 outcome

Heap classes can now embed closed inline structs, including nested Int64/reference
payloads and Guid fields. Scalar expansion preserves FieldDef identity plus leaf
components, checked owner borrows, empty-field receiver checks and ordinary GC
tracing. Five new probes supply 1,470 repeated native O0/O2 sanitizer comparisons.
The full build, 213 related checks and 62 final composed checks pass (overlapping).
See [ADR 0139](../decisions/0139-inline-values-in-heap-fields.md).

Review retained a reduced rejection regression for direct struct-return calls
stored into heap fields, whose destination/exception ordering needs explicit
lowering. The admitted copy/borrow paths have no remaining review findings.
The kernel still needs Dictionary and the remaining library/semantic closure;
this slice does not establish native kernel equivalence.

## Slice 6c.37 outcome

Throw now discards unrelated evaluation-stack values after consuming its exception.
This fixes a rejection of optimized Roslyn output found in dictionary callbacks.
A reduced scalar/reference-stack regression passes 294 repeated native checks;
the composed dictionary/throw/exception run passes 26 checks. Review found no
remaining actionable issue. See [ADR 0140](../decisions/0140-throw-discards-evaluation-stack.md).

## Slice 6d.12 outcome

The pinned dictionary lookup/insertion/resize and prime algorithms now execute
as Cilica.Library.TrialDictionary. Private exact grants select closed integer
and IEquatable struct equality/hash bodies, retaining mutable-key receiver copies
and callback failure order. Five probes provide 1,470 repeated native O0/O2
sanitizer comparisons. The full build and 64 distinct focused checks pass;
review found no remaining issue in the admitted trial. See
[ADR 0141](../decisions/0141-dictionary-storage-and-closed-key-trial.md).

The actual core image records 207 inputs and 71 requested grants. Public
System.Dictionary mapping remains pending its collection/array interface
contracts, so the real kernel's Dictionary gate has not yet cleared. Reference
keys, enumeration/removal, broader collections and native kernel equivalence
remain pending.

## Slice 6d.13 outcome

Selected runtime array contracts now participate in casts and interface dispatch.
Exactly granted managed helpers implement Count and indexed reads for Int32 and
reference vectors, including covariance and Int32/UInt32 bit-preserving reads.
Six probes provide 1,764 repeated native O0/O2 sanitizer comparisons; the full
build and 112 composed checks pass. Review corrected the bounds message and
signed/unsigned equivalence, with no remaining issue in the admitted scope.
See [ADR 0142](../decisions/0142-array-interface-relationships-and-readers.md).

The reviewed core has 208 inputs and 73 requested grants. Enumeration, mutation,
enum/wide/value vectors and Array.Empty remain pending, as do public collection
mappings and the actual kernel/native transcript gate.

## Slice 6d.14 outcome

The tested dictionary now maps the bounded public framework member subset with
its exact generic/nongeneric/serialization interface declarations. Actual reference
MethodRefs pass unchanged through the binding plan. Six mapped probes supply 1,764
native comparisons. Nongeneric array collection flags/SyncRoot complete shared
interface dispatch; seven array probes supply 2,058 comparisons. The full build
and 31 composed checks pass. Review found no remaining issue in the admitted subset.
See [ADR 0143](../decisions/0143-public-dictionary-contracts.md).

The reviewed core records 209 inputs and 76 grants. The actual kernel now advances
from Dictionary to the canonical writer's missing System.Text.StringBuilder mapping.
Reference keys, broader collection members and the full native transcript remain
pending. Unimplemented member declarations produce build errors, never runtime
stand-ins.

## Slice 6c.38 outcome

Zero-argument callvirt can resolve a public base declaration against an immediately
proven sealed receiver while retaining slot validation and the null check. This
unblocks ordinary overrides such as StringBuilder.ToString without executing an
unimplemented Object body. Two reduced probes supply 588 native comparisons;
branch/access/unproven receiver rejections and the 61-check composed integration
run pass. Review added access checks and linear predecessor indexing. See
[ADR 0144](../decisions/0144-proven-sealed-virtual-receivers.md).

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


## Slice 6d.15 outcome

The pinned chunked StringBuilder adaptation supports default construction, char
and string append, length/capacity getters and immutable ToString snapshots. An
exact private grant copies Int32-buffer code units into native UTF-16 strings;
empty strings are canonical, nonempty snapshots are fresh and independent. See
[ADR 0145](../decisions/0145-bounded-string-builder.md).

Eight focused checks include 4,362 repeated native/.NET comparisons at O0/O2,
allocation-time collection, exact surrogate/code-unit observations, large chunk
growth, private capability boundaries and malformed IR rejection. Review found
and fixed the stale fixed exception-resource cache capacity; its native sanitizer
regression now retains all 15 resources and detects cache cycles.

The trial core has 210 inputs and 77 grants. Image SHA-256:
`92197b414762e7b3e4c2022464f906668514a0baec496a5a482183fbf1b18bfd`;
manifest SHA-256:
`cc93b9f27b84e5b287b58bbe2c966cc2ddf1e51dc7ae98c53aca145cf7cdd52e`.
These are local evidence, not production trust approval. The unchanged concrete
kernel now reaches missing mapped Array.Empty<T> in KernelState.Create. Native
transcript comparison and the final implementation/filter audit remain pending.

The full solution build and 44 composed local checks pass, including native string,
collection and exception-resource tests. No hosted work ran.


## Slice 6d.16 outcome

Array.Empty<T> now preserves instance-local exact closed identity for Int32 and
reference vectors, with permanent roots and exact intrinsic admission. Eight
checks include 882 repeated native/.NET results, native cache/quota/corruption
sanitation and detached IR validation. See [ADR 0146](../decisions/0146-exact-empty-array-cache.md).
The full solution builds without warnings. Review kept the 2 KiB native stack
limit by separating cache tracing, and extended the leak-check harness to count
these permanent roots explicitly.

The trial core has 210 inputs and 78 grants. Image SHA-256:
`aa25bbb7df1a181c13499d6ece587e5cc30f2b6f3b1b3a42be019415742d8668`;
manifest SHA-256:
`65da1c010d446ba63eb7ee74c3559777b5ac6602d3cfd3a0bf8834b5233f578c`.
The concrete unmodified kernel advances to missing List<T> at Step IL_0069.
No native transcript comparison is claimed.

All 25 composed local array, dictionary, tracing, exception and collection checks
pass. No hosted CI or external application changes were made.


## Slice 6d.17 outcome

Pinned managed List<T> now supplies construction, capacity, indexed reads/writes,
Add/growth and live AsReadOnly views for Int32/reference elements. Array interface
writes and fixed-size Add have exact managed helper grants, including covariant
store checks. See [ADR 0147](../decisions/0147-managed-lists-and-readonly-views.md).
Six probes supply 1,764 repeated native/.NET comparisons. Exact nominal interfaces,
member mappings and grant denial are verified. The full build and 61 composed
collection/interface tests pass. Review found no remaining issue in this scope.

The trial core has 212 inputs and 80 grants. Image SHA-256:
`3327031b3245a1c4af859b8ab4ff8692faf422baaafbce5b1c6ddb2e9216d970`;
manifest SHA-256:
`ef0f5f047f7e86a9f87a10566f7cd8cbb6835e1e28a52424032d2423d1707f9b`.
The unmodified kernel now stops at ResolutionState.WriteCanonical IL_0002 on
compiler-generated initialized static delegate-cache storage. This requires
compiler/runtime semantics; the application remains unchanged. Native transcript
equivalence and the implementation/filter audit remain outstanding.


## Slice 6c.39 outcome

Exact single-target delegates now carry Int32/reference parameters in the integer-
object profile. Five new probes provide 1,470 repeated native/.NET comparisons,
covering captured receivers and object/interface/string/array arguments across
allocation and exceptions. Catalogs retain explicit parameter types and rooted
argument packets preserve reference ownership. See [ADR 0148](../decisions/0148-reference-delegate-parameters.md).

The full solution and 43 focused delegate checks pass: 42 passed in the composed
run and the corrected isolated serialization check passed separately. Review
covered signature bounds, detached catalog agreement, exact targets and GC roots.
Object layout is v9 and native emission v29. Wide/value/byref parameters, reference
returns and variance-based binding remain diagnostic. The kernel's initialized
static callback receiver/cache remains the next blocker.


## Slice 6c.40 outcome

Ordinary Int32/reference static fields without initialization now use explicit
typed IR and per-instance, quota-charged native storage. Static references are
collector roots, closed generic identities remain distinct, and state persists
across calls/contexts only within one runtime instance. See
[ADR 0149](../decisions/0149-instance-owned-static-storage.md).

Three differential probes provide 882 repeated O0/O2 comparisons. Native tests
cover persistence, instance isolation, root release, corruption, quota failure
and retry. The full build and 31 distinct focused/composed checks pass (29 in
the composed run, followed by six validator cases including two new regressions).
Review added combined field bounds and duplicate/instance-field collision checks.
Object layout v10, typed reference IR v8 and native v30 carry the change.

Initialized static fields, initializer execution/failure caching, static addresses
and narrow/wide/inline static values remain diagnostic. The actual kernel's
compiler-generated initialized callback receiver still blocks native compilation.


## Slice 6d.18 outcome

Exact granted TypeInitializationException construction and TypeName/InnerException
reads now retain a traced name, message and inner exception. The public constructor
formats eagerly, preserving the allocation point; null names return canonical
empty strings and nested exception identity is preserved. See
[ADR 0150](../decisions/0150-type-initialization-exception-payload.md).

Four probes supply 1,542 repeated native/.NET comparisons. Nine focused tests cover
those probes, grant denial, detached IR, roots, corrupt payloads, quota failure and
fault provenance. The full solution builds without warnings. All 43 exception
checks pass (42 in the composed run, with the corrected O0 test-harness stack
check passing in the subsequent focused run); two core-build tests also pass.
Review corrected eager message construction and kept native helpers within the
2 KiB stack limit. Initializer execution and cached failures remain outstanding;
this prerequisite alone does not advance the concrete kernel gate.


## Slice 6c.41 outcome

Authored integer-object IR now has a dedicated type-initialization operation with
per-instance state and precisely rooted cached failures. It handles reentry,
preparation failure/retry, successful completion and permanent managed/fatal
failures. See [ADR 0151](../decisions/0151-instance-type-initialization-state.md).
Eleven focused checks and 25 distinct composed checks pass at local O0/O2 with
sanitizers; the full solution builds without warnings. Review covered roots,
allocation ordering, readonly ownership, bounded state and fault propagation.

This is the runtime/IR half of initialization. CIL trigger discovery and initialized
metadata admission remain pending, so the concrete kernel is still blocked at its
initialized callback receiver. No native transcript equivalence is claimed.


## Slice 6c.42 outcome

BeforeFieldInit class initializers are now discovered and compiled as managed IL,
with a dedicated trigger before static field reads/writes. Four probes provide
1,176 repeated native/.NET comparisons, including reentry, readonly initialization,
closed generic caches and retained failures. The full build and 33 composed checks
pass. Review corrected generic exception display names and authenticated the exact
wrapper constructor grant. See [ADR 0152](../decisions/0152-field-triggered-type-initialization.md).

The trial core contains 212 inputs and 84 grants. Image SHA-256:
`3b8bd091e919c43e4157047bb92e363b5d86c6318714ece180da99dc3fe107d9`;
manifest SHA-256:
`c6ab993f914dd530bbee2e1a78eaa9956ff403919824862108e13a1e49410a27`.
The unchanged kernel now stops at private nested static owner access in
ResolutionState.WriteCanonical IL_0002. Strict type/module initialization and
initialized value types remain diagnostic; private nested access is next for the
kernel. No native kernel equivalence or completed M6 claim is made.


## Slice 6c.43 outcome

Authenticated lexical declaring-type chains now permit the containing type and
its nested siblings to use nongeneric private cache owners. Public fields and
admitted methods retain their own access checks. Three probes provide 882 repeated
native/.NET comparisons; access and malformed-chain regressions pass. The full
build and 63 distinct focused/composed checks pass. See
[ADR 0153](../decisions/0153-lexical-nested-cache-access.md).

Using the recorded 6c.42 core image/digest, the unchanged kernel now passes its
initialized private callback cache and reaches missing System.ValueTuple`2 at
StepPriority IL_00b8. Native transcript equivalence and the implementation/filter
closure audit remain outstanding. Strict initialization and generic/protected
lexical access remain outside the added scope.


## Slice 6d.19 outcome

Bounded ValueTuple<T1,T2> construction, fields, typed equality and hashing now use
ordinary managed code. Exact granted key operations also admit ordinal string
keys. Four probes provide 1,176 repeated O0/O2 native/.NET comparisons, including
null/UTF-16 string keys, dictionary growth and mutable component callbacks. Review
caught and corrected field hashing: equality copies receivers, but hashing must
mutate the actual tuple fields when their callbacks do so. Nine composed checks
and the full solution build pass. See [ADR 0154](../decisions/0154-bounded-tuple-and-string-keys.md).

The trial core has 215 inputs and 84 grants. Image SHA-256:
`e88f47457d5a7b410feed2f16ef1cc1b0f525a7b69be4c2e66bfc8c03ec9880c`;
manifest SHA-256:
`5d6eb422bbcf2cb76cf2cb71978864c803708e4827ba7eb2f5f6d57ad6144b3b`.
The unchanged kernel passes tuple binding and now requires String.Format at
DriveAutomatic IL_0121. Object tuple equality, comparison, structural operations,
formatting, public String.GetHashCode and general reference keys remain diagnostic.
Native kernel transcripts and the closure/filter audit remain outstanding.


## Slice 6c.44 outcome

Boxed interface-bearing values retain their exact nominal interface ancestry and
supported variance relationships. CIL can implicitly place fresh boxes in typed
interface locals, fields, arrays, arguments and returns through a proven checked
conversion. Payload copies and traced references use the existing box runtime.
See [ADR 0155](../decisions/0155-boxed-value-interface-relationships.md).

Three probes provide 882 repeated O0/O2 native/.NET comparisons. Ninety distinct
focused/composed checks and the full solution build pass. Review covered exact
descriptor membership, closure bounds, catalog agreement and receiver copies.
Boxed method dispatch remains explicitly rejected; it is the next prerequisite
for the formatted kernel error path. Boxed value IR is v2, scalar boxing IR v3,
and native emission v33. No new native kernel transcript claim is made.


## Slice 6d.20 outcome

Exactly granted String.Format(String,Object) now specializes a same-block direct
box of a named, non-IFormattable value. A managed parser handles escapes, indices,
alignment and ignored format text, while an exact private rendering grant borrows
the retained payload for each ordinary ToString callback. General boxed dispatch
remains diagnostic. See [ADR 0156](../decisions/0156-direct-box-formatting.md).

Four probes provide 1,176 repeated O0/O2 native/.NET comparisons for mutable and
readonly generic values, UTF-16 text, null callback results, callback order and
exact malformed-format messages. Seventeen distinct composed checks pass (16 in
the composed run and the corrected proof test separately); all three formatting
checks pass again after sharing parser helpers. The full solution builds cleanly.
Review verified same-block proof, exact grants/private callers, payload roots and
source provenance. Aliases, IFormattable, primitive/reference arguments, providers
and other overloads remain unsupported.

The final trial core has 216 inputs and 86 grants. Image SHA-256:
`639c3981452100bc140989eba23bc86a8d9cfa68c861b82fe28739a3bf277f00`;
manifest SHA-256:
`a26dcb28e4b505d8727d999bfe7bf0284e0e005fbe1a0171ee10f334c0f678ae`.
The unchanged kernel next requires System.StringComparison in PendingDecision.IsLegal.
Native transcripts and implementation/filter closure remain pending.


## Lexical interface agreement correction

Composed string/interface validation exposed a 6c.43 regression: interface mapping
facts omitted lexical owners, and record equality compared independent immutable
chain arrays by storage identity. Mapping construction now carries those facts;
binding, static-field and catalog agreement compare their lexical contents.
Independent copied snapshots are accepted, while dropped/contradictory facts remain
rejected. Three reduced tests and 104 related checks pass (107 total), including
native composed strings, interface values, array interfaces and nested access.
The full solution builds without warnings. Review checked that structural comparison
preserves every other binding fact and grants no additional access.


## Slice 6d.21 outcome

The pinned StringComparison enum and exact granted three-argument String.Equals
contract are present. Body translation requires a directly proven Ordinal constant,
then uses the existing UTF-16 equality operation. Other modes and mode aliases remain
diagnostic. See [ADR 0157](../decisions/0157-proven-ordinal-string-comparison.md).

One probe provides 294 repeated O0/O2 native/.NET comparisons for nulls, case,
NULs, surrogates and distinct equal strings. Enum/grant/proof checks pass; the full
solution builds without warnings. Fifty distinct ordinal/core/tuple/string checks
pass after the separately recorded lexical interface correction. Review covered
exact declaration and enum storage checks, evaluation order and unchanged native
ownership. General enum storage and cultural/ignore-case comparison are not added.

The trial core contains 217 inputs and 87 grants. Image SHA-256:
`659dde9e1d51a8df3c7908950b5fc36f0b706b5f698ed77199ec215de084f17c`;
manifest SHA-256:
`546374503b43fbb1389febd921e52b8428443d2e4e93a1ca38d11d4e2e0783ba`.
The unchanged kernel now reaches the 65,536-member metadata work limit while binding
DecisionBatch.Complete. Investigate reusable exact bindings before changing bounds.
Native transcript equivalence and final closure/filter auditing remain pending.


## Slice 6c.45 outcome

Successful call/field bindings are reused by operand token within the exact closed
caller context. The protected-method control-flow reader also reuses declarations
within its single caller, while computing opcode-specific stack effects separately.
Access, intrinsic authorization and per-instruction budgets remain checked; the
65,536-member limit is unchanged. See [ADR 0158](../decisions/0158-closed-binding-reuse.md).

A reduced 1,026-call/two-specialization regression failed at the metadata limit
before reuse and now passes, preserving two target bodies and static owners.
Seventy focused/composed checks and the full solution build pass. Review checked
caller identity, declaration-only caching, lifecycle bounds and unchanged error/
access handling. With the recorded 6d.21 core image and digest, the unchanged
kernel now reaches missing String.IsNullOrWhiteSpace in ResolutionRejection's
constructor. Native transcripts and final closure/filter auditing remain pending.


## Slice 6d.22 outcome

Implemented managed String.IsNullOrWhiteSpace with the pinned UTF-16 whitespace
classification. Ordinary selected String static IL bodies now pass through normal
compiler discovery and validation; instance methods and initialization stay
restricted. No new native capability is required; object operations IR is v13.

Three focused and twenty composed checks pass. Exhaustive 32-bit masks compare all
65,536 UTF-16 units, plus 49 mixed/null/empty string cases at both O0/O2, with three
repetitions, forced collection, ASan/UBSan and frame/cleanup checks. Full solution
build is clean. Review checked BMP boundaries, non-whitespace surrogates/BOM,
ordinary-body validation and retained intrinsic authorization.

The core evidence is `artifacts/milestone6-library/core-6d22-trial`: image SHA-256
`9842d11866749d195425e9da3ade6b3c100ae9ab434e98bcff84286a8807edb4`, manifest
`0a535f0aae30a5218b4adda401c0c1a7aa38d1cebdb01f5269b17ab83a42ca11`, 218 inputs
and 87 grants. `gate-mapped-6d22-trial.json` again reaches CIL2003 metadata work
exhaustion at ResolutionHandlerRegistry.Get IL_0009, via DriveAutomatic IL_0121.
No bounds were raised; native kernel equivalence remains pending.


## Slice 6c.46 outcome

Budget-owned complete method/field name indexes avoid repeated scans of unrelated
members during strict and mapped binding (ADR 0160). All matching overloads still
undergo fresh signature, generic, attribute and conflict checks. Scan work and index
storage are charged to the existing caller ledger; no bound was raised.

129 focused/composed checks and the full solution build pass. Review confirmed
complete-scan publication, fresh-budget isolation and unchanged semantic checks.
With the 6d.22 core/digest, `gate-mapped-6c46-trial.json` reaches missing String.Empty
in ResolutionHandlerId.WriteCanonical IL_0019. The concrete transcript gate remains
pending.


## Slice 6d.23 outcome

Implemented exact granted String.Empty reads (ADR 0161). The field maps to the
existing canonical empty literal, preserving allocation/fault handling, identity
and precise roots. Field capability descriptions retain exact tokens, owners and
signature hashes. Writes and missing/ambiguous/wrong-kind grants fail explicitly.

79 distinct focused/composed checks and the full solution build pass. Review
confirmed exact approval, field shape, canonical identity and rooted allocation.

Core evidence `artifacts/milestone6-library/core-6d23-trial` records 218 inputs and
88 grants, image `1ce29bc14637577934ce7426575e57da0d70fbe920fadc607f63241640b81020`,
manifest `bd23d3e3bf23aea9b0d4bd052b7f7937b99b54dad1090570bb5ea9a684bb5591`.
`gate-mapped-6d23-trial.json` reaches an aggregate compiler limit in List.AsReadOnly,
via ResolutionState.Push. Native transcript equivalence remains pending.


## Slice 6c.47 outcome

The concrete kernel exhausted 256 method declarations while using only 12,073 IL
bytes and 4,975 instructions, with 16,216 metadata work units remaining. Expanded
the common bounded method cap to 512 (ADR 0162), retaining all other budgets and
shared body/interface/declaration accounting. Aggregate diagnostics expose each
resource count.

74 focused/composed checks and the full solution build pass. A 512-method authored
closure executes under O0/O2 sanitizers and frame/cleanup checks; 513 methods fail.
Review checked every cap consumer and unchanged aggregate resource bounds.
`gate-mapped-6c47-trial.json`, using the 6d.23 core/digest, now reaches unsupported
ResolutionStatus enum layout. Native transcripts remain pending.


## Slice 6c.48 outcome

Validated exact Int32-backed enums now lower to existing scalar storage in method
signatures, locals, class/value/static fields and borrows (ADR 0163). Closed generic
identities retain named enum signatures. Boxing, arrays, other underlying widths
and general Enum APIs remain diagnostic.

130 focused/composed checks pass, including 588 repeated O0/O2 differential results,
shape rejection and generic storage independence. Full solution builds cleanly.
Review checked base identity, bounded caches, storage conversion and unsupported
boxing. An older aggregate-field fixture's cloned lexical identity was corrected.
`gate-mapped-6c48-trial.json`, with the 6d.23 core/digest, now reaches Boolean field
storage at ResolutionState.IsTerminal. Native transcript equivalence remains pending.


## Slice 6c.49 outcome

Boolean/Char and signed/unsigned byte/short fields now use explicit managed-width
normalization over private Int32 runtime slots (ADR 0164). Class, value and static
reads/writes, copies and zeroing are admitted; narrow addresses remain diagnostic.

63 focused/composed checks and the full solution build pass. Thirteen probes supply
1,662 repeated native O0/O2 comparisons including authored stores without conversion
opcodes, all six widths and noncanonical Boolean values. Review verified casts,
resolved generic fields, fault/initializer order and borrow rejection.
`gate-mapped-6c49-trial.json`, using the 6d.23 core/digest, now reaches metadata work
exhaustion at ResolutionState's canonical serialization callback. Native transcripts
and implementation/filter closure remain pending.


## Slice 6c.50 outcome

Protected-method validation now shares a bounded view of the compiler's metadata
ledger and returns successful exact call bindings for discovery reuse (ADR 0165).
Per-read limits, shared charging, generic context, access and intrinsic checks remain
in force; no metadata limit was increased.

70 focused/composed checks and the full solution build pass. Review checked parent
charging, per-read exhaustion, fresh-ledger isolation and declaration-only reuse.
`gate-mapped-6c50-final.json`, using the 6d.23 core/digest, now reaches PlayerId array
allocation at Adapter.Prepare IL_008d. This is a real value-vector requirement in the
unchanged probe/kernel path; native transcript equivalence remains pending.


## Slice 6c.51 outcome

Closed value-array identities now have detached, bounded scalar storage facts
(ADR 0166). Native allocation zeroes typed Int32/Int64/reference buffers, charges
their exact bytes and traces every reference slot under forced collection.
Value arrays remain invariant. Object layout v12/native v34 record the contract.
Three focused checks plus existing array conformance cover malformed models,
O0/O2 sanitizers, exact/short quotas, zeroing, tracing and reclamation. Review
corrected catalog walkers that assumed every named array element was a class.

This is the storage slice only. Value element reads/writes/borrows and CIL/library
integration remain next; the real-kernel gate still requires `PlayerId[]`.
No native kernel transcript equivalence is claimed.


## Slice 6c.52 outcome

Scalar backend borrows now address exact components of value-vector elements
(ADR 0167; scalar location IR v2/native v35). Typed descriptor tables preserve
field kinds and reference predicates; owner roots retain arrays across collection.
Null precedes bounds, and foreign/expired/readonly carriers fail closed. Twelve
focused/composed checks pass, including 288 repeated O0/O2 .NET/native outcomes
with ASan/UBSan and forced collection. Review also strengthened catalog equality
to compare value-array layouts. Logical copies and CIL/library integration remain
next. Native real-kernel transcript equivalence is still pending.


## Slice 6c.53 outcome

Logical value-vector reads, writes and synchronous borrows now expand through
validated scalar shapes (ADR 0168; value array IR v2/value location lowering v4).
Copies remain independent; references and borrowed owners stay rooted. Empty
values retain checks through private storage. Thirty-four focused/composed checks
pass, including 576 repeated O0/O2 .NET/native outcomes with sanitizers and forced
collection. Review checked copy ordering, shape consistency and empty-value checks.
CIL and collection/interface admission remain next; the real kernel has not yet
passed its value-array gate or native transcript comparison.


## Slice 6c.54 outcome

Closed value-vector CIL now admits allocation, typed reads/writes, synchronous
element addresses and class/value array fields (ADR 0169). The existing exact
Array.Empty grant also admits value arrays. Four C# probes supply 1,176 repeated
O0/O2 comparisons; a ported empty-value-array probe adds 294. All 79 focused and
regression checks pass with sanitizers, forced collection and frame budgets.
Review added explicit rejection of primitive TypeDef operands masquerading as
value elements. The solution builds cleanly.

The real kernel passes `Adapter.Prepare IL_008d` and now exhausts the unchanged
metadata work ledger in `PriorityWindowRequest::.ctor IL_009d`, reached from
`Adapter.Prepare IL_00b4`. Evidence: `gate-mapped-6c54.json`, using the unchanged
6d.23 core image/manifest. Array interfaces and the remaining kernel closure still
need completion; native transcript equivalence remains unproven.


## Slice 6c.55 outcome

Library method declaration matches now share the caller-owned ledger after full
validation (ADR 0170). Exact plan/image/token identity prevents cross-plan reuse;
closed arguments, executable constraints, access and grants remain independently
checked. Profiling identified repeated immutable declaration work, so the metadata
limit remains 65,536. All 121 focused checks and the solution build pass. Review
confirmed successful-only publication and bounded shared-ledger lifetime.

The real kernel passes the metadata failure and now reaches the existing boxed
interface dispatch restriction in `ResolutionHandlerRegistry.Get IL_0016`, via
`DriveAutomatic IL_0121`. Evidence: `gate-mapped-6c55.json` with the unchanged 6d.23
core. Native transcripts and the remaining semantic/library closure are pending.


## Slice 6c.56 outcome

Review of the boxed-formatting gate exposed an earlier String discovery regression:
ordinary static-body fallback preceded known String intrinsics. Exact intrinsic
matching/authorization now runs first (ADR 0171), restoring framework-fixture
Concat/equality without changing ported IsNullOrWhiteSpace execution. A reduced
C# probe checks absence of framework String bodies and native results; existing
composed string and exhaustive whitespace tests pass. This is a regression repair;
boxed use validation and the remaining kernel gate are still being completed.


## Slice 6c.57 outcome

A bounded detached use proof now distinguishes boxes confined to unboxing/helpers
from boxes that could reach interface dispatch (ADR 0172; boxed value IR v3/scalar
boxed IR v4). The proof is independently checked after scalar lowering. Direct,
mixed and helper-escaped boxed dispatch remain diagnostic. New native probes
cover an unbox helper and ported formatting with unrelated interface calls, adding
588 repeated O0/O2 .NET comparisons with sanitizers and forced collection; a
detached scalar alias mutation is rejected.

The gate has progressed to the missing invariant value-array interface assignment
in `Adapter.Prepare IL_00b4`: PlayerId[] to IReadOnlyList<PlayerId>. Evidence is
`gate-mapped-6c57.json`. Whole-program IR and native transcript checks remain pending.


## Slice 6c.58 outcome

Closed value-array interface assignment and Get/Set/Add/Count helpers now use exact
value argument facts and the existing grant-checked managed library bodies (ADR
0173). Scalar signatures check each value leaf and writable return destination;
empty values retain checked array operations despite having no signature leaves.
The new probes cover nested/closed values, reference lifetime and copy independence,
invariant casts, null/index/fixed-size faults, empty values and List/read-only views.
They supply 1,176 repeated O0/O2 native outcomes with sanitizers and forced collection.
Detached signature/shape mutations fail validation. Review also removed redundant
closure work when no new assignment edge exists, restoring the exact 256-type
interface-chain regression without raising limits.

The real kernel now reaches unsupported switch at ResolutionKernel.Step IL_003c;
see gate-mapped-6c58.json. Native transcripts and the final closure review remain.


## Slice 6c.59 outcome

Integer managed-object CIL v2 now lowers bounded switch tables to existing branch
IR (ADR 0174). Additional comparison blocks remain in source/exception-region order,
within the switch operand span. Selectors are consumed once; duplicate cases,
defaults, residual stack transfers, loops and catch/finally unwind preserve .NET
behavior. Three C# and two authored IL probes supply 1,488 repeated O0/O2 native
outcomes with sanitizers and forced collection. Invalid selectors/operand targets
and the exact 4,096 expanded block accept/reject boundary are checked.

The unmodified kernel reaches complete body translation, then fails detached IR
validation at ResolutionRejection::.ctor IL_002d: direct readonly constructor
receiver provenance is lost across a null-coalescing branch. Evidence:
gate-mapped-6c59.json. Native transcripts remain pending.


## Slice 6c.60 outcome

Readonly constructor receiver provenance now survives branches when parameter zero
cannot be mutated, and integer-object constructors may branch after a validated
entry initialization prefix (ADR 0175; object operations IR v14). The immediate
base call remains mandatory exactly once before branching; body re-entry, repeated
base initialization and local handlers around the prefix are rejected. Existing
pre-base escape/access rules remain in force. A reduced null-coalescing constructor
adds 294 repeated O0/O2 .NET/native outcomes with sanitizers and forced GC. An
address-mutated argument fixture and detached re-entry/reinitialization mutations
remain diagnostic.

The real kernel progresses to cast exception name-resource validation;
gate-mapped-6c60b.json records the next failure. Native transcript equivalence is
still pending.


## Slice 6c.61 outcome

Compiler-generated angle-bracket names are admitted in bounded cast-error text
(ADR 0176). The kernel's ResolutionFrame+<>c triggered the previous name rejection.
The exact .NET message for a closed anonymous type now matches in 294 repeated
O0/O2 native comparisons. Ten focused exception/boxing checks and the full build
pass. Existing name-length/aggregate UTF-16 limits remain unchanged.

The real kernel passes translation with no diagnostics; scalar lowering and C++
emission also succeed. Evidence: gate-mapped-6c61.json and kernel-6c61.cpp. The
native transcript comparison is now running; this slice does not claim equivalence.

## Slice 6e outcome

The unchanged private kernel and concrete adapter now produce equivalent .NET and
native command transcripts. The pinned source build is revision
`a8942127ee6d26545de6daa2259cc2793c13be8e`; copied kernel/primitives image hashes
match the earlier clean build evidence. A fresh .NET 10.0.11 run matches the saved
oracle byte-for-byte (SHA-256
`b7a111e6dd0e2cac34e46b352cb0de2462e431a3a0e3f20bdded311176a734af`).

Ten steps cover boundary rejection, immediate and framed completion, priority
passes/actors, rejected commands, terminal state, event ordering, state identity
and sequence fields. Each pass checks 130 scalar fields and 766 canonical UTF-16
code units. Three repeats at O0/O2, both normal and collect-before-allocation,
produce **10,752 exact comparisons** under ASan/UBSan. All runs pass and release
frame/borrow roots. Retained objects are stable after warm-up. Observed maxima:
32,168 heap bytes/302 objects normally, 10,710 bytes/101 objects with forced
collection, and 21,568 execution-storage bytes in both modes. These are bounded
probe measurements, not production performance claims.

Private evidence is `artifacts/milestone6-harness/evidence-6e.json`, the four
`native-6e-O*-*.txt`/baseline logs, generated C++ and its harness. The unchanged
6d.23 trial core/digest remains selected. No application game rules were changed,
no hosted jobs ran, and no public ABI/platform compatibility is claimed. The
implementation/filter closure audit and final composed review remain for 6f.

## Slice 6f closure audit

The successful executable closure contains 358 closed methods from 287 distinct
source definitions, including 148 selected-library instances from 77 library
definitions. Every source definition was checked in its hash-pinned PE image:
all have managed bodies, and none has an exception region or filter. The compiler
independently validates calls, closed dispatch targets, selected intrinsic grants,
logical IR and scalar IR. No required executable body is deferred. For this exact
root, ADR 0004 stage B is not required; general filters remain unsupported outside
this bounded closure.

The independent conservative metadata audit remains `incomplete`: it reports 34
body/intrinsic declarations, 950 array-helper cases, 78 variant-interface cases and
8 unresolved receiver sites. That walker does not apply the compiler's exact
closed receiver/helper/intrinsic lowering, and can visit fallback declarations
which are absent from the compiled program. Its output has not been relabelled as
successful. The milestone filter decision uses the validated executable closure
and its source-body audit, rather than treating that broader metadata inventory
as proof. Private evidence: `closure-6f-compiled.json`,
`closure-6f-source-bodies.json`, `closure-6f-metadata.json` and the hashed review
`closure-6f-reviewed.json` under `artifacts/milestone6-harness`.

The public composition regression combines value-bearing List/Dictionary storage,
live read-only views, boxed formatting, type-test filters, allocations in catch
paths and finally updates. Its two native builds provide 294 repeated .NET
comparisons with collection before every allocation, ASan/UBSan, exact/short
execution-frame checks and cleanup. Both filter and finally regions are explicitly
asserted to remain in the translated program.

Final validation: the full local run executed 2,630 checks in 611 seconds. It found
three stale boundary expectations from earlier method-limit/name-index changes.
Those tests now check the exact 512-method and six-unit cold lookup boundaries;
production limits and semantics were unchanged. A subsequent 163-check run,
including all affected classes and the new composition regression, passed. The
final two composition checks passed again after adding region-presence assertions.
This covers the final 2,632-check suite across the full run and targeted reruns;
the expensive full suite was not repeated after test-only corrections. Full solution
build: zero warnings/errors. Logs remain under `artifacts/milestone6-harness`.

Final review found no unresolved defect in the admitted milestone scope. Reviewed
areas include C++ defined arithmetic and source-order control flow, exact value
array layouts/interface signatures, constructor receiver ownership, rooting across
allocation/unwind, repeated heap cleanup, intrinsic/assembly provenance and filter
absence in the actual kernel closure. Local main `48ecf14` is an ancestor of this
branch; no remote history, hosted CI, publishing or application source was changed.

Milestone 6 is complete. Milestone 7 begins the separate public C ABI and mobile
library/device lifecycle gate. General filters, arbitrary library members,
primitive-width/enum/nested arrays, escaping managed locations, general boxed
interface dispatch and untested platforms are not implied by this completion.

## Follow-on saved-game baseline (2026-10-06)

The first full-application replay slice establishes a .NET oracle, not native
compatibility. A private frozen source/content archive was recovered and checked
against its byte count, SHA-256, source archive and extracted-file manifests.
The source snapshot was rebuilt unchanged with .NET SDK 10.0.400 on macOS ARM64.
Build properties explicitly pin its source revision and disable inherited
analyzers for this private build; this is not an application analyzer-clean claim.
The compiler probe uses baseline `31a0e21` and the existing reviewed 6d.23 core.

Three saved games cover a selected-seat win, a selected-seat loss and a natural
round-limit stop. Their 81, 614 and 2,532 accepted command envelopes (3,227 total)
all restore to their archived final state hashes under .NET 10.0.11. Recorded
scenario, seed, match identity, command payloads, expected versions, origins and
ordering are retained. The traced replay also matches the application's existing
full restore. Two independent process runs produce byte-identical traces with
initial/per-command state hashes and ordered event type/text observations. The
event text is not a complete payload serialization or a native equivalence gate.
A fresh command ID with a stale expected version is rejected without changing
the final state or emitting events; previously accepted IDs use host idempotency.

The metadata-only constructor/step probe is deliberately not a runnable saved-game
adapter: it supplies null inputs solely to select the real engine dependency path.
Translation stops at `CIL3015`, an unmapped `System.Type` required by application
command dispatch. This is the first observed compiler/library gap, not the only
missing capability. No application rules were changed and no mapping was invented
to bypass the diagnostic. The broader explicit-library-contract inventory is
incomplete: 6,218 nodes, 32,596 edges, 1,587 deferred nodes and 3,553 analysis
issues, including the default 100,000 dispatch-pair limit. Repeated analysis
issues are not a count of distinct compiler features to implement. Library/filter
closure and full-game native execution remain unverified.

Private source, adapters, raw saves, traces, inventories, review and hash evidence
live under ignored `artifacts/alchemy-replay-baseline/`. From that directory,
`python3 run_baseline.py` verifies saved evidence and unchanged source/content;
`python3 run_baseline.py --run` repeats the bounded local build/oracle/probe recipe
using its pinned local dependency paths. Python 3.11 was used for archive recovery
and this recipe. No training, services, hosted jobs or pushes ran. This follow-on
does not broaden milestone 6's completed kernel scope or milestone 7's ABI scope.


### Runtime type identity follow-on (2026-10-06)

ADR AC0001 implements the first observed engine blocker: bounded typeof/GetType,
RuntimeTypeHandle copies/round trips, Type equality/hash and Type dictionary keys.
The four public fixtures add 1,176 repeated .NET/native O0/O2 comparisons under
ASan/UBSan and forced collection. Cache ownership, heap limits, missing grants,
abstract allocation, unsupported receiver dispatch and detached IR are checked.
The runtime uses per-instance canonical objects and precise roots; no application
rules or source snapshots were changed.

The same private constructor/step metadata probe, with the newly reviewed local
core, now stops at CIL3015 for System.Nullable<T> (netstandard token 0x020000af).
Core SHA-256: `d9b38fd383390a7d1aa3cd9c3b14a28ad5c6e1e2a2fa61f04bfa52dd30dab6d4`.
Local fixture manifest digest:
`a93fff8ac6617e180a927fa190ccdb3ba4e9a73f60f277e051771da6fe5c9c91`.
Its 219 recorded input hashes were verified separately before fixture admission.
This removes the initial Type mapping barrier, not every dependency of command
dispatch. The three-game .NET oracle remains the existing 3,227-envelope baseline;
full-game native transcripts and implementation closure remain pending.

Private new-core build, local approval and metadata-probe evidence live under
ignored artifacts/alchemy-runtime-types/. Historical baseline evidence remains
unchanged and retains its original compiler pin. Targeted local regression logs
are in the worktree's artifacts/runtime-types/. No hosted work or push ran.

Final local validation passed all 241 targeted tests spanning identity, class
resolution/layout, boxing, core admission, dictionaries, arrays, collection,
virtual calls and both embedding ABI versions. The Release solution build has
zero warnings/errors. The complete expensive suite and private kernel transcript
were not rerun; this follow-on has its own reduced native differential evidence.


### Prioritized saved-game compatibility audit (2026-10-06)

The bounded audit uses compiler implementation `a662904` / documentation baseline
`03c6109`, the unchanged source snapshot `c18d1a53dca8bdc48f78f4e53aaf89185963e6d7`,
and the reviewed runtime-type core/manifest recorded above. Compiler DLL hashes
match that reviewed build; all 895 frozen source files still match the source
archive. No application rules, production compiler code or library code changed.
The separate milestone 7 checkout was left alone; local main `48ecf14` remains an
ancestor of this branch. This inventory does not claim the newer ABI features
were integrated or tested on this branch.

The existing broad engine contract inventory was reused, with five new bounded
method-root audits: command-handler construction, participant lookup, canonical
state hashing, grid distance and deterministic random generation. Each new audit
is capped at 3,000 nodes, 15,000 edges and 12,000 dispatch pairs, with a 45-second
process timeout. All completed without increasing their bounds. Handler discovery
hits the node bound; state hashing hits the dispatch-pair bound. Participant lookup
retains nine dispatch-analysis issues without exhausting a bound. Grid distance
and random generation have zero audit issues, but still defer framework contracts.
The original broad audit remains incomplete at its 100,000-pair bound.

Across the six inventories, 260 distinct observed framework declarations were
checked against the current explicit binding plan: 125 map and 135 do not (31
types, 90 methods and 14 fields). These are declaration counts, not 135 independent
features. Mapping proves neither an executable body nor supported semantics:
Dictionary.Add and the indexer getter map but reach explicit unsupported stubs.
Conversely, intrinsic and interface declarations can legitimately have no managed
body. Unmapped ancestry interfaces and reference primitive declarations are not
all runtime blockers. Evidence retains incoming-edge kinds, exact tokens, scope,
constructed instances and call-site witnesses to keep these distinctions visible.

A separate metadata body scan decoded 1,775 of 1,825 observed application method
definitions; 50 have no managed IL body. It observed 106 finally regions, five
catch regions and one filter region. The original audit classifies that filter as
a type-test filter. These bounded results do not prove the absence of general
filters, unsupported dispatch or additional operations beyond the observed graph.
Reflection/unsafe name/opcode signals are review leads, not automatic feature gaps.

Fifty-two focused translation probes produced nine valid C++ artifacts and 43
build diagnostics. The nine working cases were also compared with a separate .NET
oracle: 49 input pairs, three repeats and O0/O2 give 2,646 passing comparisons with
ASan/UBSan, a 2 KiB native-frame warning and collection before each allocation.
They cover arithmetic, wide multiplication, existing Type keys/list operations,
Int32 delegates, enum locals/fields, reference delegate arguments returning Int32,
and the unchanged real deterministic-random helper. This is reduced conformance,
not execution of full saved games. The two additional mapping-only experiments
add already-existing Byte/Char declarations solely to a private plan: byte vectors
still reject as unsupported storage; String(char[]) still lacks its constructor.
No production mapping or trusted manifest changed.

Prioritized implementation packages:

| Order | Package | Confirmed gap and likely benefit |
| --- | --- | --- |
| 1 | Nullable values | 486 distinct observed constructor/accessor call sites across 27 closed Nullable types in the broad graph. Engine Step, participant lookup and state hashing all stop here. Cover closed structs, reference-bearing payloads, default/HasValue/Value/GetValueOrDefault and explicitly bound nullable boxing semantics; an Int32-only special case is insufficient. |
| 2 | Reference-returning delegates | The real command handler returns EngineStepResult. An isolated reference-returning delegate is rejected while a reference-argument/Int32-return delegate passes natively. Extend rooted return handling and exact Func/Action/Predicate/Comparison contracts as required, rather than treating all delegate support as absent. |
| 3 | Collection operations and enumeration | Dictionary Add/read/remove, keys/enumeration; List enumeration, enumerable constructor, RemoveAt/Contains/AddRange/Sort/ToArray; HashSet and comparer contracts. Some are missing declarations, others intentional unsupported bodies. Observed engine uses include Dictionary.Add (11 sites), List enumerable construction (39), List.Sort (13) and HashSet.Add (19). Supplementary probes also cover nearby collection APIs not observed in this bounded graph. |
| 4 | Numeric, enum and constant-data APIs | Math Abs/Min/Max, integer CompareTo, Enum.IsDefined, Guid text construction, larger ValueTuple shapes and RuntimeHelpers.InitializeArray. The real grid-distance helper stops at Math; enum locals/fields already pass, while enum vectors reject. Implement measured operations, not broad enum/reflection claims. |
| 5 | Iterators and LINQ | Missing Enumerable/Func contracts and collection enumerators; 80 observed LINQ call sites. Array IEnumerable iteration rejects its helper contract. A yield iterator reaches Environment.CurrentManagedThreadId; this compiler-generated bookkeeping is not evidence that Alchemy requires multithreading. Re-probe after collection/delegate work. |
| 6 | String, encoding and hashing path | Additional Concat/Format/comparison/constructor APIs, byte-vector storage, UTF-8 and SHA-256. SHA256.Create is reached through handler -> spell-draft admission -> spell compilation -> SemanticFingerprint.Hash, so crypto cannot be excluded as replay-host-only work. Schedule the buffer/encoding/hash design alongside the earlier packages. |

This order is an architectural recommendation based on observed dependencies, not
an effort estimate or a promise that each package alone unlocks the full engine.
Nullable has the broadest measured immediate reach. Keep the remaining confirmed
gaps in the backlog and re-run the same focused probes after each coherent slice;
do not repeatedly enlarge the whole-program analysis budget.

The existing three-game oracle contains 3,227 accepted envelopes, 14 top-level
command types and six nested UnitAction kinds. It remains useful for integration
coverage, but does not cover every registered command, branch, rejection or filter.
Recorded decoding/IDs/seeds and host replay maintenance must be preserved in the
future native adapter. Keeping JSON/gzip in the host does not remove the engine's
own semantic fingerprinting dependency. No full replay was rerun or claimed native.

Review verified that declaration matches are not counted as execution support,
repeated issues are not counted as features, positive probes have native evidence,
missing Byte mapping is separated from unsupported byte storage, and all source
inputs remain unchanged. No remaining defect was found in this bounded audit
procedure. Full implementation closure and unknown paths remain open.

Private tools, raw results, witnesses, source hashes and logs are under ignored
`artifacts/alchemy-gap-audit/` in the primary checkout. `summary.json` is the grouped
inventory; `declarations.json` and `witnesses.json` retain auditable evidence;
`probes.json`, `mapping-only-probes.json` and `native-results.json` retain exact
outcomes. `run_audit.py` repeats the bounded metadata/translation work using the
pinned binaries, `summarize.py` groups it, and `native_probe.py` runs the existing
positive fixtures against `oracle.json`. `verify_evidence.py` checks the final
input/output hashes and assertions without rerunning builds. Restore/build the
private Harness, Probes and Oracle projects with the recorded local SDK if their
binaries are absent; their project files reference the frozen built images, never
modify or execute assemblies inside the metadata compiler. No services, training,
hosted jobs, pushes or additional agents were used.

## Parallel Alchemy compatibility implementation plan

Recorded 2026-10-06. Status: approved delivery plan; AC0, AC1a, AC1b.1–AC1b.4 and AC2.1–AC2.2 and AC3.1–AC3.4 complete within their tested bounds; remaining AC1b, AC3 and AC4–AC10 planned. Private closed callback signatures remain subject to AC8 revalidation.
The completed type-identity slice and bounded audit above are the starting evidence.
Use AC identifiers for this workstream so milestone numbering and existing exit
gates remain intact. Milestone 6 stays complete for its measured kernel scope.

### Relationship to the milestone roadmap

The milestone track delivers embedding and platforms. The AC track expands the
managed code that Cilica can execute, using saved games as integration evidence.
Neither track substitutes for the other's correctness or target-device gates.

The milestone 7 branch at `77661ab` has scalar/shared exports, retained handles,
UTF-16 copying and Integer64 transfer. Structured value copying, detailed exception
transfer and physical-device packaging/tests remain open. This is a snapshot of
that parallel branch, not a claim that its newer commits are integrated into this
compatibility branch. At that planning checkpoint it shared milestone work through
`f538e13`; the AC0 outcome below records the later integration.

| Milestone track | Work that can continue independently | Required join with AC |
| --- | --- | --- |
| 7: embedding and phones | Finish value/error contracts and package/test the already-verified bounded kernel on physical iOS, then Android | Share compiler/runtime integration checks when AC changes lowering, roots, packets or allocation. Full Alchemy coverage is not an added prerequisite for the existing bounded-kernel milestone 7 gate. |
| 8: Expo and offline Alchemy | Build bridge lifecycle, app shell and persistence adapter against a pinned supported fixture | The claimed offline Alchemy scenario must also pass AC9 replay equivalence and the relevant AC10 device checks before its start/play/terminate/restore gate closes. |
| 9: browser parity | Prepare Emscripten build/host infrastructure against supported fixtures | Reuse the pinned replay corpus and application contract from AC9; require browser/native/.NET equivalence on the claimed scope. Native results alone do not close this gate. |
| Benchmarking workstream | Maintain bounded baselines for supported workloads | Measure new operations only after semantic equivalence; optimization is not a compatibility workaround. |

Parallel here means separately tracked work that can progress without waiting for
unrelated features. It does not launch agents, duplicate tasks, hosted jobs or
simultaneous modifications to shared compiler files. Keep one active AC delivery
slice by default; ongoing milestone work can continue on its own branch.

### Delivery slices and acceptance gates

AC0, AC1a, AC1b.1–AC1b.4 and AC2.1–AC2.2 and AC3.1–AC3.4 are complete within their recorded bounds; the remaining slices are planned. Split a slice into coherent reviewable commits
when its implementation crosses more than one semantic boundary. Library APIs
are selected from the declaration ledger and actual compiled closure, not a
blanket promise to implement the entire corresponding .NET class.

| Slice | Implementation and architectural choice | Dependencies | Acceptance gate |
| --- | --- | --- | --- |
| AC0: shared baseline and gap ledger | Integrate reviewed type-identity and milestone commits at a stable boundary. Reconcile emitter/profile changes, duplicate ADR 0180 allocations and links; preserve both decision records. Promote general reduced probes to public conformance fixtures while keeping Alchemy inputs private. Assign every observed declaration/probe to a slice or an explicit unresolved/nominal-only category. | Existing audit and committed milestone snapshot | Clean integrated build; targeted type/cache plus affected embedding regressions; reproducible pinned probe baseline. No silent profile-ID collision, stub counted as support, or unassigned confirmed blocker. |
| AC1a: nullable storage and members | Implement closed Nullable<T> with presence state plus an ordinary inline payload. Reuse existing value copies, generics and reference-slot tracing rather than special-casing Int32. Admit constructor, HasValue, Value and both GetValueOrDefault forms against exact reference declarations; record any further required members separately. | AC0 | Differential empty/present/default/copy/return/field tests for scalar, enum, application-ID and reference-bearing struct payloads, plus arrays for admitted storage shapes; enum-vector composition joins AC5b. Preserve Value's exception behavior and references across GC. Engine/state probes advance beyond their current Nullable mapping error or reveal a precisely recorded next limitation. |
| AC1b: nullable conversions and boxing | Implement the required lifted/conversion IL and special nullable boxing/unboxing rules: empty boxes as null, present boxes as the underlying value. Reuse underlying value descriptors and roots. Unsupported remaining forms stay build errors. | AC1a; exact closure identifies required forms | .NET comparisons for null, present underlying type identity, wrong-type unboxing, mutation/copies and collections. Prove any deferred nullable form is absent from the accepted replay closure; do not silently give nullable boxes ordinary struct semantics. |
| AC2: delegate results and contracts | Extend single-target delegates to reference results using rooted return storage. Add only the required closed Func/Action/Predicate/Comparison declarations and tested signatures. Preserve target ownership, receiver null checks and exception propagation. Keep multicast/variance outside scope unless a reduced required case establishes the need. | AC0; AC1 for the composed command fixture | Reference result survives allocation and collection before storage/return. Native comparison for static and instance targets, null/fault paths and shared object identity. A reduced Type-to-handler dictionary returning a result object executes correctly. Existing Int32/void and reference-argument paths remain valid. |
| AC3: collection storage APIs | Fill required Dictionary Add/getter/remove and constructor gaps; List enumerable construction, mutation/search/copy APIs; HashSet and exact comparer contracts. Keep algorithms in the owned managed library and reuse narrow equality/hash/compiler hooks. Match each required overload explicitly. | AC0; AC1 for nullable/value elements; iterator-consuming APIs join AC4 | .NET comparisons for duplicates/missing keys/nulls, growth, collisions, mutation, receiver-copy behavior and specified exception order. Mapped unsupported stubs are replaced only for admitted members; other overloads still diagnose. Preserve observable application ordering without claiming a universal .NET hash value or enumeration-order contract. |
| AC4: enumeration and iterators | Implement required List/Dictionary enumerators, array IEnumerable helpers, interface dispatch and disposal/finally behavior. Include iterator-generated current-thread bookkeeping only through an explicit single-thread runtime contract; it is not authorization for multithreading. Implement version/invalidation behavior where required by the reference API. | AC3 storage foundation; AC0 dispatch; AC1 where payloads require it | Empty/nonempty, nested/copy/dispose/early-exit/fault enumeration agrees with .NET. Mutation invalidation and GC-held Current values are tested. A real iterator path advances beyond the current Environment/helper errors. Unknown dispatch remains diagnostic. |
| AC5a: numeric and ordering helpers | Add observed Math Abs/Min/Max, integer CompareTo and required Guid text/number APIs as ordinary managed library code where practical. Supply the observed comparer behavior needed by sorting. | AC0; no need to wait for delegates or full replay | Boundary tests cover MinValue Abs overflow, signed/unsigned comparisons, culture/format restrictions and exact parse/constructor failures. Real grid-distance helper matches .NET, not merely emits C++. |
| AC5b: enum, tuple and constant data | Add required larger ValueTuple shapes, Enum.IsDefined/comparison and the bounded value/enum metadata identity they require. Implement validated RuntimeHelpers.InitializeArray/field-data handling and required enum-vector semantics with explicit bounds. Do not widen this into general reflection. | AC0; existing type/value/array infrastructure; coordinate shared runtime edits with AC1/AC7a | Exact enum members/invalid values, tuple copy/equality/hash contracts, malformed tokens/RVA data, element widths, length and ownership are checked. Required reduced probes pass; unsupported token/reflection shapes still reject. |
| AC6: observed LINQ subset | Implement required Enumerable operators using managed iteration and existing collections, beginning with Any/Where/Select/First/FirstOrDefault and materialization; then ordering, Concat, Distinct/Contains/OfType/Single/Count as the ledger requires. Preserve deferred execution, disposal and callback/exception order. | AC2, AC3, AC4; AC5a comparers for ordering | Differential empty/error/multiple-enumeration/early-stop cases and callback side-effect order. Required application helper slices execute with actual closures. Each remaining observed overload has an explicit status. |
| AC7a: byte and character vectors | Add bounded byte/char array storage and operations, including loads/stores, conversions, bounds, type identity and memory accounting. An additional mapping alone is insufficient, as the byte probe demonstrates. Coordinate descriptor/tracing/packet changes with milestone 7. | AC0; may proceed independently of nullable/delegate APIs, but serialize shared emitter edits | Native/.NET edge values, array identity, copies, bounds/fault order, allocation exhaustion and lifetime checks. No regression to existing Int32/reference/value vectors or host UTF-16 transfer. |
| AC7b: required text and encoding | Add exact observed Concat/Format/comparison/constructor/StringBuilder overloads, ordinal comparer behavior and UTF-8 conversion. Prefer managed implementations over target-specific calls. Preserve UTF-16 code units and specified invalid-surrogate/fallback behavior. | AC0 for scalar text helpers; AC7a for buffer/string construction and encoding; other formatting dependencies from ledger | Complete output-code-unit/byte comparisons, empty/null/invalid input and overflow/allocation cases. Unsupported culture/provider overloads retain actionable errors. |
| AC7c: SHA-256 and fingerprinting | Implement the required Create/ComputeHash/disposal surface using a reviewed portable managed SHA-256 implementation and existing Cilica arithmetic. Record source/license provenance if adapted. This is engine work as well as replay validation: spell compilation calls SemanticFingerprint.Hash. | AC7a, AC7b and required collection/numeric operations | Standard known-answer vectors plus .NET byte-for-byte tests at block boundaries, empty/multiblock inputs and repeated instances. Run the unchanged semantic-fingerprint helper with real inputs; no host-only substitute hides a reachable engine gap. |
| AC8: replay contract and executable closure | Define an application-owned facade for scenario creation, command application and state/event inspection, discoverable through one ordinary inclusion root. Keep decoding/adapters outside compiler packages. Start facade/schema design after AC0; compile and review the complete required implementation closure as prerequisites land. | AC0 design; required AC1–AC7 implementations; appropriate milestone 7 boundary capabilities for C-host integration | Every reached method has an admitted managed body or exact granted intrinsic; required filters/dispatch/callbacks are resolved. Track actual method/type/instruction/frame/heap counts. New blockers enter reduced fixtures; neither missing bodies nor capacity failures are waived. |
| AC9: native saved-game equivalence | Replay all three frozen games using original scenario/content, seeds, identities, payloads and order. Preserve host initialization/pruning and recorded-command behavior. Strengthen ordered event observations with an application-owned canonical payload projection shared by both oracle/adapters; event text alone is insufficient for a full event-equivalence claim. | AC8 | At every command compare acceptance/rejection, version, canonical state, ordered event payloads and final archived hash against .NET. Cover stale/duplicate commands, restoration and fresh-instance repeats. O0/O2 and normal/forced GC runs pass under sanitizers. Resolve any oracle/adapter mismatch before blaming or changing engine rules. |
| AC10: milestone integration | Carry the same compiled engine contract and corpus through public ABI adapters, then physical iOS/Android and browser hosts as their milestones mature. Persist application save data, not heap addresses or object handles. | AC9 plus each target's milestone infrastructure | Retain/release, exact string/wide transfer, fault recovery, small quotas and repeated create/destroy pass on each claimed host. Phone terminate/restore and browser traces match the oracle. Record exact source/core/compiler/SDK/device/OS versions; no target inherits another target's pass. |

### Work that can proceed alongside other work

After AC0, nullable work (AC1), delegate work (AC2), leaf numeric helpers (AC5a),
portable buffer design (AC7a) and replay-facade design (AC8) have largely separate
functional prerequisites. They are eligible for parallel workstreams, not automatic
concurrent edits. The default AC execution order starts with nullable support,
then the reference-result command-dispatch fixture, then collection foundations.
Small library-only numeric/text slices can land between runtime changes when their
exact dependencies and review gates are satisfied.

Collections split at a deliberate dependency boundary: implement storage and
mutation before enumeration; iterator-consuming constructors/AddRange then compose
with AC4. LINQ follows tested enumeration/delegates. Encoding and hashing have a
separate prerequisite chain through byte/char storage. Begin their design early
because SHA-256 lies on an actual engine path, even though fewer audit sites use it.
Do not infer priority only from call-site frequency.

The first demonstrable checkpoints are: nullable API conformance; a native
Type-keyed handler dispatch returning a result object; an unchanged application
helper beyond the already-working RNG; complete executable engine closure; the
three-game native transcript; and finally the public ABI/device replay. An audit
with no reported issues is not a substitute for any execution checkpoint.

### Integration and capacity controls

Before each slice, read the relevant current specification and present its concrete
design. Pin the integration base, inspect main and the latest committed milestone
work, and preserve in-progress changes. Commit reviewed semantic slices regularly.
Do not renumber milestones or allocate a global ADR number without checking both
active branches. AC0 resolved the duplicate 0180 records with the separate AC
decision sequence, preserving both decisions and their provenance.

`ObjectCppEmitter`, runtime headers/descriptors, roots/collection, return lowering,
intrinsic capabilities and generated profile identifiers are shared areas. Sequence
changes there and integrate at reviewed commit boundaries. A milestone change to
one of those areas triggers the smallest relevant combined conformance/ABI checks;
a library-only change does not justify rerunning every platform/benchmark matrix.
There is no authorization here to send another task instructions or dispatch agents.

The observed graph is larger than the small examples, but audit nodes are not
executable counts. Measure the translated closure against the current bounds
(256 object types, 512 methods and 65,536 instructions), including library expansion
and static initialization. If a real closure exceeds them, open a separate measured
capacity/design slice: inspect algorithms and generated code size, review memory/
stack/compile-time costs, and add exact/excess boundary checks before changing a
limit. Do not blindly increase analysis limits or discard handlers to obtain a pass.
Independent multi-root discovery is not an AC prerequisite while one ordinary
facade root can retain the required closure. If it becomes necessary, plan it as a
separate compiler slice rather than weakening reachability validation.

### Evidence, review and resource policy

Maintain a ledger with the source/core/compiler hashes, exact API or IL shape,
application witness, reduced probe, owning AC slice, prerequisite, current diagnostic
and measured outcome. Distinguish observed, confirmed blocker, planned, translated,
native-validated and target-validated. Nominal-only observations and deferred paths
remain visible. The 135 declaration binding failures and 43 probe rejections are
starting evidence, not a complete or fixed feature count; nine positive probes
remain regression controls.

For each implementation slice, run focused .NET/native differential tests, O0/O2
when native semantics change, forced collection and appropriate sanitizers. Exercise
failure/limit behavior as well as successful results. Review generated C++ for
defined arithmetic, allocation roots and boundary ownership; add reduced regressions
for defects. Update compatibility/specification/decision records and the ledger,
then commit. Explicit grants and a reviewed manifest remain required; a rebuilt
library is never automatically trusted.

Re-run the small affected probes after each slice. Re-run the bounded broader
inventory at integration checkpoints or when a newly visible dependency warrants
it, not after every edit. Run the full three-game native matrix only after the
compiled closure and smaller composed fixtures pass. A repeated timeout or environment
failure requires a new hypothesis before another expensive run. Hosted runtime/CI
budget is zero unless separately authorized; services, paid plans, benchmark
matrices and additional agents are not started by this plan.

Completion of the AC workstream means the unchanged agreed Alchemy replay scope
passes AC9 with a closed implementation and documented residual exclusions. Device
and browser claims require AC10 per target. The saved games cover 14 top-level
command types and six nested UnitAction kinds, not every possible rule branch.
Extend the corpus deliberately with rejected/duplicate/restored cases and additional
required commands; do not present corpus success as unrestricted C#/.NET support.
Future general filters, async and multithreading retain their existing roadmap gates
unless a concrete required closure promotes a narrowly scoped dependency earlier.


### AC0 integration checkpoint

Integrated milestone 7 through `5a18f9a` (retained exception capture), preserving
its separate ongoing checkout. This supersedes the planning snapshot above;
structured value copying and physical-device validation still remain open.
The compiler changes merged without a code conflict. Reviewed the shared emitter,
handle publication and Type-cache rooting together; 67 focused local runtime-type
and embedding tests passed, including native O0/O2 sanitizer runs.

The combined native emitter profile is `object-frames-native-v37`, distinguishing
it from both parent branches' generated runtime text. The compatibility Type
decision is now [AC0001](../decisions/AC0001-runtime-type-identity.md); milestone
0180 retains its public-handle identity. Future compatibility decisions use the
AC prefix to avoid collisions with the milestone decision sequence. AC0's public
probe and ledger checkpoint follows separately.


### AC0 public probe and ledger outcome

AC0 is complete. The [machine-readable gap ledger](../compatibility/alchemy-gap-ledger.json)
assigns all 260 observed reference declarations, 52 original probes and both
mapping-only experiments to delivery slices or explicit unresolved categories.
Reference tokens are meaningful only with the recorded reference assembly hash.
Mapped declarations remain binding evidence, not claims of implemented bodies.
The private witness references point into the unchanged bounded audit; that
inventory has not been presented as a newly completed application closure.

Promoted 47 generic probes into `CompatibilityGapProbe` and `CompatibilityGapTests`;
the five probes containing actual Alchemy calls remain private. A single built and
explicitly admitted owned core supplies all public translation probes. The ledger
pins the reference hash, fixture source hash, SDK and integration revision, and
retains the original audit core/compiler/source hashes separately. It is an
expected-gap baseline, not a request to preserve unsupported behavior forever:
when a slice advances, update that probe deliberately and add native equivalence.

Validation on macOS ARM64: 39 individual expected diagnostic checks and one
fixture/ledger coverage check pass. Eight positive controls pass at O0/O2 under
ASan/UBSan with forced collection: 49 input pairs times three repetitions times
eight probes times two optimization levels = 2,352 result comparisons, plus
frame-budget and post-collection checks. The earlier combined integration passed
67 runtime-type/embedding checks. Hosted CI and full saved-game replay were not run.

Two public probes retain array locals that the original private IL did not retain.
Byte and enum vectors therefore fail during local storage validation with CIL2001,
before the private fixtures' mapping/field-layout diagnostics. Both IL bodies were
inspected; the ledger keeps the old diagnostic and the new expectation, with an
explanation. No compiler semantic change or newly supported array is claimed.
After correcting those two expectations, reran only the 40 inexpensive baseline
checks; the two native positive-control tests had already passed.

Review checked diagnostic specificity, all ledger assignments, exact reference
identity, separation of historical/private evidence from current public tests,
unchanged unsupported semantics and no application dependencies in the fixtures.
The old audit referenced compiler DLLs in mutable build output directories. Exact
original copies were recovered from the audit harness and archived under ignored
`artifacts/alchemy-gap-audit/pinned-compiler-a662904/`; the original manifest and
results are unchanged. `archived-input-relocations.json` records those two paths,
and `verify_archived_evidence.py` rechecks the historical hashes, counts and saved
traces using the archived copies. This verification passed after integration.

To repeat the public baseline from the repository root, use the pinned local SDK:

```sh
dotnet test tests/Cilica.Metadata.Tests/Cilica.Metadata.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~CompatibilityGapTests
```

For the combined integration gate, select
`FullyQualifiedName~RuntimeTypeIdentityTests|FullyQualifiedName~Embedding`.
Restore dependencies once if absent; no services or hosted jobs are required.
Next delivery slice: AC1a nullable storage and members, with AC1b boxing/conversion
semantics kept explicit and unsupported forms still diagnosed.


### AC1a nullable storage and members outcome

Implemented [ADR AC0002](../decisions/AC0002-nullable-value-storage.md): an owned
Nullable<T> presence flag and inline payload, constructor, HasValue, Value and
both GetValueOrDefault overloads. Value copies/returns and reference-slot tracing
reuse the existing compiler/runtime; no new native emitter profile, ABI or
intrinsic capability is needed. Closed generic constraints now recognize the
selected Enum ancestry as value semantics; signature and payload bounds remain.

Thirteen focused native probes pass at O0/O2 under ASan/UBSan and forced
collection, giving 3,822 repeated .NET/native comparisons plus frame and cleanup
checks. They cover signed/unsigned/narrow scalars, Boolean/Char, public Int32 enums,
Guid, application IDs, generic containers, reference-bearing payloads, return and
materialized field/array copies, default/present/empty states, fallback evaluation,
Value's exception and ordinary boxes containing nullable fields. A same-named
application value still uses ordinary boxing. Exact member binding and invalid
reference/nested-nullable arguments have metadata checks.

Review found two unsafe fallback paths after admitting nullable layout: ordinary
struct boxing would create the wrong identity, and key IsNull would treat an
empty nullable as nonnull. Both now diagnose by exact selected type identity,
with direct boxing/raw-unbox and isolated key-helper regressions. Equals/hash/text
and direct operator bodies remain explicit unsupported members. Existing static
inline-field, direct struct-return-to-heap and internal/non-Int32 enum limits stay
visible with reduced fixtures; they were not worked around in application code.
Enum diagnostics now include the offending type name for investigation.

The public baseline advances NullableInt and NullableGuid: ten native controls
(2,940 repeated O0/O2 results) and 37 rejection expectations. Generic constraints,
owned core, tuple behavior, Type identity, embedding and interface-constraint
checks passed. Initial new rejection fixtures exposed different C# IL shapes
(static-field address and internal generic access); corrected the fixtures to
isolate the observed field-storage paths and reran the affected rejection test.
Later review strengthened the Guid probe with nonzero multiword payloads and
reran the affected native bundle. The successful broader regression suite was
not repeated.

The unchanged private roots now report:

| Root | Next observed blocker | Assigned work |
| --- | --- | --- |
| Engine Step | Static inline application-ID field; CIL2001 | AC8 follow-on static value storage |
| State lookup | Internal Int32 enum rejected by the current visibility bound; CIL2001 | AC5b enum storage/access |
| Canonical hash | Unmapped SHA-256 contract; CIL3015 | AC7c hashing path |

The enum is not a width mismatch; source inspection confirmed its internal
Int32 declaration. Separate public probes preserve visibility and width limits.
No full executable engine closure or native saved-game replay is claimed.

Private evidence lives under ignored `artifacts/alchemy-nullable/`: unchanged
input assemblies, a separately built core, exact local fixture review, recorded
probe commands/results and engine Step IL. Core SHA-256:
`d1b03319aa2dc2b3aa41e235b212547075e8f8b0116c13e7d6b9a7e299a01d99`;
manifest SHA-256:
`49ce0437eb3ca413de68a7419820f099bbc7c967be2fe92c536e1db2f73c89a0`.
All 94 grant owner/member/capability identities remain unchanged; four raw
signature hashes shift with metadata type tokens. All prior core source hashes
are unchanged and only the owned Nullable source was added. This is local fixture
approval, not a production trust update. Hosted tests, services and training
remain untouched. The ongoing milestone branch was inspected, not merged into
this slice; its later numeric-record work remains independent.

Next is AC1b's nullable boxing/conversion contract; nullable text belongs to AC7b
and nullable key selection to AC3. The ledger records those residuals and the
newly exposed application blockers without changing the original audit evidence.


### AC1b.1 named-value boxing and conversions outcome

Implemented [ADR AC0003](../decisions/AC0003-nullable-boxing-and-conversions.md).
The exact selected Nullable<T> box/unbox.any operations use managed helpers and
existing boxes, roots and copy-return packets. Empty becomes null without a box;
present becomes the underlying value's box. Null unboxing gives default and
non-null unboxing requires the exact payload type. Direct implicit/explicit
operator methods now execute; representative lifted arithmetic/comparison and
checked/unchecked integer conversions have differential coverage.

Code inspection exposed a prerequisite within the original AC1b plan: primitive
and enum boxing lack nominal payload descriptors independently of Nullable.
This slice therefore admits the already supported named-struct payloads and
keeps scalar/enum boxing explicit errors. Remaining AC1b delivery is split into
nominal primitive/enum boxing and any required nullable type-test/raw-unbox forms;
forms omitted from the final replay closure need evidence of absence. Full AC1b
is not complete. Nullable key semantics still belong to AC3.

Review and a local .NET probe caught a wrong-target cast-message distinction:
nullable unbox names Nullable<T>, ordinary unbox names T. A diagnostic flag
preserves this through scalar lowering and the private exception payload without
changing box identity. The first native O0 sanitizer compile exposed a 2,112-byte
exception-message frame. Moving nonallocating resource selection to a small helper
passes the original 2,048-byte limit. Boxed IR v4, scalar boxed IR v5 and native
v38 identify these changes. Public embedding signatures and grants are unchanged.

Validation: 120 focused local tests pass. The nullable suite now has 27 probes
and 7,938 repeated .NET/native comparisons at O0/O2 with forced collection and
ASan/UBSan. New evidence covers fresh/empty boxes, underlying GetType identity,
value/reference copies, Guid, generic payloads, object arrays, exact mixed cast
messages, conversion operators and checked/lifted expressions. Separate heap
limits verify no extra allocation for empty nullable boxes, clean failure, and
instance reuse. Existing boxing, exceptions, exception embedding and the 37-gap
rejection baseline pass. Public native control counts remain the AC1a evidence;
private Alchemy probes were not rerun and full saved-game replay remains open.

Local logs and toolchain evidence are preserved under ignored
`artifacts/alchemy-nullable-boxing/validation/` in the primary checkout. No hosted
work, services or additional agents were used. Main remains at 48ecf14; the
independent milestone branch was inspected through e6dfa31 (Apple packaging) and
was not merged into this compatibility slice.


### AC1b.2 nominal scalar boxing outcome

Implemented [ADR AC0004](../decisions/AC0004-nominal-scalar-boxes.md): box-only
nominal descriptors for selected Boolean/Char, signed/unsigned 8/16/32/64-bit
integers, and existing public nongeneric Int32 enums. One runtime-owned carrier
slot reuses the normal box allocator, packets, exact roots and checked copies.
It is not a managed field or inline struct layout. Six exact small-primitive
reference mappings were added to the test binding plan; no grants or public ABI
were added. Native profile v39 and the ADR record the related IR versions.

A local .NET compatibility matrix established that ordinary unbox admits Int32
and Int32 enums interchangeably, whereas nullable unbox requires exact underlying
nominal identity. Native GetType preserves the original type. Actual-descriptor
field access avoids aliasing between C++ box structs. Narrow normalization is
verified even for authored SByte box IL without a preceding conversion.

Review identified incomplete primitive interface declarations in the bounded core.
Scalar-box closures with interface casts/tests or interface dispatch now diagnose
conservatively rather than return an incorrect type-test result. Scalar/nullable
isinst/castclass, raw scalar/nullable unbox, nonpublic/non-Int32 enums and broader
primitive APIs remain outside this slice. Next AC1b work is the required boxed
type-test/borrow/interface forms, or proof that a deferred form is absent from the
accepted replay closure. Nullable keys remain AC3; AC1b is still in progress.

Validation passed: 18 scalar probes give 5,292 repeated .NET/native comparisons,
and 31 nullable probes give 9,114, at O0/O2 under ASan/UBSan and forced collection.
Heap-budget/recovery tests remain green. 146 focused layout, core admission,
boxing, exception/embedding, rejection and public-ledger regression checks pass.
An initial direct Byte probe exposed the missing exact reference mapping; that
was added. One larger nullable compile timed out at 30 seconds; batches of at most
16 probes passed without increasing timeouts or removing coverage. Successful
native bundles were not rerun merely for profile labels or diagnostic-only gates.

Evidence is preserved under ignored `artifacts/alchemy-scalar-boxing/validation/`
in the primary checkout. No private Alchemy probes were rerun; the AC1a engine
blockers and historical audit inputs remain unchanged. No services, hosted CI,
pushes or additional agents were used. Main remains 48ecf14; the independent
milestone branch was inspected through 5fe186d (Swift ownership adapter) and was
not merged into this slice.


### AC1b.3 exact boxed type-test outcome

Implemented [ADR AC0005](../decisions/AC0005-boxed-type-tests.md): isinst and
castclass for existing scalar/enum and named-struct boxes, including Guid and
nullable targets. Nullable<T> operands validate their closed constraints and then
use T's exact descriptor. Both compiler stages check the explicit boxed target
and Object result view, including descriptors used only for tests. Success keeps
the original box and allocates nothing; failure/null behavior follows the existing
checked reference and exception paths. No core source, grant, binding or public
embedding ABI changed. Boxed IR v6, scalar boxed IR v7 and native v40 record scope.

The .NET oracle confirms exact type-test identity even where ordinary Int32/enum
unboxing allows compatible copies. Direct IL probes caught a separate fault-name
distinction: failed castclass Nullable<T> names T; failed nullable unbox.any names
Nullable<T>. The correction leaves the nullable-unbox diagnostic flag unchanged.
The regression distinguishes a caught cast failure from successful identity, so
accepting an invalid conversion cannot accidentally pass.

Validation: 29 native probes (19 C#, ten authored IL) provide 8,526 repeated
O0/O2 .NET comparisons on macOS ARM64 with ASan/UBSan and forced collection.
Coverage includes each admitted scalar width, enum mismatches, nullable null/
present forms, Guid, empty/closed/reference-bearing structs, test-only descriptors,
identity and exact faults. Metadata/IR checks reject invalid nullable arity and
constraints, forged targets/views and invalid operands. The 12-test main slice
run passed; two focused Guid native tests and 142 existing cast/box/exception/
embedding and public-ledger regression checks also passed. The unchanged
30-second native timeout and 2,048-byte sanitizer frame limit were preserved.

Review checked ownership/null order, exact versus compatible matching, rooted
references, absence of payload reinterpretation, descriptor reachability, closed
constraints and unchanged reference-type access. General filters remain diagnosed;
the attempted broader filter probe was retained as a rejection test. Raw scalar/
nullable unbox, primitive interface contracts/use proofs, nullable keys and
ValueType/Enum views remain follow-on work. AC1b is still in progress until needed
forms are implemented or proved absent from the accepted replay closure.

Ignored logs, source/assembly hashes and the local .NET identity/fault oracle are
preserved at `artifacts/alchemy-boxed-type-tests/validation/` in the primary
checkout. No private Alchemy roots were rerun, so historical replay blockers are
unchanged and full saved-game replay remains open. No hosted work, pushes, services
or additional agents were used. Main remains 48ecf14 and is contained in this
branch; the independent milestone branch was inspected through 68bae1d (UIKit
scene lifecycle) and was not merged into this compatibility slice.


### AC1b.4 confined scalar/interface composition outcome

Implemented [ADR AC0006](../decisions/AC0006-confined-scalar-box-uses.md). The
existing bounded use proof now admits scalar boxes beside independent interface
operations when every scalar box allocation is confined. Successful exact boxed
type tests are aliases, not new objects; follow them through the same proof.
Direct scalar interface operations and unknown reference escapes still diagnose.
Both value and scalar validators repeat the check. Limits, roots, emitted native
v40, core contracts/grants and public ABI remain unchanged; boxed IR v7 and scalar
boxed IR v8 record the new admission.

Thirteen native probes pass 3,822 repeated .NET comparisons at O0/O2 with forced
GC, ASan/UBSan, frame-budget and cleanup checks on macOS ARM64. They cover direct/
forwarded helpers, narrow/wide/enum copies, nullable copies and type tests,
independent interfaces, descriptor-only targets, faults and allocation while a
box is held by a helper. Nine fixtures preserve rejection of interface aliases,
returned/local/heap boxes, recursion and parameter addresses. General nullable
boxing helpers returning references still fail the proof in interface-using
closures. Detached isinst/castclass alias escapes are rejected at both stages;
their unmodified models validate. 129 existing regressions also pass.

Review checked all scalar allocation origins, closed parameter summary keys,
exact type-test aliases, escape detection, unchanged bounds and rooting. Initial
test-authoring issues were an analyzer violation and a nullable-return fixture
whose immediately present nullable was simplified by C#; the corrected fixture
requires an actual returning helper and now rejects as intended. Successful
native bundles were not repeated for that diagnostic-only fixture change.

Re-read the archived body inventory before choosing this slice: 1,825 method rows,
1,775 readable bodies, 50 declarations with no managed IL, 42 box instructions,
and no unbox or unbox.any. The incomplete application audit and unresolved library/
callback dependencies do not prove absence from the accepted replay closure.
Raw scalar/nullable unbox stays deferred; escaping boxes and required interface
contracts still need implementation or closure evidence. Full AC1b remains open.

The derived inventory summary, logs and hashes are saved under ignored
`artifacts/alchemy-confined-scalar-boxes/validation/` in the primary checkout.
No private compilation/replay root was rerun, so the recorded application blockers
are unchanged. No hosted work, services, pushes or extra agents were used. Main
remains 48ecf14 and is contained in this branch; the separate milestone lane was
inspected through 68bae1d and was not merged into this slice.


### AC2.1 exact reference delegate result outcome

Single-target application delegates can now return admitted managed references,
with exact target matching and normal rooted return storage. Object, string,
class/interface and supported vector results are retained by typed signatures and
catalog closure checks. Detached catalogs must agree, and arrays used only in a
delegate return signature survive pruning. Native dispatch uses a typed Object*
function pointer and publishes into the caller root before exception handling.
See [ADR AC0007](../decisions/AC0007-reference-delegate-results.md).

Thirteen new probes plus public RefDelegate provide 4,116 repeated .NET/native
comparisons at O0/O2 on macOS ARM64 with ASan/UBSan, forced GC and frame/heap/root
cleanup checks. They exercise fresh/shared/null results, captured cycles, faults,
reference argument identity, generic and interface results and the planned reduced
Type-to-handler dictionary. Five negative fixtures retain signature and covariance
diagnostics. Detached shape/capture/call/catalog mutations and JSON round-tripping
also pass. The ledger advances to 36 known gaps and 11 native controls.
201 existing delegate, catalog, dictionary, runtime-type and public-gap checks
pass in two targeted regression runs.

Object layout v15, delegate value/scalar IR v2 and native v41 change; core mappings
and the public embedding ABI do not. Review covered reference lifetime, exception
order, exact signatures, catalog retention and typed native dispatch. The byref
negative test was corrected for the earlier signature-discovery rejection.
AC2.2 will add only required framework delegate contracts and tested signatures.
Full AC2, remaining AC1b and private saved-game replay stay open; no private root
was rerun and no hosted work or additional agents were used.


### AC2.2 required framework delegate contract outcome

The owned core now declares Action<T>, Func<T,TResult>, Func<T1,T2,TResult>,
Predicate<T> and Comparison<T> beside Action<T1,T2>. Pinned variance and exact
constructor/Invoke mappings pass. Boolean results use ordinary normalized scalar
returns; discovery checks scalar target identities before carrier lowering.
Supported parameters remain Int32/reference, and results Int32/Boolean/reference/
void. Variance conversions, multicast and broader signatures remain diagnostic.
See [ADR AC0008](../decisions/AC0008-framework-delegate-contracts.md).

Fifteen new probes plus public FuncDelegate provide 4,704 repeated native/.NET
comparisons at O0/O2 with ASan/UBSan, forced GC and frame/root/heap checks. Coverage
includes captured callbacks, Boolean predicates, comparison delegates and a
Type-to-Func handler dictionary returning a shared object. Twelve focused tests
and 112 distinct existing regressions pass. Four authored IL mismatches prove
that bool/int and signed/unsigned scalar aliases cannot bypass target validation.

The ledger advances to 35 known gaps and 12 native controls. Where no longer fails
at Func`2; its next exact blocker is Enumerable. Composed CIL is v3, while native
v41, object layout v15 and delegate IR v2 remain unchanged. The approved core
manifest changes with the owned declarations; intrinsic grants and public ABI do
not. Review covered metadata identity, normalization, existing receiver/reference
checks and bounded claims. No private replay root was rerun.

AC2.1/AC2.2 are complete within tested bounds. Actual private closed callback
signatures and the full command closure remain for AC8. Continue with AC3
collection operations; do not treat this as full replay or LINQ support.


### AC3.1 concrete list mutation and snapshot outcome

Managed List<T>.Insert/RemoveAt/Clear/ToArray now preserve indexed ordering,
capacity, reference/value-copy behavior and snapshot identity. Removed slots are
cleared; empty snapshots use a traced per-closed-type runtime static cache distinct
from Array.Empty<T>. Existing compiler/runtime storage handles the implementation;
profiles, intrinsic grants and public ABIs remain unchanged. See
[ADR AC0009](../decisions/AC0009-list-mutation-and-snapshots.md).

Eleven new probes plus public ListRemoveAt/ListToArray provide 3,822 repeated
native/.NET comparisons at O0/O2 on macOS ARM64 with ASan/UBSan, forced collection,
frame budgets and cleanup checks. Four native GC checks prove removal of direct
and struct-contained references while lists remain rooted. 61 existing regression
checks pass. Review covered overlap direction, bounds/version order, allocation
publication, static cache identity and root release. The GC harness was corrected
to prepare exceptions before installing its manual root.

The ledger advances to 33 known gaps and 14 native controls. Two interface-mutation
fixtures expose the pre-existing array-helper discovery gate and remain exact
rejection tests. Follow-up AC3 work must address that gate, then copying, search,
bulk operations and remaining dictionary/set APIs; enumeration joins AC4. Full AC3
and private saved-game replay are not complete. No private root was rerun and no
hosted work, additional agents or services were used.


### AC3.2 array-interface mutation outcome

Five separately granted managed helpers resolve AC3.1's interface-discovery gate.
Generic/nongeneric Insert and RemoveAt preserve unconditional fixed-size faults;
generic Clear throws read-only while nongeneric Clear resets elements. Logical
and flattened value signatures are checked in interface plan v4. Existing native
operations preserve covariance, null/argument order, identity and reference roots;
native v41 and public embedding ABIs do not change. See
[ADR AC0010](../decisions/AC0010-array-interface-mutations.md).

Thirteen array probes plus the two advanced list/read-only interface probes pass
4,410 repeated O0/O2 .NET/native comparisons with forced GC and ASan/UBSan on macOS
ARM64. Four live-array GC checks, exact grant denial, detached signature checks
and 125 existing regressions pass. Review covered fault order, argument shapes,
empty values, covariance and release of nested reference slots. No native limits
or timeouts changed.

The supplemental ListInterfaceMutation blocker advances to native-validated;
the public ledger stays at 33 gaps and 14 controls. Continue AC3 with copying,
search, bulk operations and remaining dictionary/set APIs; enumeration joins AC4.
Full AC3 and private saved-game replay remain incomplete. No private root was
rerun, and no hosted work, additional agents or services were used.


### Dictionary removal and clearing (AC3.3)

Owned Dictionary Remove (with/without out value) and Clear now unlink bucket
chains, release payload roots and reuse freed entry slots before growing. Mutable
key hashes preserve the pinned receiver semantics; throwing callbacks leave out
storage and dictionary contents unchanged. Ten probes plus public DictionaryRemove
supply 3,234 repeated O0/O2 .NET/native comparisons, four live-dictionary GC checks
and 38 existing library/baseline checks. The ledger has 32 gaps and 15 controls.
No compiler/runtime profile, grant or ABI changes. Copying requires rich range
faults and precise array downcast semantics; Add/getter/constructors, other
collections and enumeration remain planned. Full replay remains incomplete.
See [ADR AC0011](../decisions/AC0011-dictionary-removal-and-clear.md).


### Integer math and typed comparisons (AC5a.1)

Owned managed Math implements signed Int32/Int64 Abs and signed/unsigned 32/64-bit
Min/Max. Typed CompareTo methods preserve exact IComparable<T> declaration flags
and use existing scalar receiver lowering. Explicit minimum-value guards preserve
Abs overflow and its message; comparisons avoid overflowing subtraction.
Eleven authored plus three public probes supply 4,116 repeated O0/O2 .NET/native
comparisons; exact overload checks and 35 existing regressions pass. The ledger
has 29 gaps and 18 controls. No profile, grant or ABI changes. Broader math,
object comparison, comparer execution, Guid helpers and full replay remain open.
See [ADR AC0012](../decisions/AC0012-integer-math-and-comparison.md).


### List and array search (AC3.4)

List Contains/IndexOf/Remove and live read-only search now use exactly selected
managed loops. Value equality borrows the stored array element, preserving its
observable mutations and roots across allocating/throwing callbacks; integer and
admitted reference search uses copied receivers. Array IndexOf/Contains/Remove
have exact grants and interface-plan-v5 packet checks. ClosedArraySearch is a
separate private grant; scalar/reference element addresses remain unsupported.
Twelve authored plus one public probe supply 3,822 repeated O0/O2 .NET/native
comparisons, two live-list GC checks and 48 existing regression checks. The ledger
has 28 gaps and 19 controls. Native v41 and public ABIs remain unchanged. Nullable/
unknown-reference equality, range/copy/bulk APIs and full replay remain unfinished.
See [ADR AC0013](../decisions/AC0013-list-and-array-search.md).


### Ordinal text comparison (AC7b.1)

String.CompareOrdinal and IsNullOrEmpty now have owned managed bodies. Comparison
preserves the pinned .NET 10.0.11 numeric result, including short-string zero
padding and odd-prefix terminators, using safe UTF-16 indexing. Four authored
probes plus the public comparison provide 7,794 repeated O0/O2 native/.NET results
with forced GC and ASan/UBSan. All 71 relevant baseline/string regressions pass.
The ledger has 27 gaps and 20 controls. No profile, grant or ABI changes; cultural,
ignore-case and other text APIs remain separate work. Private replay is unverified.
See [ADR AC0014](../decisions/AC0014-ordinal-text-comparison.md).


### Fixed-invariant numeric append (AC7b.2)

StringBuilder.Append accepts signed/unsigned 32/64-bit integers through existing
managed integer formatting and string append. This bounded core fixes numeric
text to invariant culture; it does not implement ambient CurrentCulture behavior.
The .NET oracle is explicitly pinned and restored by the tests. Six probes plus
public StringBuilderInt supply 2,058 repeated O0/O2 native/.NET comparisons, and
37 builder/baseline regressions pass. Forced GC, sanitizers, frame and cleanup
checks remain enabled. The ledger has 26 gaps and 21 controls. No new grants or
profiles; full replay remains unverified. See [ADR AC0015](../decisions/AC0015-invariant-numeric-append.md).


### Array read-only views (AC3.5)

Array.AsReadOnly now returns a live nonempty view or a shared per-closed-type empty
view. Private caches use existing instance-owned static roots. Null validation,
covariance, original-array SyncRoot and readonly mutation faults match the pinned
runtime. Six probes plus the public baseline provide 2,058 repeated O0/O2 native/
.NET results; two cache lifetime/isolation checks and 54 existing regressions pass.
Sanitizers and forced GC remain enabled. The ledger has 25 gaps and 22 controls.
No new grants or profiles; enumeration, copying and full replay remain unfinished.
See [ADR AC0016](../decisions/AC0016-array-readonly-views.md).


### Concrete list enumeration (AC4.1)

List<T>.GetEnumerator now supplies the exact nested value-type contract with
Current, MoveNext and Dispose. Copies advance independently; successful list
mutations invalidate later MoveNext calls, while capacity changes and failed
removals do not. Default/ended current values, reference/value copies and fault
messages match the pinned runtime. Twelve authored probes plus public ListForeach
supply 3,822 repeated O0/O2 native/.NET comparisons with forced GC and sanitizers.
Three reduced metadata tests fix nested generic parameter double counting; 82
existing regressions and the boxed-interface rejection pass. No new grants or
runtime profiles. The ledger has 24 gaps and 23 controls. Interface enumeration,
boxed receiver dispatch and full replay remain unfinished.
See [ADR AC0017](../decisions/AC0017-concrete-list-enumeration.md).


### Dictionary key and value views (AC4.2)

Dictionary Keys/Values now expose cached live views through concrete, generic,
read-only and nongeneric contracts. They preserve identity, Count, SyncRoot and
read-only faults. Key lookup forwards ContainsKey; ContainsValue and value-view
lookup scan live entries with copied equality receivers. Nulls and removed slots
are handled explicitly. Ten probes plus public DictionaryKeys supply 3,234 repeated
O0/O2 native/.NET comparisons; two view-cycle GC checks, exact nested metadata,
unknown-equality rejection and 41 existing regressions pass. The ledger has 23
gaps and 24 controls. No new grants or native profiles. Copying, enumeration and
full replay remain incomplete. See [ADR AC0018](../decisions/AC0018-dictionary-views.md).


### Concrete dictionary enumeration (AC4.3)

Dictionary.GetEnumerator now exposes its exact nested value-type contract. It
walks active entry slots, copies the current KeyValuePair and resets it at the end.
Only insertion of a new key increments the version: removal, clearing, overwrite
and failed TryAdd remain valid during iteration. Eleven probes plus the public
foreach baseline supply 3,528 repeated O0/O2 native/.NET comparisons, including
current-value survival across forced GC after clearing the dictionary. Forty-three
existing regressions pass with sanitizers enabled. The ledger has 22 gaps and 25
controls. No new grants/profiles; boxed interface enumeration, view enumeration
and full replay remain unfinished. See [ADR AC0019](../decisions/AC0019-concrete-dictionary-enumeration.md).


### Concrete dictionary view enumeration (AC4.4)

KeyCollection and ValueCollection now expose their exact nested enumerator types.
They retain only the selected current key or value, preserve dictionary mutation
rules, copy independently and reset current at completion. Eight probes supply
2,352 repeated O0/O2 native/.NET comparisons; four precise GC checks prove the
key/value retention difference after clear and release at iteration end. All 35
view/baseline regressions pass with sanitizers and forced GC. No new grants or
profiles. The ledger remains 22 gaps and 25 controls; interface/array enumeration
and full replay remain unfinished. See [ADR AC0020](../decisions/AC0020-concrete-view-enumeration.md).


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


### Frozen Alchemy re-probe after AC6.3

At compiler f6ba217, verified unchanged frozen inputs admit 31 of 52 probes.
RealRandom and RealGridDistance additionally pass 588 repeated O0/O2 native/.NET
comparisons with ASan/UBSan, forced GC and exact cleanup. This advances the real
grid-distance helper beyond the earlier Math gap. Evidence is local and private in
`artifacts/alchemy-closure-refresh-f6ba217`; original audit artifacts are retained.

The engine Step probe now stops at a static RejectionId value; state lookup stops
at the internal Int32 SpellPreparationPhases enum; canonical hashing still requires
SHA256. Prior public probes do not establish full application admission. Next work
prioritizes contextual enum access and static value storage, preserving the frozen
engine and recorded games. AC8/AC9 remain incomplete.


### Internal Int32 enums (AC5b.1)

Top-level internal Int32 enums now use scalar storage in admitted layouts and
same-assembly direct memory operands. Five probes pass 1,470 repeated O0/O2
native/.NET comparisons with sanitizers and forced GC. Cross-assembly operands,
nonpublic generic/box access and cross-owner internal fields remain checked.
The frozen state-lookup probe advances to a metadata-constant layout rejection;
it is not yet executable. No profiles/grants change; public counts remain 18 gaps
and 29 controls. See [ADR AC0026](../decisions/AC0026-internal-int32-enums.md).


### Metadata constant layouts (AC5b.2)

Validated primitive constant declarations no longer occupy class or struct storage.
Five probes pass 1,470 repeated O0/O2 native/.NET comparisons with sanitizers and
forced GC; 12 metadata boundaries, 97 layout/enum regressions and 21 baseline checks
pass. Direct field operations and malformed constants remain rejected. The frozen
state probe now reaches a private nested enum; replay remains incomplete. See
[ADR AC0027](../decisions/AC0027-metadata-constant-layouts.md).


### Direct string calls (AC7b.3)

Exact String.Length/Chars now accept Roslyn's direct call form as well as callvirt.
Existing declaration and intrinsic permissions remain enforced. New fault/UTF-16
probes and direct-call layout regressions pass 2,646 repeated O0/O2 comparisons;
150 string/admission checks pass with sanitizers and forced GC. No profile/grant
changes. See [ADR AC0028](../decisions/AC0028-direct-string-instance-calls.md).


### Nested enum storage (AC5b.3)

Exact nested Int32 enum payloads now use admitted layouts with separate lexical
operand access. Three probes pass 882 repeated O0/O2 native/.NET comparisons;
eight boundary checks and 30 previous enum/lexical checks pass. The frozen state
probe now reaches the unchanged 256-type catalog limit. Static value storage and
full replay remain unfinished. See [ADR AC0029](../decisions/AC0029-nested-enum-storage.md).


### Wide static storage (AC5b.4)

Int64/UInt64 static fields preserve all bits in per-instance storage. Five probes
pass 1,470 repeated native/.NET comparisons; two persistence/isolation runs, 18
static/default checks, 25 initialization/packet checks and 21 public baseline checks
pass. Native profile v42 and layout v16 account for the wider private slot. Inline
static structs and full replay remain unfinished. See
[ADR AC0030](../decisions/AC0030-wide-static-storage.md).


### Static inline-value copies (AC5b.5)

Whole static struct loads/stores now expand through validated value layouts into
per-instance scalar and traced reference slots. Eight probes pass 2,352 repeated
O0/O2 native/.NET comparisons; four GC scenarios, eight packet mutations, the
address boundary, 90 existing regressions and 21 baseline checks pass. Layout v17,
native v42. Frozen EngineStep advances to a nonpublic generic argument; state
lookup still reaches the type limit. Static addresses and full replay remain
unfinished. See [ADR AC0031](../decisions/AC0031-static-inline-value-copies.md).


### Private generic reference transport (AC8.2)

Original explicit type names are checked in their declaration scope before closed
substitution. Legal private reference arguments can pass through opaque generic
calls, locals, fields and delegates; foreign explicit names and member access stay
checked. Four probes pass 1,176 repeated O0/O2 native comparisons with forced GC
and sanitizers; 106 broad and 60 focused checks pass. Private T[] still requires
additional detached access evidence. Frozen EngineStep advances to String.Format;
StateLookup remains at the unchanged type limit. Full replay remains incomplete.


### Private generic arrays (AC8.4)

Exact original T array uses now carry instruction-scoped context evidence through
logical and scalar IR. Both stages recompute specialization identities and check
the variable, element, offset and operation. Six probes pass 1,764 repeated O0/O2
native comparisons with sanitizers and forced GC, including List<private type>.
Eleven mutations fail in both stages; array permission does not authorize casts
or explicit foreign names. 117 broad and 46 post-review checks pass. Object IR v16
and integer-object CIL v4; native v42/layout v17 unchanged. Engine/state closure
and full replay remain incomplete. See ADR AC0035.


### Closure failure evidence (AC8.5)

Nested resolution now preserves inner IL locations and requesting calls. Type-limit
errors report bounded partial identity counts. 85 focused checks and 21 baseline
checks pass; limits are unchanged. Engine formatting is specifically the two-object
String.Format call in GameState.GetPlayer IL_0020. State discovery reaches 257
identities (143 distinct nominal declarations, 122 closed generic identities), not
a measured final runtime catalog. Capacity and full replay remain unresolved.
See ADR AC0036 and artifacts/alchemy-closure-diagnostics.


### Two-box composite formatting (AC7b.4)

The exact two-object String.Format overload now requires a direct same-block box
witness for each named ordinary value. Both overloads share the pinned parser;
repeated callbacks retain each boxed copy. Five new and five previous probes pass
2,940 repeated O0/O2 comparisons with sanitizers and forced GC; 71 formatting,
admission and baseline checks pass. Scalar/enum/provider formatting and aliases
remain diagnostic. Frozen GetPlayer advances to unmapped KeyNotFoundException;
full engine and replay execution remain unverified. See ADR AC0037.

### Selected KeyNotFoundException (AC8.6)

The owned core admits exact granted default and string-message constructors for
System.Collections.Generic.KeyNotFoundException. Default and null messages use the
pinned Arg_KeyNotFound resource; empty and arbitrary UTF-16 messages are preserved.
Typed catches, rethrow, finally and exception identity reuse existing rooted payloads.
Inner constructors, HResult and serialization remain unsupported. Managed exception
IR and scalar exception IR advance to v4; native v42/layout v17 remain unchanged.
Four probes pass 1,176 repeated O0/O2 native/.NET comparisons with ASan/UBSan and
forced collection. Forged resource/name/ancestry packets fail at both IR stages;
removing any exact layout/constructor grant fails admission. See ADR AC0038.
Frozen EngineStep advances to List<DomainEvent>(IEnumerable<DomainEvent>) in
FinalizeAccepted IL_0005. StateLookup still hits the unchanged 256-type bound.
Full engine execution and saved-game replay remain unverified.

### Int32 actual-value range messages (AC8.7)

Selected ArgumentOutOfRangeException(String,Object,String) construction admits
null or a directly boxed selected Int32 in the same block. Both IR stages verify
the exact box and exception identity. The exception retains the original box with
precise tracing; Message appends the pinned actual-value text after its parameter
text, including Int32.MinValue. The intermediate message is rooted across allocation.
Aliases, other payloads and ActualValue retrieval remain diagnostic. This is a
prerequisite for collection-copy range checks, not general actual-value dispatch.
Native profile is v43 and both managed-exception IR stages are v5; layout v17 stays.
Four probes pass 1,176 repeated O0/O2 native/.NET comparisons; additional O0/O2
native checks cover retained-box collection, intermediate allocation failure,
origin preservation and corrupted roots. Existing confinement and public baseline
checks pass. See ADR AC0039. Full engine/state/replay execution remains unverified.

### Direct list copies (AC3.6)

List<T>.CopyTo(T[]) and CopyTo(T[],Int32) support admitted integer, reference and
inline-value elements. Source fields are read before destination checks; validation
preserves null/range/space order, exact parameter messages and unchanged list version.
A separately granted private CopyStore operation preserves actual reference cast
names and completed writes when a covariant destination fails. Ordinary array stores
retain ArrayTypeMismatchException. Native v44, object operations IR v17 and
integer-object CIL v5 record the new operation; exception IR v5/layout v17 remain.
Six probes plus one raw null-receiver probe pass 2,058 repeated O0/O2 native/oracle
or explicit CIL-fault comparisons with ASan/UBSan and forced collection. Exact
grant/private-caller checks and four packet mutations in both IR stages pass;
47 selected copy/array-mutation/baseline checks pass. See ADR AC0040.
Interface CopyTo still requires array dispatch support; section/nongeneric copies
and List(IEnumerable) remain unsupported. Full engine/state/replay is unverified.

### Generic collection copies (AC3.7)

ICollection<T>.CopyTo now supports admitted arrays and lists. A shared managed
prefix helper preserves argument validation, whole-array compatibility, partial
writes and exact cast messages. Unrelated class arrays fail even when empty;
related or interface element types permit checked element copies. A private exact
CopyCompatible grant uses a bounded static descriptor table without allocation.
Both detached stages and array helper signatures remain validated. Native v45,
object IR v18, integer-object CIL v6 and interface plan v7; layout v17 unchanged.
Nine new probes and prior direct-list/null-receiver probes pass 4,704 repeated
O0/O2 native/.NET comparisons with ASan/UBSan, forced collection and strict cleanup.
58 adjacent regression checks and the final empty-list pair pass. General
Array.Copy, section/nongeneric copying and List(IEnumerable) remain unsupported;
full Alchemy engine/state/replay remains unverified. See ADR AC0041.

### Proven collection construction (AC3.8)

List<T>(IEnumerable<T>) supports sources statically proven to implement the exact
ICollection<T> contract, including admitted arrays/lists and custom collections.
An independently granted managed bridge preserves allocation-before-validation,
Count once, zero-count CopyTo suppression, exact capacity, copied identity and
version zero. Its narrower typed parameter is checked during translation and both
detached stages. IEnumerable/IReadOnlyList aliases and general iterator sources
remain diagnostic. Integer-object CIL v7; native v45 and layout v17 unchanged.
Eight authored probes plus the advanced public control pass 2,646 repeated O0/O2
native/.NET comparisons with ASan/UBSan and forced collection. The baseline now has
30 native controls and 17 known gaps. Frozen engine discovery reaches missing
Enumerable.ToArray; state discovery still exceeds 256 identities. Full replay is
unverified. See ADR AC0042 and artifacts/alchemy-list-construction.

### Array materialization (AC6.4)

Enumerable.ToArray supports admitted array/collection copies and reference-enumerator
fallbacks. Where's exact query path preserves cursor/current and first-enumerator
identity while traversing the source. Managed traced buffers replace private pooled
scratch storage, retaining callback/disposal order and canonical empty arrays.
Twelve authored probes pass 3,528 O0/O2 native/.NET comparisons with ASan/UBSan,
forced collection and strict cleanup; 29 adjacent query/boundary checks pass.
Compiler/native profiles and grants are unchanged. Boxed collection factories,
including List in the conservative enumerable closure, remain diagnostic; direct
List.ToArray remains supported. Frozen engine discovery next requires HashSet<T>;
state still exceeds its identity quota. Full replay is unverified. See ADR AC0043.

### Default HashSet operations (AC3.9)

HashSet<T> now supports default construction, Add/Contains/Remove/Clear/Count and
TryGetValue for admitted integer/string/inline equality keys, with generic set and
collection mutation dispatch. Traced inline entries retain null handling, prime
buckets, collisions, free slots and callback ordering. A private exact reference-
shape grant folds the validated layout so removal releases references while
preserving freed scalar values observable through reentrant equality callbacks.
Integer-object CIL v8; native v45/layout v17 unchanged. Nine authored probes plus
the public control pass 2,940 O0/O2 comparisons, two native GC checks pass and 42
adjacent checks pass. The baseline has 31 controls and 16 gaps. Comparers,
enumeration, copying, capacity/enumerable constructors and set algebra remain
unsupported. Frozen engine discovery next needs String.Concat(string[]); state
still exceeds its identity quota. Full replay is unverified. See ADR AC0044.

### String-array concatenation (AC7b.5)

String.Concat(string[]) preserves null faults, canonical empty strings, single-
element identity, fresh multi-element results and exact UTF-16. Managed length
checking and copying reuse the existing private UTF-16 construction grant, with an
extra bounded temporary vector. Six ordinary probes and a bounded oversized-total
probe pass 1,770 repeated O0/O2 native/.NET comparisons with ASan/UBSan and forced
collection. 34 adjacent checks and the corrected lexical-boundary assertion pass.
Profiles/grants are unchanged. Object-array/enumerable and three/four-string
concatenation remain unsupported; concurrent mutation is not validated. Frozen
engine discovery next needs predicate FirstOrDefault; state remains over its type
quota. Full replay is unverified. See ADR AC0045.

### Predicate first-element queries (AC6.5)

Predicate First/FirstOrDefault share managed search logic with source-before-predicate
null checks, loaded-element return, array traversal and reference-enumerator cleanup.
An internal Int32 found flag preserves matching zero/null without requiring narrow
by-reference support. Eleven probes pass 3,234 repeated O0/O2 native/.NET comparisons
with ASan/UBSan, forced collection and strict cleanup; 30 adjacent LINQ tests pass.
Nonpredicate overloads, boxed List factories and value-payload predicates remain
diagnostic. Profiles and grants are unchanged. Frozen engine discovery next reaches
Func<GameState,PlayerId,bool> at PrepareForfeit IL_0395; state still exceeds its type
quota. Discovery progress is not engine execution or saved-game replay. See ADR AC0046.

### Inline-value delegate arguments (AC2.3)

Single-target delegates now accept admitted named inline structs by value, including
nested/empty values, Int64 fields and traced references. Logical signatures retain
exact nominal types; scalar lowering expands both arguments and declarations through
one layout plan. Detached scalar validation checks the preserved logical catalog.
Existing parameter/root/frame quotas stay fixed. Fifteen probes pass 4,410 repeated
O0/O2 native/.NET comparisons under ASan/UBSan and forced collection; compiler-cached
predicate objects use the existing stable persistent-root checks. 76 adjacent and
boundary checks pass, including malformed packets and expanded signature limits.
Delegate IR/scalar profiles are v3, integer-object CIL v9, object IR v19, object
layout v18 and native v46; physical delegate storage is unchanged. Direct wide/narrow
or enum parameters, struct results, byrefs and multicast remain unsupported. Frozen
engine discovery next rejects internal IEffectScope metadata while resolving
GameStateScope.set_CurrentRound; state still exceeds 256 identities. Full replay
remains unfinished. See ADR AC0047.

### Top-level internal interface dispatch (AC8.8)

The explicit closed/integer-object profiles admit top-level internal abstract
interfaces with public instance members. Catalog/declaration visibility stays
consistent; calls and type tests require the caller's image to match an internal
contract at both logical and scalar stages. Public contracts retain external use.
Seven probes pass 2,058 O0/O2 native/.NET comparisons, including explicit/inherited
mappings, class slots, generic value payloads, nulls and interface casts/arrays.
119 selected checks pass initially; the corrected detached assertion and foreign
CIL check pass separately. Exact metadata work exhaustion and the old public-only
resolver boundary remain tested. Profiles advance to object layout v19, object IR
v20, interface plan v8, integer-object CIL v10 and native v47; physical storage is
unchanged. Closed interface/class/constrained metadata targets are v2. Nonpublic
nested contracts, friend assemblies, default/static interface bodies and cross-image
internal calls remain unsupported. Frozen engine discovery next needs protected
DomainEvent base construction; state remains over its type quota. See ADR AC0048.

### Protected immediate-base construction (AC8.9)

Protected constructors are admitted only through the existing immediate-base
initialization path on the original this receiver. Family/FamORAssem follow the
validated base relation; FamANDAssem also requires the same image. Owner visibility,
exact signatures, initialization ordering and ordinary allocation access remain
checked. Six probes pass 1,764 repeated O0/O2 native/.NET comparisons with ASan/UBSan,
forced collection and exact cleanup. 29 adjacent/forged-IR checks and six corrected
cross-image access checks pass. General protected members and constructor chaining
remain unsupported. Integer-object CIL v11, object IR v21 and native v48; layout v19
is unchanged. Frozen engine discovery next needs Select in ReleaseFinished, whose
selector returns SpellId; state still exceeds its identity quota. See ADR AC0049.


### Inline-value delegate results (AC2.4)

Single-target delegates can return admitted named inline values, including nested,
empty, nullable and reference-bearing structs. The logical signature retains the
exact value identity. Lowering appends writable caller-owned result slots and uses
the existing struct-return path; reference leaves stay rooted across the call and
no callee-frame address escapes. Empty results still invoke the target and preserve
null/fault behavior. Expanded arguments and results share the existing budget.

Eleven probes pass 3,234 repeated native/.NET comparisons at O0/O2 with forced GC,
ASan/UBSan and exact cleanup. Focused malformed-IR checks reject invalid nominal
results, hidden slots and fault exits; the public baseline remains 31 native
controls and 16 gaps. Native v49, object IR v22, object layout v20, integer-object
CIL v12 and both delegate profiles v4 record the new calling convention. Direct
wide/narrow/enum results, byrefs and multicast remain outside this slice. No core
mapping or intrinsic grant changes. Frozen engine discovery still needs Select
(and then Distinct); full saved-game execution remains unverified. See ADR AC0050.


### Deferred Select and terminal traversal (AC6.6)

Non-indexed Enumerable.Select now supports admitted arrays, reference enumerables,
custom IList sources and inline-value delegate arguments/results. Selection stays
lazy, reuses the first same-thread iterator, clones later traversals, retains
Current until replacement/disposal and preserves null/fault ordering. Owned
Where/Select traversal helpers preserve earlier query cursors without generic
virtual calls. Chained Select keeps per-item callback order. Array/IList Any skips
selectors; Count runs them. IList Count/ToArray snapshot Count once and use its
indexer, while ordinary iteration uses GetEnumerator.

Eighteen probes pass 5,292 repeated native/.NET comparisons at O0/O2 with forced GC,
ASan/UBSan and exact cleanup; two unsupported-shape checks and 42 adjacent/core/
public-baseline checks pass. No compiler profile or intrinsic grants change.
Boxed collection factories, indexed Select and nongeneric Current remain outside
scope. Frozen engine discovery advances to Distinct at ReleaseFinished IL_004e;
state remains 257/256 identities. Complete replay remains unverified. See ADR AC0051.


### Default-comparer Distinct (AC6.7)

Enumerable.Distinct now preserves lazy acquisition, first-iterator reuse/cloning,
cached Current, insertion/fault order and cleanup for admitted sources. Any uses
first-element query paths without hashing. Count/ToArray build independent sets;
materialization copies stored values, including mutable hash results. Public
HashSet and private query sets share one non-enumerable SetStorage helper, keeping
unsupported collection factories out of closures that only need internal storage.
The public wrapper owns one additional managed storage object.

Eighteen probes pass 5,292 repeated O0/O2 native/.NET comparisons with forced GC,
ASan/UBSan and exact cleanup, including the value Where/Select/Distinct/ToArray
pipeline. The composed run passes 59 tests; all 11 existing HashSet checks also
pass after the extraction. Explicit comparers, DistinctBy, boxed collection
factories and HashSet enumeration remain diagnostic. No compiler profile, grant
or limit changes. Frozen engine discovery now needs OfType in ZombieSiege.
AdvanceRound IL_0009; state still reaches 257/256 identities. Full replay remains
unverified. See ADR AC0052.


### Scalar boxes beside typed interface operations (AC1b.5)

Scalar-box confinement is now required when a closure converts an unconstrained
reference to an interface. Already interface-typed receivers rely on ordinary IR
assignment/cast validation, so unrelated scalar boxes may be returned, stored in
locals/arrays/fields, forwarded recursively or passed through managed locations.
Object-to-interface tests/casts still require the existing confinement proof;
primitive interface contracts and boxed dispatch have not been added.

Twenty-one probes pass 6,174 repeated O0/O2 native/.NET comparisons with forced GC,
ASan/UBSan and exact cleanup. Logical/scalar checks reject escaping test aliases
when an unconstrained interface conversion exists and reject forged interface
parameter types. The proof uses block-local value identities. Boxed value IR v8
and scalar boxed IR v9 record the admission change; native v49 is unchanged.
See ADR AC0053. This prerequisite enables nongeneric cursor boxing; full replay
remains unverified.


### Nongeneric vector cursors and OfType (AC4.7 / AC6.8)

Bounded vectors now expose nongeneric IEnumerator through an exact managed helper
contract. Current preserves reference identity and freshly boxes scalar/inline/
nullable values, with exact before-start/after-end errors and Reset behavior.
Generic array and owned reference-class query cursors also support nongeneric
Current; boxed List/Dictionary/HashSet factories remain unsupported.

OfType filters lazily over admitted nongeneric sources, excludes nulls, preserves
cursor reuse/cloning and conditionally disposes the source. Count/ToArray/Any
preserve the original query cursor. Non-nullable already-typed sequences retain
the upstream identity shortcut. A private exact type-kind grant distinguishes
reference and value/nullable projection paths. Reference-result OfType.Select
remains diagnostic at the existing variance-only dispatch boundary; value/nullable
projection is tested. No general Type reflection or variance support is claimed.

Fourteen OfType and twelve cursor probes pass 7,644 repeated native/.NET comparisons
at O0/O2 with forced GC, ASan/UBSan and exact cleanup. Grant denial, lexical access,
logical/scalar dispatch packet checks, adjacent boxing and the public baseline pass.
Integer-object CIL is v13 and interface plan v9; native v49/layout v20 are unchanged.
Two exact grants are added. Frozen engine discovery advances to predicate Single
in ZombieSiege.AdvanceRound IL_0063; state remains 257/256 identities. See ADR AC0054.


### Predicate Single and SingleOrDefault (AC6.9)

The two-argument predicate overloads now preserve eager null checks, first loaded
match identity/copies, remainder scanning and exact no/multiple-match messages.
Arrays use direct indexing; supported reference cursors retain using disposal,
including disposal faults that replace earlier results or exceptions. Named inline
and nullable results use existing rooted copy/return lowering. Parameterless and
explicit-default overloads, List span access and boxed factories remain diagnostic.

Sixteen probes pass 4,704 repeated native/.NET comparisons at O0/O2 with forced GC,
ASan/UBSan and exact cleanup. All 43 selected Single, adjacent First and public
baseline tests pass. No compiler profile, intrinsic grant or resource limit changes.
Frozen EngineStep advances to a metadata-classification limit in FinalizeAccepted
IL_0165; StateLookup remains at 257/256 discovered identities. These are partial
closure failures, not measured emitted layouts. Full replay remains unverified.
See ADR AC0055 and artifacts/alchemy-single.


### Type-classification budget evidence (AC8.10)

Snapshot-limit diagnostics now distinguish cached declarations/specializations,
field reads, remaining metadata work, explicit roots and validated Int32 enums.
Samples and per-image distributions are bounded; no limits or admission rules
change. Thirty-six focused metadata checks pass. Frozen EngineStep reaches 256
snapshots with 27,208 metadata work units left, 23 value/176 reference/5 vector
roots and 65 snapshots outside explicit value/reference roots. These are partial
counts, not emitted layouts. StateLookup remains at 257/256 discovered identities.
Next separate lexical visibility reads from structural layout reads; do not infer
capacity from either failure. See ADR AC0056 and artifacts/alchemy-type-classification.


### Declaration-only visibility reads (AC8.11)

Lexical source-type checks now read declaration nesting without loading fields,
interfaces or initializers. A shared owner-chain reader preserves structural and
lexical visibility facts. The visibility cache retains at most 256 entries;
uncached declarations charge the existing metadata ledger. Actual enum/layout
validation and all type, field, method and metadata limits remain unchanged.

Twenty-nine focused checks pass, including 294 O0/O2 native comparisons across
257 otherwise-unused generic identities; 70 existing access, array-evidence, enum,
core and public-baseline checks also pass. Frozen EngineStep gets beyond the
snapshot limit and now requires IOrderedEnumerable for OrderBy<Int32> in
ReconcileControlOpportunity IL_0079, followed by ordinal-string ThenBy and Concat.
StateLookup still reaches its separate catalog bound. Full replay is unverified.
See ADR AC0057 and artifacts/alchemy-lexical-reads.


### Abstract class slot metadata (AC8.12)

Closed class/interface target resolution now distinguishes a bodyless abstract
slot from its concrete implementation. Abstract declarations must belong to an
abstract owner, use ordinary managed IL flags, have no body and not be final.
Concrete receivers must resolve to a real override; re-abstraction and hidden
new slots preserve their original slot identities. The legacy resolver remains
restricted. Sixty-one focused metadata checks pass. Closed class and interface
target profiles advance to v3. This is metadata foundation only: executable
abstract class calls still require a bodyless IR declaration path. No placeholder
implementation is emitted. Full replay remains unverified. See ADR AC0058.

### First-element terminals and captured lists (AC6.15)

Non-predicate First and FirstOrDefault now reuse owned query dispatch, one IList
Count/index read or a single enumerator step with disposal. First preserves the
empty-versus-default distinction. Review also reproduced and fixed predicate and
filtered-list First mutation behavior: capture original backing storage/count and
return the element loaded before the callback. A private first-element traversal
mode carries that behavior through projections without consuming existing cursors.

Sixteen probes supply 9,792 O0/O2 native/.NET comparisons with forced GC and
sanitizers. All 45 selected checks pass, including predicate/distinct regressions
and the unchanged 42-control/five-gap baseline. Grants, compiler/native profiles,
runtime layout and production bounds are unchanged. Full frozen-engine execution
and saved-game replay remain unfinished. See [ADR AC0101](../decisions/AC0101-direct-first.md).

### HashSet cursors and enumerable construction (AC4.9 / AC3.15)

HashSet now supplies mapped value cursors and boxed interface cursors, with exact
version/current/reset behavior and the shared empty cursor. Enumerable construction
validates collection, reads ICollection Count once and enumerates with disposal.
Existing-set sources preserve the dense clone versus sparse reinsertion threshold;
duplicate-heavy construction trims stored hashes without callbacks.

Seventeen probes provide 7,956 O0/O2 native/.NET comparisons with forced GC and
sanitizers. All 38 final selected checks pass, including set lifetime/grant tests
and the unchanged 42-control/five-gap baseline. Int64 enumerable construction still
reaches the unsupported wide vector needed by its empty cursor; an explicit negative
probe pins that dependency. Wide fields in admitted value keys pass. Custom
comparers, capacity constructors, public copying, set algebra and serialization
remain unimplemented. Compiler/native profiles, runtime layout and resource limits
are unchanged; full engine/replay remains unfinished. See [ADR AC0102](../decisions/AC0102-set-enumeration.md)
and [ADR AC0103](../decisions/AC0103-enumerable-set-construction.md).

### Int64 vectors and array helpers (AC7a.2)

Int64 vectors now have a distinct type identity and use the existing UInt64 array
buffer, with one 64-bit slot per element. Discovery, signatures, generic CIL,
logical/scalar IR and native loads/stores preserve all bits and checked fault order.
Exact array helpers support indexed access, search/mutation faults, copying and
enumeration; runtime type/cast text and empty caches distinguish System.Int64[].
List<long> and enumerable HashSet<long> now have native differential evidence.

Thirteen probes supply 4,326 O0/O2 native/.NET comparisons. All 105 selected checks
pass, including malformed IR at both stages, descriptor/quota/recovery tests,
existing array suites and the 42-control/five-gap baseline. Scalar unsigned views
preserve bits; explicit UInt64 vectors and unsigned enumeration/copy recipes remain
diagnostic, as do primitive element borrows. Integer CIL v33, object layout v23,
object IR v34, interface plan v13 and native v61 record this extension. Resource
limits and owned library bodies are unchanged. Full engine/replay remains
unfinished. See [ADR AC0104](../decisions/AC0104-int64-vectors.md).

### Constant array data (AC7a.3)

The integer CIL v34 profile accepts the uninterrupted constant/newarr/dup/field-token/
InitializeArray pattern for Byte, Char, Int32 and Int64 vectors: 1–256 elements,
at most 2,048 file-backed data bytes. The exact selected helper requires a manifest
grant; the metadata reader checks readonly RVA storage, size, visibility and layout.
Little-endian data becomes existing checked array stores. General runtime field
handles, other patterns and other element types remain diagnostic.

Eight probes supply 2,352 repeated O0/O2 native/.NET comparisons with forced GC and
ASan/UBSan. Metadata/control-flow/grant negatives and adjacent core/type-identity
checks pass. The public baseline advances to 43 native controls/four gaps. Native
v61, object layout v23 and production resource limits are unchanged. Full engine
execution and saved-game replay remain unfinished. See [ADR AC0106](../decisions/AC0106-constant-array-data.md).

### Dictionary capacity construction (AC3.16)

Dictionary(int capacity) now validates negative input, leaves zero capacity lazy,
and allocates positive capacities using the existing prime table. Constructor and
first insertion share initialization that publishes fields after both arrays are
allocated. Default equality/hash and other dictionary behavior are unchanged.

Six probes supply 3,276 repeated O0/O2 native/.NET comparisons. Ten native quota
failure/recovery cycles check released roots and reusable runtime state. All 39
selected checks pass, including dictionary/view/error-message regressions and the
43-control/four-gap public baseline. Compiler/native profiles and production limits
are unchanged. Comparer and other constructors remain diagnostic; full engine and
saved-game replay remain unfinished. See [ADR AC0107](../decisions/AC0107-dictionary-capacity.md).

### List predicate existence (AC3.17)

List.Exists now uses FindIndex(match) != -1, matching the pinned source. Four
probes provide 2,160 repeated O0/O2 native/.NET comparisons covering null/empty
behavior, short-circuiting, faults and predicate mutation of scalar, reference and
reference-bearing value lists. All 25 selected checks pass, including FindIndex
regressions and the unchanged 43-control/four-gap public baseline. Forced GC and
sanitizers remain enabled. Profiles and resource limits are unchanged; full engine
and saved-game replay remain unfinished. See [ADR AC0108](../decisions/AC0108-list-exists.md).

### Scoped scalar/reference ref locals (AC5b.10)

The integer CIL profile now admits Int32, Int64 and reference borrows stored in
method locals with one assignment site that dominates every read. The bounded
proof runs at both IR stages. Each local publishes its heap-owner root and retains
existing frame-token lifetime checks. Ref returns, heap-stored refs, stack joins,
inline-value ref locals and ref locals in methods with exception regions remain
unsupported. Reference-array slot borrows are the next prerequisite.

Seven probes supply 2,058 repeated O0/O2 native/.NET comparisons. All 98 selected
checks pass, including five malformed-IR cases at both stages, source boundaries,
root plans, native carrier/lifetime regressions and the 43-control/four-gap baseline.
Integer CIL v35, managed-location IR v2, scalar-location IR v3, object IR v35 and
native v62 record this extension. Layout, library and production limits are
unchanged. Full engine/replay remains unfinished. See [ADR AC0109](../decisions/AC0109-scoped-ref-locals.md).


### AC8.40: BeforeFieldInit struct storage (2026-10-07)

Closed structs with BeforeFieldInit initializers now share the existing lazy
static-storage path. Static fields are excluded from inline layouts, including
otherwise empty structs and static copies of the declaring value. Generic state,
reference roots, reentrant zero reads and cached failures retain the existing
runtime behavior. Strict/module initialization and static addresses remain
unsupported. Integer CIL v63; IR/native profiles and budgets are unchanged.
Six probes provide 1,764 O0/O2 native/.NET comparisons, with forced collection,
ASan/UBSan, plus runtime isolation/root-release checks. All 88 selected
struct/static/layout checks pass. See [ADR AC0146](../decisions/AC0146-beforefieldinit-structs.md).
The frozen engine previously reached EffectTarget layout after 372,047 metadata
units; complete engine/fingerprint execution and saved-game replay remain open.


### AC7a.6: Int32 enum array interfaces (2026-10-07)

Validated enum identities now survive closed array-interface metadata. Generic
helpers retain the requested element type; nongeneric enumeration retains the
actual array element type when boxing. Compatible integer/enum views share
four-byte storage, including CopyTo, while runtime type identity remains distinct.
Unused vector pruning also removes corresponding dispatch facts. Strict catalog,
grants, rooting and scalar-box confinement checks remain in force.
Twelve new probes and three formerly negative controls provide 4,410 repeated
O0/O2 native/.NET comparisons with ASan/UBSan and forced collection. The 100
selected array/copy/enumeration/confinement checks pass. Integer CIL v64, object
IR v42, layout v26, interface plan v14 and native v68 record the extension;
core and budgets are unchanged. See [ADR AC0147](../decisions/AC0147-int32-enum-array-interfaces.md).
The frozen engine previously reached IEnumerable<PriorityWindowTiming> after
386,000 metadata units; full engine/fingerprint execution and replay remain open.


### AC8.41: BeforeFieldInit interface targets (2026-10-07)

Closed interface planning now admits BeforeFieldInit owners, matching ordinary
method and layout admission. Existing static-field access performs initialization;
interface dispatch adds no new trigger. Strict/module initialization and the
older non-closed profile remain diagnostic. Six probes supply 1,764 repeated
O0/O2 native/.NET comparisons with forced GC and ASan/UBSan; 90 focused checks pass.
Integer CIL v65; IR/native profiles, core and budgets are unchanged. See
[ADR AC0148](../decisions/AC0148-initialized-interface-targets.md).
The frozen engine previously reached SpellResolutionHandler.get_Id after 440,238
metadata units. Full engine/fingerprint execution and saved-game replay remain open.


### AC8.42: Single-candidate interface variance dispatch (2026-10-07)

Reference receivers can now dispatch through one compatible declared generic
interface when no exact contract is present. Exact matches take precedence;
multiple candidates, generic variant methods and value/byref adaptation remain
diagnostic. Selected declaration identity and compatible argument/result types
are retained and validated before and after lowering. Seven new probes and one
promoted control provide 2,352 O0/O2 native/.NET comparisons with forced GC and
ASan/UBSan. The 152 distinct focused checks pass. Integer CIL v66, object IR v43,
interface plan v15 and native v69 record this extension; core and budgets are
unchanged. See [ADR AC0149](../decisions/AC0149-variant-interface-dispatch.md).
The frozen engine previously reached an OfTypeIterator<RoundStartedEvent> through
IEnumerable<DomainEvent> after 466,593 metadata units. Full engine/fingerprint
execution and saved-game replay remain open.


### AC8.43: Reuse type relationships within interface planning (2026-10-07)

Frozen closure discovery cleared variance and exhausted 524,288 metadata units.
Two copied 786,432-unit probes hit their unchanged 60-second cap. A 600,000-unit
phase audit isolated repeated assignment-graph construction inside interface
planning: the first 2,997 method reads took 1.3 seconds, followed by about 40
seconds in the planner before the budget diagnostic.

Reuse assignment rules within one immutable planning/validation pass, with fresh
rules for each new catalog. All 152 focused interface checks pass. The same frozen
600,000-unit probe drops from 41,913 ms to 2,607 ms with identical diagnostics and
all metadata-work buckets. This is one measured compiler probe, not runtime or
whole-application throughput. Production limits, profiles and managed semantics
are unchanged. See [ADR AC0150](../decisions/AC0150-interface-planning-reuse.md).
The 786,432-unit copied diagnostic budget remains subject to the 60-second cap;
full engine/fingerprint execution and saved-game replay remain unverified.


### AC8.44: Interface and declaration signature context (2026-10-07)

Bound interface packets no longer borrow the entry method's lexical access
context. Method parameter/return/local storage uses the declaring method's own
closed context. Original source declarations and operand tokens still enforce
lexical visibility, and selected helpers still require exact intrinsic grants.
Four two-assembly probes provide 1,176 repeated O0/O2 native/.NET comparisons
with forced GC and ASan/UBSan; all 201 focused access/value/interface checks pass.
Integer CIL v67 records admission; IR/native profiles and budgets are unchanged.
See [ADR AC0151](../decisions/AC0151-interface-signature-context.md).
The frozen engine previously reached hidden WaveKind inside a generic array
helper signature after 776,942 metadata units. Full engine/fingerprint execution
and saved-game replay remain open.


### AC8.45: Retain delegate metadata for vector helpers (2026-10-07)

Validated delegate declarations now retain their exact constructed metadata
identity for selected array-interface helper binding. Delegate shape validation,
visibility, source access and helper grants are unchanged. Four probes cover
nongeneric Count, indexed private-delegate reads, closed Func copies and
nongeneric Clear, with captured target lifetime checks. They provide 1,176
repeated O0/O2 native/.NET comparisons with forced GC and ASan/UBSan; all 64
focused checks pass. Integer CIL v68 records admission; IR/native profiles and
budgets are unchanged. See [ADR AC0152](../decisions/AC0152-delegate-vector-metadata.md).
The frozen engine previously reached a CommandHandler[] array helper after
780,053 metadata units. Full engine/fingerprint execution and replay remain open.


### AC7a.7: Byte/character array interface metadata (2026-10-07)

Byte and character arrays now support interface Count, collection flags and
SyncRoot using their exact existing array identity. Selected Count/False/True/
Identity helpers never read or write elements. Indexed operations, copies,
enumeration, clearing and signed generic views remain diagnostic. Detached plans
check the same exact widths and operation boundary. Five probes provide 1,470
repeated O0/O2 native/.NET comparisons with forced GC and ASan/UBSan; all 113
focused checks pass. Integer CIL v69, object IR v44, interface plan v16 and native
v70 record the extension; core and budgets are unchanged. See
[ADR AC0153](../decisions/AC0153-narrow-array-interface-metadata.md).
The frozen engine previously reached narrow array Count after 782,813 metadata
units. Full engine/fingerprint execution and saved-game replay remain open.


### AC8.46: Diagnostic metadata and dispatch-method capacity (2026-10-07)

After AC7a.7, the copied 786,432-unit metadata budget exhausts with 786,428 units
charged. Raising only copied metadata to 1,048,576 reaches the method limit after
817,418 units. A counter-only audit measures 4,246/4,096 combined method identities:
2,997 bodies, four abstract methods, 273 interface declarations, 1,017 mapping facts
and 38 exception declarations (categories overlap). Other counters remain within
their diagnostic limits: 1,207 type roots, 72,751 instructions, 5,870 parameters and
3,239 locals.

A copied 6,144-method allowance then reaches HashSet<string>(IEnumerable<string>,
StringComparer.Ordinal) in HandLimitHandler.Resolve after 820,241 metadata units
in 3,477 ms. StateLookup remains 15 methods/11,511 units. Production bounds and
60-second probe caps are unchanged; no native engine emission/execution occurred.
See [ADR AC0154](../decisions/AC0154-dispatch-capacity-audit.md). Full engine/fingerprint
execution and saved-game replay remain open.


### AC3.27: Source collection plus proven ordinal HashSet construction (2026-10-07)

HashSet<string>(IEnumerable<string>, comparer) now admits a directly proven
StringComparer.Ordinal getter through its exact selected constructor/factory and
separate two-argument grant. Construction retains source-expression/getter order,
duplicate and null-key behavior, enumeration faults and comparer-sensitive copying.
Same-comparer source sets may preserve slots; different comparer identities rebuild
by enumeration. Seven probes supply 2,058 repeated O0/O2 native/.NET comparisons
with forced GC and ASan/UBSan; 68 distinct focused checks pass. Integer CIL v70
records admission; native v70/layout v26 and production budgets are unchanged.
See [ADR AC0155](../decisions/AC0155-ordinal-set-source-construction.md).
The frozen engine previously reached this constructor in HandLimitHandler.Resolve
after 820,241 metadata units. Full engine/fingerprint execution and replay remain open.

### GUID static parsing (AC5a.5)

Guid.Parse(String) shares the owned managed D/N/B/P/X parser with Guid(String).
It preserves the static API's input parameter name and FormatException for
UInt32 overflow; the constructor retains g and OverflowException. Parsed words
are copied into the existing Guid representation only after success. No compiler
profile, intrinsic grant, native operation or resource bound changes.
Six static probes and the promoted Parse control provide 7,266 repeated O0/O2
native/.NET comparisons with ASan/UBSan, forced collection and exact cleanup.
Constructor and static tests share malformed-input, whitespace and mutation corpora.
All 24 focused GUID checks pass. See ADR AC0156. TryParse/ParseExact and span/byte
inputs remain unmapped. Full engine and saved-game replay remain unverified.

### Three-component tuples (AC5b.15)

The owned ValueTuple<T1,T2,T3> supplies three fields, construction and typed
equality through the established value/key model. Comparisons copy each component,
read in order and stop on the first mismatch. Numeric and reference-bearing tuple
copies, returns, heap/vector storage, boxing and nullable values use existing paths.
Ten probes add 2,940 repeated O0/O2 native/.NET comparisons with ASan/UBSan and
forced collection. All 44 tuple-family checks pass. Hashing, text, object/structural
equality, comparison and ITuple indexing remain explicit diagnostics. No compiler
profile, intrinsic grant or resource-bound change. See ADR AC0157. Full engine and
saved-game replay remain unverified.

### Two-argument HashCode.Combine (AC5a.6)

HashCode.Combine<T1,T2> uses existing closed key hashing and the fixed-seed PairHash
combiner. Arguments are by-value copies, hashed in order with zero for null. Six
probes provide 1,764 repeated native/.NET behavioral comparisons; 294 additional
comparisons validate numeric output against an independent pinned-algorithm model
with Cilica's seed. Cross-runtime numeric hash equality is not claimed.
All 29 distinct selected hash/key/tuple checks pass after repairing an older array-key
diagnostic: unsupported array hashing/equality now fails before layout validation.
Nullable/array components, other arities and the mutable builder remain unsupported.
Existing grants and compiler/native profiles are unchanged. See ADR AC0158.
Full engine, fingerprint execution and saved-game replay remain unverified.

### Further diagnostic metadata capacity (AC8.47)

After committed HashCode.Combine support, the frozen engine exhausts the copied
1,048,576-unit metadata budget. Raising only that diagnostic copy to 1,572,864
reaches a static-field access rejection at AlchemyResolutionAdapter.Prepare IL_02ed
(1,100,871 charged audit units; 3,695 ms). StateLookup remains 15 methods/11,517 units.
Production limits are unchanged, as are all other diagnostic bounds and the
60-second command cap. This is discovery evidence, not native engine execution.
See ADR AC0159. Full saved-game replay remains unfinished.

### Nested private static fields (AC8.48)

The integer-object profile admits private static reads/writes from nongeneric
nested declarations when the existing validated lexical chain proves directional
containment in the declaring type. Discovery and both IR stages share the same
rule already used for private method access. Sibling, enclosing, unrelated, foreign,
generic-owner and legacy-profile access remain diagnostic; readonly writes retain
the owning-initializer restriction. Static addresses remain unsupported.
Six probes add 1,764 repeated O0/O2 native/.NET comparisons with forced collection,
ASan/UBSan, retained-root accounting and initializer-fault checks. All 107 distinct
focused/adjacent checks pass across the recorded final runs. CIL v71, object IR v45
and native v71 record admission; layout v26, interface plan v16 and limits remain
unchanged. See ADR AC0160. Full engine and saved-game replay remain unverified.


### Sealed application-class shallow cloning (AC8.49)

Object.MemberwiseClone now has an exact owned-core intrinsic grant for calls from
sealed application classes in the integer-object profile. The compiler validates
the complete ordinary application base chain and emits typed field copies into a
fresh allocation. Inherited fields, embedded value leaves and references are copied;
constructors and runtime ownership, identity and collector headers are not copied.
The source remains rooted across allocation, and allocation failure uses the existing
managed fault path. Nonsealed classes, special runtime hierarchies, invalid caller
receivers and pre-base constructor use remain diagnostic.
Eight probes provide 2,352 repeated O0/O2 native/.NET comparisons with ASan/UBSan,
forced collection and exact cleanup. Quota/header checks cover object and byte limits,
managed allocation faults and recovery. All 42 focused clone, constructor, identity,
collection and boxed-value checks pass. CIL v72, object IR v46 and native v72 record
the operation; layout v26, interface plan v16 and resource limits are unchanged.
See ADR AC0161. Full engine, unchanged fingerprint execution and saved-game replay
remain unverified.


### Read-only collection enumeration (AC4.10)

ReadOnlyCollection<T> now forwards enumeration to its backing list using existing
managed cursor support. The generic factory reads Count first and returns the shared
per-type empty array cursor at zero; otherwise it forwards GetEnumerator. The
nongeneric factory directly forwards without reading Count. This preserves live
array reads, list version checks, Current/Reset/Dispose behavior, empty cursor identity
and custom-list callback/fault order from the pinned .NET implementation.
Nine probes provide 2,646 repeated O0/O2 native/.NET comparisons with ASan/UBSan,
forced collection and exact static-root cleanup. All seven focused checks pass.
No compiler profile, intrinsic grant, native layout or resource-bound changes.
CopyTo and the remaining nongeneric search/index surface remain diagnostic. See
ADR AC0162. Full engine, unchanged fingerprint execution and saved-game replay
remain unverified.


### Deferred LINQ casts (AC6.17)

Enumerable.Cast<TResult>(IEnumerable) returns an already compatible generic source
unchanged, checks null immediately and otherwise defers element casts. Separate
managed cursors preserve the general yield iterator's automatic fault cleanup and
retained Current versus the ICollection iterator's caller-driven cleanup and cleared
Current. Collection Count/Any, materialization, First and Contains use the pinned
shortcuts; existing query interfaces preserve Select callbacks and cursor identity.
Reference, scalar, nullable and reference-bearing value targets use existing casts,
boxes, roots and managed exceptions. No compiler profile, intrinsic grant, layout or
resource bound changes. Unsupported source/element operations remain diagnostic.
See ADR AC0163 for differential evidence, source provenance and exact boundaries.
Full engine, unchanged fingerprint execution and saved-game replay remain unverified.

AC6.17 validation: 40 distinct checks pass, including 5,382 repeated native/.NET
Cast comparisons and the public baseline; O0/O2, forced GC and ASan/UBSan remain active.


### Dictionary and view interface enumeration (AC4.11)

Dictionary<TKey,TValue>, KeyCollection and ValueCollection now return boxed existing
cursors for nonempty generic/nongeneric enumeration. Their nongeneric Current checks
state before returning fresh value boxes or the original reference. Existing slot,
version, copy, Reset and no-op Dispose behavior is retained. Empty key/value views
share the mutable array cursor; empty dictionary pairs use a distinct stateless
per-type singleton whose Current always reports enumeration not started.
Reference, nullable and reference-bearing value payloads use existing tracing and
boxing. Legacy IDictionary.GetEnumerator and IDictionaryEnumerator.Entry/Key/Value
remain explicit unsupported bodies. No compiler profile, intrinsic grant, native
layout or resource-bound changes. See ADR AC0164 for tests and review.
Full engine, unchanged fingerprint execution and saved-game replay remain unverified.

AC4.11 validation: 33 distinct checks pass, including 4,212 repeated native/.NET
comparisons and the public baseline, with forced collection and ASan/UBSan.


### Measured diagnostic local capacity (AC8.50)

The frozen engine exhausts the diagnostic copy's aggregate local bound at 4,115/4,096
slots, with 7,549/8,192 parameters. A fresh disposable copy raises only locals to
6,144. Other copied limits remain types 2,048, methods 6,144, metadata 1,572,864,
instructions 131,072 and parameters 8,192; command timeout stays 60 seconds.
It reaches Boolean.TryParse(String, Boolean&) in ContributeShieldCardPrimitive's
bond-policy parser after 1,127,785 charged metadata units in 3,843 ms. StateLookup
remains 15 methods/11,527 units. Production limits/profiles are unchanged; no engine
C++ is emitted or executed. See ADR AC0165 and artifacts/alchemy-locals-6144.
Full engine, unchanged fingerprint execution and saved-game replay remain unverified.

### Width-preserving frame borrows (AC8.51)

Byte, short, Char and Boolean locals/parameters now support synchronous borrows
with explicit storage widths, preserved through both IR stages and checked native
carriers. Boolean retains its low byte. Mismatched widths and narrow field/array
addresses remain diagnostic. Fifteen probes supply 2,250 repeated O0/O2 native/.NET
comparisons with forced GC and sanitizers; see ADR AC0166. Integer CIL v73, object
IR v47 and native v73 record the extension. Limits are unchanged. The measured
Boolean.TryParse API and full saved-game replay remain unfinished.

### Boolean string parsing (AC5a.7)

The owned Boolean.TryParse(String, out Boolean) now preserves case-insensitive
true/false, surrounding Unicode whitespace/NUL, and failed-output reset through
local/parameter borrows. Four probes supply 3,660 repeated native comparisons;
852,096 additional managed character cases and all 29 selected checks pass.
Profiles, grants and limits are unchanged. The frozen engine now reaches generic
ReadOnlyCollection<T>.CopyTo after 1,124,897 charged diagnostic units; StateLookup
remains 15 methods/11,527 units. Full engine/fingerprint/replay execution remains
unfinished. See ADR AC0167 and artifacts/alchemy-boolean-tryparse-engine.

### Generic read-only collection copying (AC3.28)

ReadOnlyCollection<T>.CopyTo now forwards to the backing list without prevalidation
or extra callbacks. Eight probes provide 2,352 repeated native/.NET comparisons,
including live views, values/references, faults, partial copies and List snapshots.
Profiles and limits stay unchanged; nongeneric copying remains unsupported. Frozen
engine discovery next reaches Dictionary.KeyCollection.CopyTo at 1,147,132 charged
units. StateLookup remains 15 methods/11,527 units. Full replay remains unfinished.
See ADR AC0168 and artifacts/alchemy-readonly-copy-engine.

### Generic dictionary copies (AC3.29)

Dictionary keys, values and generic key/value pairs now copy with pinned validation,
active-slot order, reference identity and partial covariant-store behavior. Thirteen
probes plus capacity cases supply 4,146 repeated native/.NET comparisons. Owned
helpers retain the compiler's generic access boundaries; profiles and limits stay
unchanged. Frozen discovery next reaches HashSet.CopyTo through List.AddRange in
EffectTags.Combine (1,149,539 charged units); StateLookup remains 15 methods at
11,529 units. Full replay is unfinished. See ADR AC0169.

### Generic HashSet copies (AC3.30)

All three HashSet.CopyTo overloads now preserve validation order, negative actual
values, active-slot order, requested-count behavior and partial covariant writes.
Fifteen C#/raw-IL probes plus capacity cases provide 4,842 repeated native comparisons;
all 35 selected checks pass. No profiles, grants or production/native limits change.
Frozen engine discovery next exhausts the diagnostic parameter budget at 8,193/8,192
(4,504/6,144 locals; 1,159,312 charged units). StateLookup remains 15 methods/11,529
units. Full replay remains unfinished. See ADR AC0170.

### Measured diagnostic parameter capacity (AC8.52)

After observing 8,193/8,192 parameters, a fresh diagnostic copy raises only that
budget to 12,288. Other copied capacities, production/native limits and 60-second
commands stay unchanged. Frozen discovery reaches OrdinalCaseSensitiveComparer
hashing through TypeKeyOperations.Hash (1,159,620 charged units). Its exact IL calls
Object.GetHashCode on a loaded Type&; investigate over-broad virtual reachability
before treating this as a required hash API. StateLookup remains 15 methods/11,529
units; full replay is unfinished. See ADR AC0171.

### Known class receiver discovery (AC8.53)

Public zero-argument virtual discovery now uses a proven ordinary static class,
including abstract/closed generic bases and a reference loaded from a validated
managed location. Canonical slot selection, subclass overrides, null checks,
visibility and bounded same-block proof remain intact. This removes unrelated
comparer reachability from Type-key hashing. Seven new native probes provide
2,058 repeated O0/O2 comparisons with forced GC and sanitizers; all 47 selected
checks pass. Integer CIL v74 records the change; IR v47/native v73 and limits stay
unchanged. See ADR AC0172. Frozen discovery next reaches the actual ordinal string
hash in UnitId.GetHashCode at 1,182,823 metadata units; full replay is unfinished.

### Ordinal string hashing (AC7b.19)

StringComparer.Ordinal.GetHashCode(string) now checks null with parameter obj and
uses the existing managed UTF-16 key hash. Direct/generic comparer calls preserve
equal-string consistency and fault ordering. Numeric hashes are implementation-
specific and intentionally differ from randomized .NET values. Eight probes supply
5,454 repeated native comparisons; all 30 selected checks pass. Profiles, grants
and limits are unchanged. See ADR AC0173. Frozen discovery next reaches excessive
parameterless-comparer hash reachability from ReferenceKeyOperations<T>.Hash;
full engine/fingerprint/replay execution remains unfinished.

### Receiver-bounded virtual dispatch (AC8.54)

Class dispatch maps retain proven receiver classes from bounded stack discovery
or validated constrained reference calls. Bounds union across call sites; unproven
calls retain owner-wide discovery. Typed call validation and complete subclass
coverage prevent omitted receivers, including inherited Object implementations.
Six probes add 1,764 repeated native comparisons, with eight detached-IR corruption
cases and 88 passing selected checks. CIL v75/object IR v48/native v74 record the
contract; layout/interface/grants/limits are unchanged. See ADR AC0174. Frozen
discovery next reaches SelectMany(source, selector) in ConstructionInvitations at
955,482 metadata units; full engine/fingerprint/replay execution remains unfinished.

### Single-selector SelectMany (AC6.18)

The nonindexed two-generic SelectMany overload now provides deferred nested
iteration plus the pinned independent terminal paths. Managed segments preserve
inner collection Count/CopyTo choices, and cursor faults retain exact disposal
ordering. Fifteen probes provide 6,078 repeated native comparisons; all 45 selected
checks pass. Other overloads/nested-vector source types remain diagnostic. See ADR
AC0175. Profiles, grants and limits are unchanged. Frozen discovery next reaches
Single(source) in construction-contribution handling at 958,875 metadata units;
full engine/fingerprint/replay execution remains unfinished.

### Source-only Single terminals (AC6.19)

Single(source) and SingleOrDefault(source) now preserve the pinned IList Count/index
fast path and two-MoveNext enumerable path, including disposal and exact fault text.
Nine probes supply 2,424 repeated native comparisons; all 37 selected checks pass.
Explicit-default overloads remain unmapped. See ADR AC0176. Profiles, grants and
limits are unchanged. Frozen discovery next reaches indexed Select in
ContinueShieldContribution at 972,267 metadata units; full engine/fingerprint/replay
execution remains unfinished.

### Indexed Select (AC6.20)

The indexed Select overload now follows the pinned yield lifecycle with checked
indices, deferred source access, fault cleanup and retained Current. It uses no
optimized terminal shortcuts. Eleven probes supply 3,630 repeated native
comparisons plus two live-root checks; all 63 selected checks pass. See ADR AC0177.
Profiles, grants and limits are unchanged. Frozen discovery next reaches List<T>.
Reverse() in ContinueShieldWave at 964,532 metadata units. Full engine/fingerprint/
replay execution remains unfinished.

### List reversal (AC3.31)

List<T>.Reverse() and Reverse(index,count) now reverse the validated range in place,
including exact argument order/text and enumerator invalidation for empty/singleton
ranges. Nine probes supply 2,952 repeated native comparisons; all 34 selected checks
pass. See ADR AC0178. Profiles, grants and limits are unchanged. Frozen discovery
next reaches String.Split(char,StringSplitOptions) in DamageReplacementCheckpoint.
RecordAnswers at 989,604 metadata units. Full engine/fingerprint/replay execution
remains unfinished.

### Character string splitting (AC7b.20)

String.Split(Char,StringSplitOptions) now supports UTF-16 separators, empty-entry
removal and Unicode trimming through managed scans and existing substring copies.
Invalid flags and empty/input identities match the pinned runtime. Six probes
supply 3,696 repeated native comparisons; all 29 selected checks pass. See ADR AC0179.
Other split overloads remain diagnostic. Profiles, grants and limits are unchanged.
Frozen discovery next reaches invariant Int64.TryParse with NumberStyles.None in
RecordAnswers at 990,636 metadata units. Full engine/fingerprint/replay execution
remains unfinished.

### Invariant Int64 parsing (AC5a.8)

The exact Int64.TryParse(String,NumberStyles,IFormatProvider,out Int64) declaration
now accepts directly proven None and InvariantCulture. A managed parser checks
unsigned accumulation before overflow and preserves all output bits. Four probes
supply 3,660 repeated native comparisons; 131,648 additional grammar cases are
managed-only. All 72 distinct selected checks pass. See ADR AC0180. CIL v76/object
IR v49 record the wider contract; native v74 and all limits remain unchanged.
Frozen discovery next stops at nonpublic GameOccurrence.Permits dispatch in
ShieldReactionDiscovery.Discover (998,224 metadata units). Full engine/fingerprint/
replay execution remains unfinished.

### Same-assembly internal class dispatch (AC8.55)

Closed class dispatch now admits internal virtual/abstract slots and C#'s
CheckAccessOnOverride flag under exact same-assembly receiver/override and caller
access checks. Other access modes and legacy/interface profiles remain restricted.
Eight probes add 3,024 repeated native comparisons; all 123 selected checks pass.
See ADR AC0181. Closed class target v4, CIL v77 and object IR v50 record the change;
native v74, grants and limits are unchanged. Frozen discovery now reaches the
separate inline-value abstract-signature restriction at GameOccurrence.Permits
(998,255 metadata units). Full engine/fingerprint/replay execution is unfinished.

### Copied value arguments on abstract class calls (AC8.56)

Abstract declarations now accept copied inline-value parameters and expand their
nested scalar/reference leaves with the same layouts as concrete implementations.
Empty values remove no calls or null checks. Value returns and borrowed abstract
parameters remain diagnostic. Six probes supply 1,764 repeated native/.NET
comparisons; all 65 distinct selected checks pass. See ADR AC0182. CIL v78/object
IR v51 record the contract; native v74, grants and limits are unchanged. Frozen
engine discovery advances past Permits(PlayerId) to a custom message/inner-exception
constructor in SetRangeModifier.DistanceFromFeet (1,019,328 metadata units).
Full engine, unchanged fingerprint and saved-game replay execution remain unfinished.

### Custom message/inner exception forwarding (AC8.57)

Exception(String,Exception) and fieldless custom forwarding chains now preserve
exact message/inner identity through existing traced payloads. Discovery checks
every argument and direct-base constructor; both IR stages and native allocation
share allowed wrapper identities. Side effects and unsupported framework families
remain diagnostic. Eight probes supply 2,352 repeated native/.NET comparisons,
with two additional GC/allocation-failure checks; all 90 selected checks pass.
See ADR AC0183. Integer CIL v79, exception IR v9 and native v75 record the change.
Frozen discovery advances to missing EqualityComparer<T> mapping in target
selection (1,039,295 metadata units). Full engine/fingerprint/replay remains unfinished.

### Default comparers for exact equatable keys (AC3.32)

EqualityComparer<T>.Default retains one comparer per closed T and runtime for four
integer types and exact IEquatable<T> keys. Existing key operations preserve copied
structs, null handling and typed callbacks. Eleven probes supply 3,234 repeated
native/.NET comparisons plus two independent-runtime ownership checks; all 80
selected checks pass. See ADR AC0184. Integer CIL v80 adds a private factory grant;
native v75, IR and limits are unchanged. Specialized string/enum/nullable/object
fallbacks, nongeneric operations and comparer-object methods remain diagnostic.
Frozen discovery advances to the existing field-metadata limit (1,038,837 work
units). Full engine, unchanged fingerprint and saved-game replay remain unfinished.

### Diagnostic field-budget audit (AC8.58)

The unchanged frozen engine exhausts 4,096 cached field declarations at 1,914 type
snapshots; the next type needs eight more fields. Explicit roots then contain 184
values, 1,304 references and 157 vectors. These are partial discovery counts, not
emitted layouts. Raising only the isolated diagnostic copy to 6,144 fields reaches
String.IndexOf(Char) in TargetOptions.ParseCell (1,041,357 metadata units/4,429 ms).
Production limits, profiles and semantics remain unchanged. See ADR AC0185.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.

### Character string searching (AC7b.21)

String.IndexOf(Char) and its start/count overloads now use a managed UTF-16 scan,
with exact ordered range validation and errors. Seven probes supply 5,640 repeated
native/.NET comparisons; all 69 selected checks pass. See ADR AC0186. Profiles,
grants and production limits are unchanged. Frozen discovery with the measured
diagnostic field allowance advances to Int32.Parse(String,NumberStyles,IFormatProvider)
in TargetOptions.ParseCell (1,051,158 metadata units). Full engine, unchanged
fingerprint and saved-game replay execution remain unfinished.

### Throwing invariant Int32 parsing (AC5a.9)

The exact three-argument Int32.Parse now admits directly proven Integer and
InvariantCulture. Shared managed status parsing distinguishes malformed text from
overflow, preserving invalid-suffix precedence and existing TryParse outputs.
Four probes supply 2,202 repeated native/.NET comparisons; all 73 selected checks
pass. Another 262,752 Parse/TryParse oracle cases are managed-only. See ADR AC0187.
Integer CIL v82 and object IR v53 add the exact throwing-mode declaration contract;
native v75 and production limits remain unchanged. Frozen engine discovery now
reaches the diagnostic instruction limit (1,049,093 metadata units). Full engine,
unchanged fingerprint and saved-game replay execution remain unfinished.

### Diagnostic instruction-budget audit (AC8.59)

The frozen engine consumes 131,071 of the diagnostic 131,072 instructions; the next
method needs more than the one remaining. Partial discovery has 5,147 declarations,
5,096 bodies, 324,841 IL bytes, 10,035 parameters and 5,994 locals. Raising only the
archived diagnostic compiler to 196,608 instructions reaches String.ToLowerInvariant
in ContinueConstructionContribution (1,052,133 metadata units/4,239 ms). Production
limits remain unchanged. This audit uses AC5a.9's archived CIL v82/IR v53 sources,
separate from concurrent Boolean-vector work. See ADR AC0188. Full engine,
unchanged fingerprint and saved-game replay execution remain unfinished.

### Pinned invariant string lowercasing (AC7b.22)

String.ToLowerInvariant now uses pinned Unicode 16.0.0 simple lowercase mappings,
including the invariant U+0130 exception, supplementary pairs, lone surrogates and
pinned .NET string identity. A generated managed lookup uses no static arrays or
host globalization API. All 1,114,112 code-point inputs match the local .NET oracle;
18,156 repeated native comparisons cover exact changed scalars/neighbors, complete
Unicode string-block hashes, identity, nulls and mixed text. All 98 selected
checks pass. See ADR AC0189. No compiler profile, grant or production limit changes.
Frozen discovery advances to 6,145/6,144 diagnostic locals in ContinueShieldContribution;
full engine, unchanged fingerprint and saved-game replay remain unfinished.

### Diagnostic local-budget audit (AC8.60)

After AC7b.22, the frozen engine needs 6,145 local slots against the diagnostic
6,144 allowance (10,345/12,288 parameters). Raising only the archived diagnostic
copy to 8,192 locals reaches Enumerable.Skip in ContinueAcquiredShieldChoice,
with 1,075,533 metadata units/4,336 ms. Production limits remain unchanged; no
engine code is emitted or executed. See ADR AC0190. Full engine, unchanged
fingerprint and saved-game replay remain unfinished.

### Skip over ordinary enumerable and list sources (AC6.21)

Enumerable.Skip now supports ordinary IEnumerable and IList sources, including
arrays, deferred acquisition/disposal, repeated/overflowed skips, mutable lists,
count/first/materialization and the exposed read-only list view. Copied equality,
ToList capacity and source/destination copy validation match the pinned .NET paths.
Seventeen probes supply 7,344 repeated native comparisons; all 124 selected checks
pass. Optimized LINQ-source specializations remain explicit build diagnostics,
including Where, Select and ordering. See ADR AC0191. No compiler/native profile,
intrinsic grant or production-limit change is needed. Frozen discovery remains
partial; full engine, unchanged fingerprint and saved-game replay are unfinished.

### Diagnostic type-snapshot audit (AC8.61)

AC6.21 fills the 2,048 cached type snapshots in the diagnostic copy. Raising only
that archived copy to 3,072 reaches Guid.TryParse(String, out Guid) in
TargetOptions.ParsePlayer at IL_0003. Discovery charges 1,117,323 metadata units
in 4,288 ms; StateLookup remains 15 methods/11,613 units. Production's 256-type
limit stays unchanged. See ADR AC0192. No engine emission or execution occurs;
full engine, unchanged fingerprint and saved-game replay remain unfinished.

### Nonthrowing string GUID parsing (AC5a.10)

Guid.TryParse(String, out Guid) now shares D/N/B/P/X validation with the constructor
and Parse, returns false and clears the output for null/invalid/overflowing input,
and creates no parsing exception on those paths. The throwing APIs retain their
exact fault policies. Six new probes supply 6,972 repeated native/.NET comparisons;
all 32 selected GUID checks pass with O0/O2, forced GC and sanitizers. See ADR AC0193.
The frozen engine advances to invariant Int64.Parse(String,IFormatProvider) in
ShieldAcquisitionReceipt.Read. Full engine, fingerprint and replay remain unfinished.

### Throwing invariant Int64 parsing (AC5a.11)

Int64.Parse(String,IFormatProvider) now supports a directly proven InvariantCulture
provider and implicit Integer style. Its full signed range, ASCII whitespace,
trailing NULs and exact format/overflow precedence match .NET. Four probes supply
2,202 repeated native comparisons; all 40 selected integer-parser checks pass,
including 262,754 managed-only oracle cases. Integration with the separately
committed checked-index slice passes 69 distinct selected checks. See ADR AC0194. The frozen engine next
reaches constrained ElementGender.ToString; full engine, fingerprint and saved-game
replay remain unfinished.

### Constrained default enum text (AC7b.23)

Exact constrained Object.ToString calls on admitted Int32 enums now read the scalar
receiver and use managed formatting. Names are cached per closed enum type;
unnamed values reuse the existing invariant integer cache. Private metadata helpers
stay private. Nine probes supply 3,030 repeated native/.NET comparisons, plus 129
allocation-limit recovery cases at each optimization level. All 77 selected checks
pass. See ADR AC0195. Flags, aliases, wide/boxed receivers and direct enum-array
addresses remain diagnostic. Frozen discovery reaches 12,290/12,288 diagnostic
parameters; full engine, unchanged fingerprint and saved-game replay are unfinished.

### Diagnostic parameter and method allowances (AC8.62)

After AC7b.23 reaches 12,290/12,288 parameters, increasing only the copied parameter
allowance to 16,384 reaches exactly 6,144 method declarations. Increasing only that
copied method allowance to 8,192 then reaches HashSet<T>.UnionWith through
ResolveChain. Production remains 4,096 parameters/512 methods. See ADR AC0196.
No engine code is emitted or executed; full engine, fingerprint and replay remain
unfinished.

### Enumerable set union (AC3.33)

HashSet<T>.UnionWith now performs the pinned null check and one-pass insertion.
It preserves source callbacks/disposal, partial mutation, self-union and stored
identity without Count/CopyTo shortcuts. All 67 selected set checks pass; twelve
new probes provide 5,184 repeated native/.NET comparisons. See ADR AC0197. Frozen
discovery next reaches protected abstract GameOccurrence.WriteContext dispatch.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.

### Protected class slots (AC8.63)

Closed class dispatch admits exact protected slots with same-assembly receivers
and overrides. Caller access remains independently checked: calls from the owning
class work; this does not grant inherited or foreign protected access. All 176
selected checks pass, including 3,024 new repeated native/.NET comparisons. See
ADR AC0198. Frozen discovery passes WriteContext and reaches the default string
comparer branch behind an explicit ordinal ThenBy. Full engine, unchanged
fingerprint and saved-game replay remain unfinished.

### Explicit ordinal ordering (AC6.22)

All four OrderBy/ThenBy comparer overloads admit directly proven StringComparer.Ordinal
string keys through checked managed bridges. Sorting, selector order, stable ties and
terminal behavior reuse the owned implementation; cloning reuses its resolved comparer.
Ordinal ThenBy requires exact owned source element types in the closed receiver universe;
custom/covariant sources and unproven comparers remain diagnostic. Ninety-one distinct
selected checks pass across the integrated run and targeted fixture repairs, including
4,410 new repeated native/.NET comparisons. See ADR AC0199. Frozen discovery advances
to missing IGrouping<TKey,TElement> in forced movement. Full engine, unchanged fingerprint
and saved-game replay remain unfinished.

### Default grouping (AC6.23)

The two-argument GroupBy and IGrouping now preserve first-key group order, element
order, deferred buffering, callback failures, read-only group lists and cursor state.
Managed lookup storage uses the existing admitted default key operations. Sixty-two
selected checks pass, including 6,342 new repeated native/.NET comparisons and exact
retained-graph collection checks. See ADR AC0200. Frozen engine discovery advances
to missing Stack<T> in forced movement. Comparer/selector overloads, ToLookup and
optimized query Skip remain unimplemented. Full engine execution, unchanged
fingerprinting and saved-game replay remain unfinished.

### Bounded stack storage (AC3.34)

Stack<T> now supports parameterless construction, Count, Push and Pop with pinned
growth and empty-stack exceptions. Popped reference-bearing values release their
stored references; failed growth leaves the stack unchanged and recoverable.
Twenty-three selected checks pass, including 2,352 new native/.NET comparisons.
See ADR AC0201. Frozen discovery advances to CollectionExtensions.GetValueOrDefault
in forced movement. Stack enumeration, other constructors and remaining members stay
diagnostic. Full engine, unchanged fingerprint and saved-game replay remain unfinished.

### Dictionary fallback lookup (AC3.35)

Both IReadOnlyDictionary GetValueOrDefault overloads now use the owned null check
and one ordinary TryGetValue call, preserving custom dictionary callbacks and fallback
values. Eighteen checks pass with 2,352 native/.NET comparisons. See ADR AC0202.
Frozen engine discovery advances to Nullable<T>.ToString in composite formatting;
other CollectionExtensions members remain diagnostic. Full engine, unchanged
fingerprint and saved-game replay remain unfinished.

### Named nullable text (AC1a.2)

Nullable<T>.ToString now returns empty text when absent and invokes an admitted named
payload override when present. Payload mutation, null returns, exceptions and receiver
storage follow pinned .NET behavior. Twenty-nine selected nullable checks pass, with
2,058 new native/.NET comparisons. See ADR AC0203. Primitive constrained formatting,
narrow field addresses and nullable composite boxes retain explicit diagnostics.
Frozen discovery advances to Enumerable.Range; the independently observed nullable-box
formatting gap also remains. Full engine, unchanged fingerprint and replay are unfinished.

### Nullable boxed formatting (AC7b.24)

String.Format now recognizes directly proven nullable boxes of admitted named values
and Int32 enums. Discovery selects the actual payload type; block-local proof comes
only from the exact nullable box operation. The shared formatter treats null arguments
as empty text before payload dispatch. Sixty-four checks pass with 2,646 new repeated
native/.NET comparisons. See ADR AC0204. CIL93; IR59/native79 unchanged. Aliases, joins,
primitive nullable formats and IFormattable payloads stay diagnostic. Frozen discovery
still stops at Enumerable.Range. Full engine, unchanged fingerprint and replay are unfinished.

### Int32 ranges (AC6.24)

Enumerable.Range now validates bounds and supplies a reusable Int32 iterator with
read-only list views and the admitted query operations. End-marker wraparound,
retained cursor values, copy exceptions and independent Select traversal match the
pinned runtime. All 24 focused Range checks pass with 3,528 repeated native/.NET
comparisons; 23 adjacent controls also passed. See ADR AC0205. Frozen engine discovery
advances to the isolated scan's 8,192-local allowance (8,194 observed). Production
limits and CIL93/IR59/native79 are unchanged. Full engine execution, unchanged
fingerprinting and saved-game replay remain unfinished.

### Diagnostic local-storage allowance (AC8.64)

After Range exposed 8,194 aggregate locals, the isolated engine-discovery probe now
allows 12,288 locals. The production 4,096-local limit and CIL93/IR59/native79 are
unchanged. Discovery advances to Enum.GetValues(Type) in SpellModeChoices.BondsAfter;
StateLookup still translates its 15-method closure. See ADR AC0206. This is diagnostic
coverage, not full engine compilation, native execution or replay evidence.

### Named enum value arrays (AC5b.16)

Enum.GetValues(typeof(Int32-enum)) now constructs fresh nominal arrays in unsigned
constant order, retaining aliases and empty-array identity. One uninterrupted typeof
sequence and exact grants for both calls are required; dynamic Type values, other
carriers and more than 256 entries remain diagnostic. Sixty-eight distinct selected
checks pass, including 3,234 new native/.NET comparisons. See ADR AC0207. CIL94;
IR59/native79 unchanged. Frozen discovery advances to parameterless List<Int64>.Sort
in GameStateHasher.AppendSpell. Full engine, unchanged fingerprint and replay are unfinished.

### Default signed integer list sorting (AC3.36)

Parameterless List<Int32>.Sort and List<Int64>.Sort reuse the managed introsort and
existing granted numeric CompareTo bridge. Successful sorts invalidate cursors even
for zero/one items; no comparer cache is created. Thirty-seven selected checks pass,
including 1,764 new native/.NET comparisons. See ADR AC0208. No new compiler/runtime
operation, profile or grant is introduced (validated snapshot CIL94/IR59/native79).
Unsigned delegate/vector composition and other default comparer types remain diagnostic.
Frozen engine discovery advances to an access check in HashSet<internal WaveGroupId>
hashing. Full engine, unchanged fingerprint and saved-game replay remain unfinished.

### Bounded vector section clearing (compatibility batch010)

Array.Clear(Array,Int32,Int32) now selects a granted ordinary managed helper for
same-block proven vectors. Complete null/range validation precedes typed default
stores; covariance, argument evaluation and reference release are preserved.
Int32/UInt32/Int64/enums, references and supported inline values have O0/O2 .NET and
GC evidence. Narrow scalar generic locations, general Array aliases and joined
argument stacks remain diagnostic. CIL95; IR59/native79, layouts, quotas and
collector unchanged. See [decision](../decisions/compatibility-012-array-section-clear.md)
and compatibility batch010. CircularBuffer still needs public Copy/ArraySegment;
its upstream code and full Alchemy replay have not been executed by this slice.

### Hidden value-key callbacks (AC8.65)

Core collections can invoke the exact GetHashCode override and IEquatable<T>.Equals
implementation of admitted internal/private value keys, including explicit interface
implementations. Metadata-selected callback contracts survive scalar lowering and
are rechecked at both IR stages; ordinary private calls stay restricted. Existing
borrow/copy semantics preserve mutating keys, allocating callbacks and exception order.
Sixty-five distinct selected checks pass, with 3,234 new native/.NET comparisons.
See ADR AC0209. CIL97/IR60 adds checked call evidence; native79 and production limits
are unchanged. Private nested generic construction and default value-key text retain
independent diagnostics. Frozen discovery passes WaveGroupId hashing and next exhausts
the existing diagnostic instruction allowance. Full engine execution, unchanged
fingerprints and saved-game replay remain unfinished.

### Bounded public vector copying (compatibility batch011)

Array.Copy now supports the three/five-argument Int32 overloads for same-declared-
element vectors proven in one block. Managed validation preserves null/range/type
order; directional copying preserves overlap and existing checked stores preserve
covariant partial faults and reference lifetimes. Scalar, reference, inline-value,
constructor-field and mutation probes run against .NET at O0/O2 with sanitizers.
General Array aliases, unsupported producer shapes, cross-element widening/boxing
and Int64 overloads remain diagnostic. CIL98; IR60/native79, quotas and runtime
unchanged. See [decision](../decisions/compatibility-013-array-section-copy.md).
CircularBuffer itself is unexecuted; ArraySegment and full replay remain open.

### Initial interface-planning audit allowances (AC8.66)

After AC8.65, the isolated frozen-engine scan reaches 196,602 decoded instructions;
the next method consumes its remaining six-instruction quota with 29 IL bytes left.
A diagnostic-only 262,144-instruction allowance lets the initial work queue drain at
7,621 bodies. Interface planning then exhausts 1,572,864 metadata work units. Raising
only that isolated budget to 2,097,152 reveals the next semantic gap: nongeneric
narrow-array enumeration reached through OfTypeIterator.Count. This is partial
closure discovery, without full IR admission, native emission or replay. Production
limits remain 65,536 instructions/metadata units; StateLookup still translates 15
methods. See ADR AC0210 for the measured progression and unchanged input identities.

### Narrow nongeneric array enumeration (AC7a.8)

Boolean, Byte and Char arrays now use the existing nongeneric array cursor and exact
primitive boxes. Shared discovery/detached-plan rules preserve the actual element
identity; generic enumeration, other narrow element operations, signed/UInt16 array
construction and OfType<byte>'s narrow-memory requirement remain diagnostic. All 36
selected checks pass on the combined Array.Copy snapshot, including 2,352 new repeated
native/.NET comparisons at O0/O2 with sanitizers and forced GC. CIL99/interface-plan18;
object IR60/native79 and all production quotas remain unchanged. See ADR AC0211.

The follow-up AC8.67 audit increases only copied diagnostic metadata work to 4,194,304
after measured 2,097,152 exhaustion. Frozen discovery then identifies boxed List<T>
enumerator covariance at IEnumerator<T>.Current as the next gap. This is partial
interface planning, not full engine execution, fingerprint parity or saved-game replay.

### Boxed single-candidate interface variance (AC8.68)

Named boxed values now share the existing exact-first, single-candidate variance
selection with class receivers. Discovery selects the declared contract; both IR
stages check its original member identity and compatible reference/scalar signature.
Boxed payloads remain borrowed in place, preserving mutation and allocation roots.
All 55 distinct selected checks pass, including 2,940 new native/.NET comparisons.
CIL100/interface-plan19; object IR60/native79, core bodies and production quotas are
unchanged. Ambiguous/generic variance and missing return-interface catalog discovery
remain diagnostic. Frozen discovery finishes its initial interface-planning pass,
then reaches 10,797 method declarations/mappings against the 8,192 diagnostic quota.
See ADR AC0212. Full engine execution, fingerprint parity and replay remain unfinished.


### Bounded ArraySegment views

ArraySegment<T> constructors and Array/Offset/Count now use ordinary rooted managed
value fields. Null/negative/range ordering, default values, shared backing identity
and copied segments preserve pinned behavior. Required interface metadata remains
exact; existing boxed Count is tested, while other interface/indexer/equality/copy/
enumerator bodies remain unsupported. Empty segments retain the complete backing
array. No compiler/runtime profile, grant or quota changes: CIL99/IR60/native79.
Batch012 passes 48 focused checks, 2,940 new repeated native/.NET comparisons and 4 GC
lifetime variants. The actual CircularBuffer trial remains blocked at same-owner
constructor chaining before oracle/native execution; full replay is unverified.

### Interface-body discovery audit allowances (AC8.69)

After AC8.68, raise only the isolated diagnostic method allowance from 8,192 to
12,288 after measuring 10,797 distinct declarations/mappings. Loading the selected
interface bodies then reaches 16,385/16,384 aggregate parameter slots; a diagnostic
32,768-parameter allowance advances to unmapped System.Decimal in frozen spell-route
selection. Frozen IL requires Int32 conversion/construction, decimal multiplication,
division and invariant-provider text. Production limits remain unchanged. This is
partial closure discovery, without full engine emission/execution, fingerprint parity
or saved-game replay. See ADR AC0213.


### Same-owner constructor chains

The integer-object profile admits acyclic calls to another constructor on the exact
same closed class, retaining direct receiver, access, single-prefix initialization
and uninitialized-object safety checks. Both IR representations verify the chain
graph. Typed value-array argument stores now retain those checks after lowering.
CIL101/object IR61; native79, runtime and quotas unchanged. Batch013 passes 111 focused
checks, 2,058 new native/.NET comparisons and allocation-failure recovery at O0/O2.
CircularBuffer requires a new frozen selected-root trial; no external retry ran.


### Bounded Decimal route arithmetic (AC8.70)

The owned Decimal value implements Int32 construction/conversion, multiplication,
division and directly proven invariant-provider text. A reusable managed helper
preserves the 96-bit coefficient, sign, scale, nearest-even rounding, overflow and
division-by-zero behavior. Fixed six-word scratch arrays use existing managed
operations; there is no new native numeric ABI or floating-point approximation.
CIL102/object IR62 record Decimal provider admission; native79 and production limits
are unchanged. Constants, five-part/other constructors, parsing, other operators,
equality/hashing and general formatting remain diagnostic.

The final merged snapshot passes 71 focused/adjacent checks, including 38,766 new
O0/O2 native/.NET comparisons with ASan/UBSan, forced collection, exact root/borrow
cleanup and frame-budget boundaries. The frozen engine advances past Decimal to
missing Enumerable.SequenceEqual in redirect-edit admission. This remains partial
closure discovery, without full engine execution, fingerprint parity or replay.
See ADR AC0214 and artifacts/alchemy-decimal{,-merged,-refresh,-engine}.


### Bounded SequenceEqual traversal (AC8.71)

SequenceEqual now accepts two admitted sequences, with default equality or an
admitted explicit comparer. Preserve first/second null precedence, collection
count shortcuts, indexed reads and paired-enumerator acquisition, callbacks and
reverse disposal. Exact arrays/lists retain rooted backing-storage/count snapshots,
including across callback mutations; other shapes use the collection or cursor path.
Default value equality receives copies, so mutating Equals cannot alter the source.
Existing unsupported key types remain diagnostic, including Nullable.

Ten focused checks pass, including 7,920 O0/O2 native/.NET comparisons with forced
collection, sanitizers and exact cleanup/frame-budget checks. No compiler profile,
intrinsic, runtime, emitter or production quota change. The frozen engine advances
to Stack<T>'s unimplemented nongeneric enumerator via OfType.Count. Full engine
execution and replay remain unfinished. See ADR AC0215.


### Bounded Stack enumeration (AC8.72)

Stack<T> now has the pinned concrete Enumerator plus generic/nongeneric factories.
Version checks follow successful Push/Pop mutations; failed growth preserves active
cursors. Nonempty interface factories box independent mutable cursors; empty factories
share the existing per-T array/list cursor. Preserve reverse order, copied/current
values, reset/dispose behavior and exact System.Collections mutation text. Concrete
empty/default cursors retain their distinct behavior. Direct nested Enumerator use
requires its exact reference-to-core mapping alongside Stack<T>.

All 35 focused/adjacent checks pass, including 3,234 new O0/O2 native/.NET comparisons,
existing lifetime tests and two new quota/cursor recovery checks. Compiler profiles,
runtime, emitter and production quotas are unchanged. The frozen engine advances to
its diagnostic 3,072 cached-type allowance; full admission/execution/replay remain
unfinished. See ADR AC0216.


### Later interface-body audit headroom (AC8.73)

The diagnostic scan passes Stack enumeration, then exhausts cached types, fields,
call arguments and combined interface-method declarations. Increase only isolated
audit allowances to types 4,096, fields 8,192, call arguments 131,072 and methods 16,384.
After measuring 259,735/262,144 instructions and 11,747/12,288 locals, add proactive
headroom to 393,216 instructions, 16,384 locals and 6,291,456 metadata units; these last
three original limits were not exhausted in that run. Parameters remain 32,768.
Production limits and the 60-second probe bound remain unchanged.

The next semantic gap is Int32.Parse(String,IFormatProvider) in the unchanged
ActivationCountDamage operation, using directly supplied InvariantCulture. EngineStep
still fails during discovery; StateLookup validates a 15-method program. These diagnostic
scans suppress C++ emission for both roots; no probe output is compiled/executed natively. Full engine/fingerprint/replay work
remains unfinished. See ADR AC0217.


### Provider-only Int32 parsing (AC8.74)

Int32.Parse(String,IFormatProvider) now uses the existing owned Integer parser with
an exact overload-specific InvariantIntegerParsing grant and a directly proven
InvariantCulture operand. Validation derives the provider position from the selected
signature and the result width from the primitive identity. CIL104/object IR63;
native79, runtime operations and production quotas are unchanged. Other providers,
provider aliases, default overloads and Int64 explicit-style Parse remain diagnostic.

All 90 combined parsing/Char-delegate checks pass on an isolated snapshot, including
2,202 new repeated O0/O2 native/.NET comparisons for four parsing roots, forced GC,
ASan/UBSan and exact frame/root cleanup. Removing only this overload's grant rejects
it while preserving explicit-style Int32 Parse. The frozen engine passes the new call
and reaches unmapped SortedDictionary<TKey,TValue> in DispelOperation.Run. The measured
probe predates the merged Char-delegate source; full native engine/replay remains open.
See ADR AC0218. AC8.73 emission notes are corrected: diagnostic scans emit neither root.


### Bounded ordinal sorted dictionary (AC8.75)

SortedDictionary<string,TValue> now admits a directly proven StringComparer.Ordinal
constructor through an exact factory grant, Count/Comparer, Add, indexed read/write,
ContainsKey, TryGetValue and concrete/generic/nongeneric traversal. The owned red-black
tree preserves insertion order of comparisons, duplicate/version behavior, live current
values and shared cursor-copy paths. Empty interface cursors share the mutable array
cursor, distinct from ordinary Dictionary's empty cursor. Remaining members are absent
or diagnostic stubs; public SortedSet construction is not implemented.

All 21 focused/adjacent checks pass, including 2,592 new O0/O2 native/.NET comparisons
and two allocation-failure recovery checks, with sanitizers, forced collection and
exact frame/root cleanup. CIL106; object IR63/interface20/native79 unchanged. The frozen
scan next stops at String.LastIndexOf(Char) in SurfaceOccupants.SettleDrops. A separate
reduced probe retains the duplicate-text diagnostic for non-sealed reference values
such as List<string>; the full Dispel path is not yet admitted. No engine C++ emission,
native engine run or saved-game replay. See ADR AC0219.


### Reverse character search (AC8.76)

String.LastIndexOf now implements the Char, Char/startIndex and Char/startIndex/count
overloads through bounded managed UTF-16 scans. Preserve the pinned empty-string return
before validating bounds, then validate startIndex before count with exact parameter
names/messages. Count zero performs no reads. String needles, comparison modes and
LastIndexOfAny remain diagnostic. No new grants, compiler profiles, runtime operations
or production quotas; CIL106/object IR63/interface20/native79 remain unchanged.

All 30 forward/reverse search checks pass, including 5,640 new repeated O0/O2 native/.NET
comparisons, ASan/UBSan, forced collection and exact frame/root cleanup. The frozen engine
passes SurfaceOccupants.LastIndexOf and reaches the three-argument Enumerable.Contains
call in ForcedMovementOperation.OneOf. Full native engine/replay and the independently
confirmed polymorphic sorted-dictionary duplicate-text gap remain open. See ADR AC0220.

### Explicit-comparer sequence membership (AC8.77)

`Enumerable.Contains<T>(source,value,comparer)` now supports admitted default-key
and comparer shapes. An explicit null comparer uses default equality, including for
collections with their own Contains behavior. Exact arrays and Lists retain their
original storage/count through comparer callbacks; other sources enumerate with
normal short-circuiting and disposal. Nullable/default-key and unsupported collection
construction gaps remain diagnostic. No compiler profile, runtime or quota changes.

The broad run passed 37 of 38 checks; its sole failure was a stale negative for a
reference OfType/Select composition that now translates. Replacing it with native
positive coverage passed both optimization levels: 39 distinct final checks passed
across the broad run and focused follow-up, with 12,534 new native/.NET comparisons.
ASan/UBSan, forced collection, exact roots/borrows and frame budgets remain enabled.
The frozen engine scan now reaches String.Join in ForcedMovementOperation.Encode
(3,306,855 work units); StateLookup validates 15 methods. Neither audit emits C++.
Full engine closure/replay and non-sealed sorted-dictionary duplicate text remain
open. See ADR AC0221 for evidence and scope.

### String-sequence joining (AC8.78)

`String.Join(string,IEnumerable<string>)` now uses List/array storage shortcuts and
ordered cursor traversal for other sequences. Preserve null separator/entries,
empty and singleton identity, UTF-16 code units, length checks and finally disposal.
Other Join overloads remain diagnostic. Existing managed UTF-16 buffers and the
owned chunk builder replace pinned native/span construction; no runtime/profile or
quota changes, and concurrent source mutation is not validated.

Twenty distinct checks pass across the adjacent run and focused follow-up, including
5,376 new O0/O2 native/.NET comparisons with sanitizers, forced collection and exact
cleanup/frame budgets. The first run's two failures were an incorrect cached-delegate
expectation in the new test; fixture IL proves a fresh delegate, and the corrected
zero-root test passes. The frozen engine scan now reaches String.Replace in
SuppressTaggedEffectOperation.Label (3,310,764 units); StateLookup validates 15
methods. Both audit emission fields are null. Full closure/replay and the separate
non-sealed sorted-dictionary duplicate-text gap remain open. See ADR AC0222.

### Character replacement (AC8.79)

`String.Replace(Char,Char)` now preserves the original string for equal characters
or no match, otherwise copies exact UTF-16 units to a fresh string through existing
managed construction. String-pattern/culture overloads remain diagnostic. All 12
focused/adjacent checks pass, including 5,880 new sanitized O0/O2 native/.NET
comparisons with forced collection and exact cleanup/frame budgets.

The frozen engine scan now reaches List.RemoveAll in ObstacleOverflow.Drain
(3,379,372 work units); StateLookup validates 15 methods. Neither audit emits C++.
No profile/grant/runtime/production-budget changes; full closure and saved-game
replay remain incomplete. See ADR AC0223 for validation and the frozen hashes.

### Predicate list removal (AC8.80)

`List<T>.RemoveAll` now follows pinned live compaction, callback/exception order,
reference-bearing tail clearing and completion-only size/version publication.
Reentrant list mutation remains observable; null predicates and failed clear ranges
retain managed faults. All 19 focused/adjacent checks pass, including 4,608 new
O0/O2 native/.NET comparisons and four new live-list GC checks for references and
reference-bearing values. Existing sanitizer, cleanup and frame checks remain on.

The frozen engine now reaches the previously reduced polymorphic duplicate-text gap
in SortedDictionary.Add through DispelOperation. The collection value is a non-sealed
reference type; its ToString dispatch is not yet admitted by DictionaryKeyText.
Engine work is 3,387,734 units; StateLookup validates 15 methods. Both Emission fields
are null. Full closure and saved-game replay remain incomplete. See ADR AC0224.
