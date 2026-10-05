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

ADR 0180 implements the first observed engine blocker: bounded typeof/GetType,
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
