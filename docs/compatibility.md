# Compatibility manifest

Updated 2026-10-06. Capabilities are stage-specific. A translated operation does
not establish native execution or platform compatibility. Sections below preserve
the evidence and limits of earlier checkpoints; the current native profile retains the collector and handle behavior recorded
at milestone 4, with bounded value/generic/delegate/exception capabilities documented
separately. Historical checkpoint sections retain their original stage-specific limits.

Milestone 6 completion used `integer-managed-objects-cil-v2`,
`object-layout-v13`, `object-operations-ir-v14`, `scalar-location-ir-v2`,
`value-arrays-ir-v2`, `value-location-scalars-v4`, `boxed-value-ir-v3`,
`scalar-boxed-ir-v4` and `object-frames-native-v35`. The bounded owned core and
unchanged private kernel pass 10,752 .NET/native transcript comparisons on macOS
ARM64, with O0/O2 sanitizers and forced collection. The exact compiled/source-body
closure has no filters; the broader metadata audit retains its unresolved status.
[ADR 0177](decisions/0177-stage-six-completion.md) records final validation and
exclusions. The table below retains earlier checkpoint profiles and their limits;
it does not replace the current composed evidence above and at the end of this file.

Milestone 7a adds `embedding-int32-v1` through `EmbeddingCppEmitter`: generated C
header/shared-library linkage, persistent isolated instances, version checks,
stale runtime-token rejection, guarded entry/destruction and copied fault origins.
Only Int32 parameters/results cross the boundary; each artifact has one export
and a separate registry. Local macOS ARM64 C11/O0/O2 ASan/UBSan lifecycle and race
tests pass. See the [ABI contract](embedding-abi.md) and [ADR 0178](decisions/0178-persistent-scalar-c-abi.md).
This v1 boundary has no object/value transfer or phone-device support and does
not expand the managed semantics profile.

Milestone 7b adds `embedding-module-int32-v2`: 1..64 explicit public/static scalar
exports from one validated closure share each runtime's state. Stable IDs,
interface fingerprints, logical signature validation, capabilities and lowerable
heap/object/execution quotas have local C11/O0/O2 evidence. At 64 exports, the native
2 KiB frame check and concurrent admission tests pass. V1 remains supported.
Independent-root discovery and cross-program merging remain unimplemented;
[ADR 0179](decisions/0179-shared-export-module.md) records the precise boundary.

Milestone 7c.1 adds `embedding-object-handles-v3`: typed Int32/reference arguments,
Int32/reference/void results and explicit strong-handle retain/release. Handles
are runtime-owned nonreused leases; null is zero. Alias identity, type/owner checks,
collection and cycle reclamation, preflight handle quotas, fault cleanup and
release/invoke/retain races pass local C11 and O0/O2 ASan/UBSan checks. Both ordinary
object and composed exception emitters have native evidence. V1/V2 stay compatible.
String/64-bit/value copying, detailed exception messages, independent roots and
phone support remain planned. See [ADR 0180](decisions/0180-public-retained-object-handles.md).

Milestone 7c.2 extends v3 with optional UTF-16 capability bit 256. Exact String
alias binding and enabled feature selection participate in contract negotiation;
disabled contracts preserve their fingerprints. Import returns a retained managed
string; copy writes only caller-owned buffers, with length queries and no partial
writes. Local C11/O0/O2 ASan/UBSan evidence covers every UInt16 code unit, managed
length/index/concatenation, lifetime/quotas, collection and copy/import/release
races. No encoding normalization, interning or phone support is promised. See
[ADR 0181](decisions/0181-host-owned-utf16-copying.md).

Milestone 7c.3 extends v3 with `integer64` signatures and capability bit 512 when
wide parameters/results are exported. The unchanged tagged record carries every
bit; logical width is checked before lowering, while signedness remains a managed
operation rule. C11 O0/O2 .NET/native comparisons cover 2,704 boundary outcomes,
wide fields, mixed handle/scalar calls, malformed inputs and quota faults. Ordinary
and composed packet paths have sanitizer evidence. Structured value copying,
richer exception transfer and phone support remain planned. See
[ADR 0182](decisions/0182-exact-integer64-embedding.md).

Milestone 7c.4 adds optional exception capture (bit 1024) for composed managed-
exception emission. An escaping managed exception can be retained independently
of later calls and inspected through caller-owned UTF-16 type/message buffers.
Declaration identity and original throw origin are distinct copied records. Local
C11 .NET comparisons, O0/O2 ASan/UBSan lifetime/quota tests and copy/release/collection
races pass. Fatal failures may have no capturable managed object. Message formatting
can fail under resource limits; original copied faults remain available. Inner
traversal, stack traces, structured values and phone support remain planned. See
[ADR 0183](decisions/0183-retained-exception-capture.md).

| Component/profile | Implemented stage | Boundaries |
| --- | --- | --- |
| Metadata frontend | Inspection, IL structure, explicit binding and bounded audits | Depth-level validation and incomplete dependency reports; no execution guarantee |
| `explicit-library-contracts-v1` | Application dependency inventory with explicitly selected library boundaries | Exact identities/hashes, declarations/signatures/ancestry and inclusion reasons; library bodies/layout/initializers and indirect callbacks deferred; no implementation, intrinsic trust, compatibility or filter-absence claim (ADR 0088) |
| `explicit-library-type-bindings-v1` | Validated reference-to-implementation type plans and explicit structural type/member projection | Exact snapshots, names/nesting, header/generic attributes, immediate bases, member declarations and projected generic constraint sets; unmapped types/members fail; ordinary resolver/compiler binding unchanged; no executable-library or intrinsic privileges (ADRs 0089–0090, 0129) |
| Explicit library compilation view | Applies a validated mapping plan during ordinary compiler-library translation | Native scalar reference-call probe tested at O0/O2 UBSan; strict source remains unchanged; declared-owner lookup only; no new intrinsic grants; use the separate mapped audit API (ADRs 0091, 0124) |
| `explicit-library-implementation-closure-v1` | Explicit mapped audits with pinned type and reached member projections, constructed signatures and optional approved build digest | Implementation bodies/layout/initializers expanded; missing dependencies and exhausted budgets remain unresolved; 147 focused checks; actual required-library closure and execution compatibility remain separate (ADR 0124) |
| `cilica-library-intrinsics-v1` | Independently approved build attestations and exact string/exception type/member capabilities for mapped compilation | Output identities/hashes, source/compiler evidence and declaration-specific grants checked; existing shape checks remain; native string and compiler exception gates tested; no shipped library or general unsafe permission (ADR 0092) |
| `integer-arithmetic-ir-v1` | Explicit Int32/Int64 scalar IR with typed calls, locals, edges, arithmetic, comparisons and checked/unchecked integer casts | 19,584 arithmetic O0/O2 UBSan results match .NET; no heap/array/borrow storage or public ABI claim (ADRs 0093, 0095) |
| `integer-arithmetic-cil-v1` | Static nongeneric integer/Boolean/Char CIL closures with verified widths, narrow scalar storage and all integer conversion opcodes | 17,856 arithmetic, 9,408 conversion and 4,320 narrow-storage O0/O2 UBSan results match .NET; enum and heap/generic integration remain pending (ADRs 0094–0096) |
| `integer-managed-objects-cil-v1` | Explicit wide/narrow scalar CIL composed with class fields, nested struct copies/returns, closed generic scalar fields, struct constructors, direct class instance calls, boxing, synchronous borrows, nominal reference vectors and managed exceptions | 1,176 wide-object, 1,176 array, 1,470 constructor, 2,640 narrow-boundary and 588 instance-call O0/O2 ASan/UBSan results match .NET with forced collection and bounded frames; wide/value/narrow arrays, element borrows, enums and broader instance/generic dispatch remain pending (ADRs 0099–0104) |
| Closed interface variance | Exact closed identity, substituted inheritance and reference assignments in the explicit integer-object profile | Declaration positions and independent constraints validated; 1,176 repeated native O0/O2 ASan/UBSan results match .NET for nested variance and vector covariance; value arguments remain exact and variance-only dispatch remains pending (ADRs 0101, 0105, 0109) |
| `closed-interface-method-target-v1` | Metadata-only exact interface target selection on concrete closed class receivers | 48 mappings and authored calls match .NET with shared budget checks; exact scalar/reference CIL calls compose through ADR 0109; variance-only targets, generic methods, default bodies and array helpers remain pending (ADR 0106) |
| `closed-class-virtual-target-v1` | Metadata-only exact public class slots on concrete closed receivers | .NET callvirt/base-slot and shared-budget checks; scalar/reference composed CIL integration in ADR 0111; abstract hierarchies and class MethodImpl remapping remain pending (ADR 0110) |
| Closed class CIL dispatch | Scalar/reference/value class virtual calls and synchronous borrow parameters in the explicit integer-object profile | Scalar and value probe suites each supply 1,470 matching native results; abstract hierarchies, ref returns and Object runtime methods remain pending (ADRs 0111, 0114) |
| Same-assembly method access | Public/internal members with assembly-visible owner chains in the integer-object profile | Same image required; 588 matching native results and negative visibility/cross-image checks; broader protected and friend access remain pending (ADR 0116) |
| Closed generic memory | initobj/ldobj/stobj/cpobj for admitted integer, reference and value storage in the integer-object profile | 882 .NET-matching and 588 explicit expected native results; exact borrow elements and widths checked; narrow carriers remain pending (ADR 0117) |
| Class constructor values | Inline copies and synchronous borrows in integer-object constructors | 882 matching native results; immediate-base and receiver-escape checks retained; inline heap fields and broader constructor control flow remain pending (ADR 0118) |
| Closed reference boxing | box identity and unbox.any checked class/interface/vector casts in the integer-object profile | 1,470 matching native results, including exact interface cast messages; reference unbox borrows remain rejected (ADR 0119) |
| Direct struct instance calls | Nonvirtual methods/accessors over exact checked writable receivers in the integer-object profile | 1,176 matching native results; copied returns, mutation and empty receivers covered; readonly receivers and value dispatch remain pending (ADR 0120) |
| Direct interface-value bodies | Checked closed interface declarations retained for direct struct calls in composed integer-object scalar wrappers | 882 matching native results including runtime IEquatable; boxing these values remains diagnostic; exact constrained direct calls have a separate gate (ADRs 0121, 0125, 0127) |
| Byref-like-permitting declarations | AllowByRefLike generic flag accepted for supported ordinary arguments | Actual stack-only values/arguments rejected, including nested/unused arguments and marker-changing mappings; existing constraints and unknown-flag checks retained (ADR 0125) |
| `constrained-value-method-target-v1` | Metadata-only exact interface bodies and Object/ValueType overrides on closed ordinary values | 79 focused checks; preserves closed receiver/declaration/body and explicit mappings; boxing/default fallbacks and class/enum receivers remain pending (ADR 0126) |
| Constrained direct value CIL | Exact adjacent constrained./callvirt pairs over checked writable inline value receivers in the integer-object profile | 882 matching native results; no boxing; private explicit bodies across owners, readonly/primitive/reference receivers, mixed prefixes and broader generic constraints remain diagnostic (ADR 0127) |
| Exact interface constraints | Closed type/method constraints with substituted owner/method arguments, inherited exact interfaces and selected primitive identities | 588 additional matching native results for C# constrained helpers; primitive unmanaged proof only; class/dependent constraints, array helpers and variance-only conversions remain diagnostic (ADR 0128) |
| Invariant integer formatting APIs | Exact mapped Int32/UInt32/Int64/UInt64 ToString(IFormatProvider) with directly proven InvariantCulture | 8,976 code-unit/length comparisons plus cache-boundary/identity, collection and heap-borrow checks; [0, 300) shared cache; null/other/unproven providers, formats and parameterless APIs remain diagnostic (ADR 0134) |
| Owned read-only collection contracts | Exact mapped IReadOnlyList/IReadOnlyCollection and generic/nongeneric enumeration with IDisposable | 1,764 native results for application collections; preserves inheritance/variance declarations; List storage, array interface helpers and variance-only dispatch remain pending (ADR 0135) |
| Default readonly static reads | Exact initonly ldsfld without type/module initialization or explicit storage | 588 native comparisons of scalar/reference/inline defaults and independent copies; mutable/initialized storage, addresses and writes remain diagnostic (ADR 0137) |
| Integer instance bodies and hashing | Exact selected Int32/UInt32/Int64/UInt64 ordinary managed instance bodies over checked scalar borrows | 294 new native results for the four mapped GetHashCode members; direct calls only, no primitive boxing, constrained calls, layout fields or narrow primitive receivers (ADR 0133) |
| Invariant provider identity | Exact selected CultureInfo.InvariantCulture, IFormatProvider assignment and stable per-instance identity in mapped core builds | 294 new matching native results; permanent traced cache, heap/ownership checks and exact grants; general culture members and GetFormat remain unsupported; numeric APIs are scoped separately (ADR 0132) |
| Managed decimal formatting trial | Pinned-source Cilica.Library helpers for invariant Int32/UInt32/Int64/UInt64 decimal text | 8,192 managed text comparisons and 8,976 repeated native sanitizer results; no System member mapping, providers, precision or string-cache identity claim (ADR 0122) |
| Library build evidence producer | Explicit local Roslyn invocation, input snapshots/hashes, output identity/hash and requested exact intrinsic declarations; optional zero-reference core profile | Bounded process, strict request selection and failure cleanup; trusted build host assumed, no automatic approval or capability admission (ADRs 0123, 0130) |
| Cilica.Core bootstrap | Owned zero-reference image with pinned-reference string/exception, provider, integer-hash and bounded numeric mappings | Eight probes supply 2,352 repeated native results; 65 requested grants require independent approval; broader APIs and kernel execution remain pending (ADRs 0131–0134) |
| Composed String operations | Selected UTF-16 indexer, two-string Concat and inequality in the integer-object profile | 1,176 repeated native sanitizer results match .NET, including identity and fault order; other overloads remain unsupported (ADR 0112) |
| Selected argument exceptions | Argument/argument-null/range string constructor forms, ParamName and Message; invalid-operation/not-supported defaults in the integer-object profile | 1,470 repeated native results match .NET; parameter tracing and allocation-failure checks; inner exceptions, actual values and localization remain pending (ADR 0113) |
| Closed interface plans | Explicit `ObjectInterfacePlanBuilder.BuildClosed` metadata preparation | 42 mappings match .NET interface maps with exact specialization/signature and bounded-work checks; feeds exact scalar/reference CIL discovery (ADRs 0108, 0109) |
| Composed interface IR | Explicit scalar/reference/value signatures and synchronous borrow parameters in the integer-object value/delegate/exception pipeline | Scalar and value probe suites each supply 1,176 repeated O0/O2 ASan/UBSan results matching .NET; detached value layouts and rooted return destinations are checked; ref returns, variance-only dispatch and array helpers remain pending (ADRs 0107, 0109, 0115) |
| Closed interface CIL | Exact scalar/reference interface calls in `integer-managed-objects-cil-v1` | 1,176 repeated O0/O2 ASan/UBSan results match .NET across closed targets, wide calls, reference identity and caught faults; abstract-class/variant/struct dispatch and array helpers remain pending (ADR 0109) |
| Array interface boundary | Metadata-backed catalogs diagnose selected Array interfaces and generic vector contracts | Prevents unsupported array type tests from silently returning false; exact runtime identities and bounded ancestry checks preserve application lookalikes (slice 6c.16a) |
| `class-virtual-target-v2` | Exact metadata target resolution | One public nongeneric class virtual slot/body per concrete receiver, with newslot/final checks and validated interface-only MethodImpl rows; executable calls use the separate object profile |
| `interface-method-target-v1` | Exact metadata target resolution | Public nongeneric abstract interface declarations mapped to one concrete class body, including inherited/reimplemented/explicit mappings |
| `int32-pending-fault-v1` | Typed IR, C++20 emission and local native differential execution | Explicit static MethodDef and complete direct-call closure; nongeneric owner/method; Int32 parameters/return; initialized Int32 locals; typed block inputs; division/remainder/checked-arithmetic faults; no initializer obligations |
| C++ backend | C-linkage status entry, plus value-only entry for proven pure programs; private helpers, hash/token identity, IL comments and scoped block labels | Primitive profiles retain numeric faults; managed exception objects/handlers use the opt-in exception profile; public IR is validated before emission |
| `object-layout-v6` | Bounded nominal/vector/String metadata and detached catalogs | Classes/interfaces/vectors and the selected sealed String share the 256-type limit; String uses an intrinsic shell, without framework layout or interfaces |
| `delegate-values-ir-v2` | Authored single-target delegate contracts and source root plans | Exact accessible static/instance targets; Int32/void and, in integer-object composition, reference parameters/results (AC2.1); native storage/invocation through the scalar wrapper; CIL via explicit profiles; no multicast, variance, open-instance or closed-static binding |
| `delegate-values-cil-v2` | Public sealed nongeneric or exactly closed generic single-target delegates | Owned Action<int,int> and closed application probes; Int32 parameters and Int32/void returns; exact casts/type tests in integer-object composition; broader signatures and variance conversions remain diagnostic (ADR 0136) |
| `closed-generic-values-cil-v1` | Closed static methods, struct and concrete class layouts/constructors, native specialization and local O0/O2 execution | Scalar/reference/value closures; static closed owners; nested generic fields, struct TypeSpec operations and exact boxes; class/struct/default-constructor constraints; generic dispatch, broader constraints, inline heap struct fields and primitive boxing remain diagnostics |
| `value-layout-v2` | Metadata discovery, detached inline layouts and bounded copy/reference-path planning | Nongeneric sealed structs with Int32, included references and nested values; exact selected ValueType identity; executable copies use the separate scalar-lowering profile |
| `typed-value-ir-v1` | Detached value copies, field operations, calls/joins and root plans | Exact struct identity; scalar/reference copy paths; 1,024 roots per method and 65,536 expanded scalar slots per program; managed locations and CIL use the separate wrapper/translator |
| `managed-location-ir-v1` | Detached storage locations, synchronous borrowing and owner-root plans | Frame local/parameter and heap-field origins, nested fields, invariant element types, readonly propagation, loads/stores/init and direct void mutators; no escaping locations or byref locals/joins; value native emission and CIL use separate lowering/translation profiles |
| `scalar-location-ir-v1` | Authored Int32/reference borrows with existing native object operations | Explicit `EmitLocations` entry, synchronous direct static borrowers, readonly views, owner roots, checked carriers and O0/O2 allocation/collection stress; inline values require the separate expansion profile; CIL byrefs use the separate value translator |
| `value-location-scalars-v3` | Mutable inline values and caller-owned struct returns expanded into native scalar borrows | Authored nested local/by-value parameter mutation, snapshots, readonly views, rooted return destinations and embedded reference slots; authored IR and bounded value CIL; bounded composed heap fields are described by ADR 0139; no public struct ABI |
| `value-arrays-ir-v1` | Inline copies/borrows and vector operations composed before scalar lowering | Authored IR validation, allocation-root planning and rooted return packets; direct static methods only; class/string allocation excluded |
| `value-objects-ir-v1` | Authored value/class-construction composition | Shared constructor/access/readonly verification, allocation roots and scalar lowering; static value methods and scalar/reference class constructors only |
| `value-arrays-cil-v1` | Static struct programs composed with Int32 vector CIL allocation/access | Compiler-library opt-in; selected Int32 identity, exact array operands, live embedded roots through calls/borrows/returns, local O0/O2 collection stress and failure cleanup; no class allocation composition |
| `value-objects-cil-v1` | Static struct programs composed with class construction and fields | Compiler-library opt-in; shared constructor/access rules, scalar/reference constructor bodies, inherited fields and scalar heap borrows; local O0/O2 live-graph collection/failure evidence |
| `boxed-value-ir-v1` | Detached named-struct boxing and exact unbox copy/borrow contracts | Authored IR validation, logical payload paths and allocation/owner roots only; no native lowering or CIL boxing |
| `boxed-payload-runtime-v1` | Private native boxed storage and checked copy/borrow helpers | O0/O2 identity, mutation, fault-status, collection and heap-budget probes; boxed IR lowering/emission and CIL remain unsupported |
| `scalar-boxed-ir-v2` | Authored boxing IR expanded into checked scalar packets and payload locations | Explicit `LowerBoxes`/`EmitBoxes`; O0/O2 copy/mutation, empty borrowed-owner, collection, fault-origin and arena-bound probes; no CIL boxing |
| `boxed-values-cil-v1` | Named nongeneric struct box/unbox/copy CIL | Compiler-library opt-in; exact types/access, synchronous mutable payload borrows, C# empty structs, local O0/O2 graph/copy/fault evidence |
| `value-cil-v1` | Static value CIL translated through existing value/location IR and native lowering | Compiler-library API; exact joins, synchronous Int32/value borrows, whole copies, field operations and struct returns; O0/O2 local sanitizer conformance; no allocating source value programs or CLI profile change |
| `value-scalars-v2` | Pure inline copies lowered to the existing scalar/reference backend | Local O0/O2 sanitizer comparisons for authored IR copies, nested updates and scalar-returning calls/joins; packed Int32 entry; no struct return packets, CIL value translation, boxing or live embedded-reference collection evidence |
| `typed-reference-ir-v5` | Detached caller-constructed IR validation | Class/interface/array/String references/null, ancestry, reference-vector covariance and typed flow; String metadata/signature flow and bounded native storage are implemented |
| `interface-dispatch-plan-v1` | Bounded metadata-backed dispatch preparation and detached validation | Bodyless interface signatures, class method facts and complete receiver maps; CIL bodies/access/calls and native execution remain separate |
| `object-operations-ir-v10` | Bounded nominal/vector/String CIL and selected integer instance methods | Existing rooted string/array operations plus invariant provider contracts/cache operations and exact Int32/UInt32/Int64/UInt64 scalar receivers; integer hashing/formatting use ordinary managed code (ADRs 0132–0134) |
| `object-frames-native-v23` | C++20 emission and local native differential execution | v20 capabilities plus the bounded numeric string cache (ADR 0134); invariant-provider root/cache (ADR 0132); traced argument-exception parameter payloads (ADR 0113); explicit Int64 class fields, frames, calls, checked borrows and inline/boxed payloads; wide CIL uses the explicit integer-object API; wide arrays remain pending (ADRs 0097–0099). Existing UTF-16 strings, Int32/reference vectors, class/interface dispatch, bounded collection, handles and managed exceptions retain their documented limits |
| `managed-exceptions-cil-v1` | Bounded CIL catch/finally/fault, throw/rethrow, pure type-test filters and native execution | Selected exception shells, verified fieldless custom exceptions and forwarding constructors, messages/literals, runtime fault types and contextual class-cast/unbox messages; composes with closed generics/boxes/delegates; general filters, virtual exception APIs, localization and interface-target cast messages remain diagnostic |
| Broader managed runtime and library | Planned | General arrays beyond the bounded vector profile, broader String APIs, advanced GC features, general filters, broad library and public embedding support are unimplemented |

## Int32 pending-fault profile

| IL | Translation contract |
| --- | --- |
| `nop` | No executable effect; other operations retain their original IL locations |
| `ldarg.0`–`ldarg.3`, `ldarg.s`, `ldarg` | Verified parameter index; Int32 load |
| `ldc.i4.m1`, `ldc.i4.0`–`ldc.i4.8`, `ldc.i4.s`, `ldc.i4` | Exact signed 32-bit constant |
| `add`, `sub`, `mul` | Ordered operands; explicit wrapping arithmetic modulo 2^32 |
| `add.ovf`, `sub.ovf`, `mul.ovf`; corresponding `.un` forms | Signed/unsigned Int32 bit interpretation; overflow fault when the exact mathematical result is outside the selected 32-bit range; checked before narrowing |
| `and`, `or`, `xor`, `not` | Exact 32-bit bitwise operations; `not` consumes one operand |
| `shl`, `shr`, `shr.un` | Int32 value/count; count masked with 31; low 32-bit left shift, explicit sign extension or zero-fill right shift |
| `div`, `div.un` | Signed quotient truncates toward zero; unsigned quotient uses operand bit patterns; zero divisor faults; signed MinValue/-1 faults with overflow |
| `rem`, `rem.un` | Signed nonzero remainder has the dividend's sign; unsigned remainder uses operand bit patterns; zero divisor faults; signed MinValue/-1 faults with overflow in the selected profile |
| `ldloc.0`–`ldloc.3`, `ldloc.s`, `ldloc`; corresponding `stloc` forms | Verified Int32 storage index; zero initialization required when locals exist; stores retain order |
| `dup`, `pop` | Verified Int32 stack duplication/discard |
| `ceq`, `clt`, `clt.un`, `cgt`, `cgt.un` | Int32 result 0/1; explicit signed/unsigned comparisons |
| `br`, `br.s`; `brtrue`, `brfalse`; `beq`, `bne.un`, `blt`, `bgt`, `ble`, `bge` and applicable `.un`/`.s` forms | Explicit branch edges; exact instruction targets; matching Int32 stack inputs at each join/backedge |
| `call` | Exact scoped declaration; nongeneric static Int32 signature; already-evaluated arguments in signature order; verified complete closure; immediate propagation after may-fault calls |
| `ret` | One Int32 value and empty remaining stack at every reachable return |

Unsupported signatures/operations return diagnostics and no IR or C++ source. This includes
conversions, unary negation, switches, virtual/indirect calls,
argument stores, references,
exception regions, required type/module initialization and synchronization.
Boolean, pinned, non-Int32 and uninitialized locals are unsupported. Unreachable
blocks are conservatively rejected. Loop/recursion termination is not proved;
managed stack exhaustion is unimplemented.
Reference-assembly and native/runtime bodies cannot establish execution support.
Bounds and ownership follow [ADR 0014](decisions/0014-straight-line-int32-ir.md)
and [ADR 0016](decisions/0016-int32-control-flow.md), extended by
[ADR 0017](decisions/0017-bounded-static-call-closure.md). The current program profile extends
the control-flow IR data shape; this is an early evolving API, not a stable IR
serialization or embedding ABI. Prior arithmetic inputs retain their semantics.

Calls accept members of the same declaring type (excluding compiler-controlled
PrivateScope), or public methods on externally visible types. Internal, family,
friend-assembly and inherited MemberRef owner rules are conservatively unsupported.
Reference the exact declaring type; required type/module initialization still fails.
Program traversal is iterative, includes each content-hash/MethodDef identity once,
and limits closures to 256 methods, 1 MiB IL and 65,536 instructions. Existing IR
limits also apply across the program: 4,096 blocks/parameters/locals and 65,536
statements/block inputs/edge arguments. Call argument copies have their own 65,536
bound. Declaring-type snapshots are bounded to 64 levels by default.
`Translate` retains the call-free method API; `TranslateProgram` and `EmitProgram`
handle complete closures. Caller IR with missing targets, invalid arity/operands,
duplicate identities, unreachable methods or excess resources is rejected.

Bitwise/shift semantics extend the same program profile; see
[ADR 0018](decisions/0018-int32-bitwise-and-shifts.md). Counts are masked to five
bits even for raw IL without a C# mask. This is Cilica's defined choice consistent
with C# and the tested .NET runtime; CIL leaves oversized counts unspecified.
All native shifts use unsigned 64-bit operands with bounded counts; signed right
shift supplies sign bits explicitly. Unary complement uses a new validated IR
expression.

[ADR 0019](decisions/0019-primitive-pending-faults.md) adds explicit method-exit
failure edges to divisions and calls. Caller-built IR must provide these edges;
pure definitions must not carry one. The profile name changes from
`int32-static-calls-v1` to `int32-pending-fault-v1`; prior pure programs retain
their value-only native entry, with an additional status entry. A conservative
closure analysis propagates may-fault effects through recursive cycles and omits
checks only for proven pure callees. Each native entry owns a fresh stack context;
the first fault leaves immediately and retains the originating image hash,
MethodDef and IL offset across callers. This context has primitive fault tags,
not managed exception objects or messages; catch/filter/finally require the separate
managed-exception profile.
Object identity/rooting are supplied only by the separate object profile below. The CoreLib/Alchemy filter gate is unresolved.

[ADR 0020](decisions/0020-checked-int32-arithmetic.md) adds six checked arithmetic
operations with the same method-exit edges, pending tag and native status entry.
Signed add/subtract/multiply use exact Int64 intermediates and signed range
checks. Unsigned add/multiply use UInt64; unsigned subtraction uses Int64 and a
0..UInt32Max range check. Conversion to UInt32 happens only after a successful
check. Unsigned results above Int32Max retain their bits without a spurious fault.
Shared operation classification keeps translation, edge validation and effect
analysis consistent. The IR shape/profile name and ABI are unchanged.

[ADR 0021](decisions/0021-int32-remainder.md) adds signed/unsigned remainder on
the existing pending-fault path. Signed operands widen to Int64 before `%`;
unsigned operands widen to UInt64. Zero is guarded before native evaluation.
Signed MinValue/-1 explicitly raises Overflow, independently observed in checked
C#, unchecked C# and raw authored IL on .NET 10.0.11 ARM64. This is a selected
profile contract, not a universal CLI-platform claim. Other signed remainders
follow truncation-toward-zero division, retaining the dividend's sign when nonzero.
Unsigned MinValue-bit-pattern rem -1-bit-pattern succeeds without signed overflow.

The `_try` C entry returns UInt32 status: 0 success, 1 divide-by-zero, 2 overflow,
3 null output pointers. Its four outputs precede Int32 inputs: Int32 result,
UInt32 MethodDef, UInt32 IL offset, `const char*` image SHA-256. Valid disjoint
storage is required; null outputs cause no execution or writes. Faults leave the
result unchanged. Successful calls zero the origin numbers and set image to null.
Origin strings borrow the generated library lifetime. `CppArtifact.EntryPoint`
selects this entry when `MayThrow` is true; `FaultEntryPoint` always names it.
No value-only wrapper is generated for a may-fault root. This convention is not
the stable managed embedding ABI.

Current native evidence: .NET SDK 10.0.400 / runtime 10.0.11 and Apple Clang
17.0.0 (`clang-1700.4.4.1`), target `arm64-apple-darwin24.6.0`. Thirty-two authored
C# entry methods and five reduced IL probes produce 16,252 native results per build,
checked against authored .NET results and the independent zero-local expectation
at `-O0` and `-O2`; the latter also enables `-fsanitize=undefined` and
`-fno-sanitize-recover=all`. Cases include signed extremes, overflow/underflow,
multiplication truncation, compound operand order, negative constants and an
unused parameter, signed/unsigned comparisons, both branch outcomes, joins, loop
mutation, live stack values across backedges, shared helpers, cross-assembly calls,
argument capture before local mutation, bounded direct/mutual recursion, bitwise
composition and shifts in calls/loops, using 100 boundary combinations and 256
seeded input tuples per method. Seven shift probes additionally use ten boundary
values and 44 counts (0..33, negative/wrap boundaries and Int32 extremes).
C# loop/recursion counts are bounded to -32..32;
the reduced stack-loop probe uses 0..31. Raw shift references run independently
authored .NET DynamicMethods with no explicit count mask; no input bytes are
loaded or executed by the compiler. The zero-local probe has expected result 0.
Process timeout/output-limit termination and cleanup also pass. Reduced tests
reject forged IR and verify CLI failures preserve existing output files.
See [ADR 0015](decisions/0015-int32-cpp-emission.md) and
[ADR 0016](decisions/0016-int32-control-flow.md).

The separate fault harness checks 37 authored entries and 13,986 outcomes per
configuration (100 boundary combinations, 256 seeded tuples, 22 ordered/boundary
regressions). Direct signed/unsigned division/remainder and six checked arithmetic forms,
transitive/cross-assembly calls, competing fault order, loops, bounded recursion,
skipped conditional faults, signed/unsigned compound operations and
pure status entries match .NET return values or exception kinds. Origin hashes
and MethodDef tokens are independently computed from the authored fixture, with
expected arithmetic offset 2, or 2/4 for two-operation compounds. First-fault
origins are checked even when both competing failures have the Overflow tag.
Product inputs straddle signed and unsigned limits, including products beyond
Int64Max; unsigned input casts are unchecked to avoid testing unsupported
conversions. Repeated invocations verify context reset and unchanged
results on fault; all four null-output cases reject before execution/writes.
Both builds disable C++ exceptions/RTTI; optimized execution retains UBSan.
Output is batched within the existing process limits. These tests establish the
selected profile's `Overflow` tag for signed MinValue/-1 on the recorded runtime,
not a universal exception-object contract for every CLI implementation.

Eighteen explicit remainder contract cases pin expected values/faults against
independently authored raw-IL DynamicMethods and checked/unchecked C# references.
The native harness adds checked/unchecked signed and unsigned remainder, helper
and cross-assembly propagation, loops/recursion, conditional avoidance, competing
checked-arithmetic faults and chained signed/unsigned remainder at offsets 2/4.
All sign combinations, zero divisors, MinValue boundaries and unsigned results
above Int32Max are covered. The existing 50-tuple batches preserve capture limits.

These are local conformance observations for this subset and toolchain. Other
native platforms, WebAssembly and the later runtime/embedding ABI are untested.

The [primitive example](../examples/Primitive/README.md) reproduces the complete
C# → IL → external Cilica CLI → C++ → native path with four fixed probes. Debug
and Release runs match .NET for a loop/helper result, overflow at the helper's
IL offset 2, success immediately after that fault, and an empty loop avoiding
the fault. The bounded smoke test also uses an output path containing spaces.
The example derives identifiers from its own built kernel; only its separate
authored reference helper executes .NET code. This closes milestone 2 for the
documented subset without expanding the profile or platform claims.

## Object layout foundation

`ObjectModelBuilder.Build` takes one resolved type and an explicitly selected
runtime library, then resolves a complete base-class, field and interface graph. Types use content
hash/TypeDef identity; fields retain that declaring identity and their image-scoped
FieldDef tokens. Logical reference maps distinguish reference fields from Int32.
The immutable output survives reader disposal; input signatures remain
resolver-owned until the build completes. Ordinary layout fields accept Int32,
named CLASS or primitive OBJECT; self/mutual reference cycles are supported.

Classes are concrete, nongeneric and automatic-layout, with acyclic base chains
ending at the selected fieldless `System.Object`. A base must be present and
unsealed; sealed leaves are permitted. Fields and `ReferenceSlots` describe only
declarations on that class, without copying inherited fields or losing their
original owner identities. Hidden fields therefore remain distinct. Enclosing
generic owners are rejected. Public nongeneric interfaces have separate fieldless
contract records and parent edges, without a class base or native layout. Classes
retain their declared interface edges; inherited contracts follow base links.
Interface roots and interface-typed fields include their transitive parents.
Abstract classes, nonpublic interfaces, generic inheritance,
packing/native layouts, initialization, reference
assemblies, static/literal/constant/RVA/marshalled/explicit-offset fields and
unsupported signatures fail with no partial model. Readonly and visibility flags
are retained for future access verification; layout inclusion does not grant
executable access. This is not a general CLI verifier or intrinsic classifier.

Bounds: 256 included types, 4,096 aggregate fields, 65,536 metadata inspection
units and at most 64 enclosing types. Class and interface identities share the
256-type limit. Direct interface lists are separately bounded to 256 entries and
each row consumes shared metadata work. Field signatures share a 4,096-node decoder
budget per type; base/interface decoding is separate. The model validator rejects forged
identities, duplicate FieldDefs across owners, missing targets, unrelated types,
invalid storage/base shape and aggregate excess. Independent fixtures verify
exact/excess limits, forwarding, cross-image namesakes, ownership and detached
lifetime. The 256-type limit includes ancestors, bounding base-chain checks and
cached ancestry relationships to at most 256 squared entries. Inheritance
validation is iterative. See [ADR 0022](decisions/0022-detached-object-layouts.md)
and [ADR 0030](decisions/0030-detached-class-hierarchies.md), extended by
[ADR 0036](decisions/0036-interface-catalogs-and-reference-flow.md). Catalogs
validate storage and type edges, not method signatures, bodies or implementation maps.

The layout model alone supplies no execution. Target layouts, allocation, ownership,
root frames and native object faults require the separate object emitter below;
collection remains unimplemented. The existing Int32 translation profile, emitted
C++ and native status ABI retain their semantics. `IrType` is now a record carrying a kind and optional concrete class
or interface identity; this early compiler API is not a stable serialization contract.

## Typed reference value-flow foundation

`ReferenceIrProgram` is a detached validation API, not a CIL translator or native
entry. It shares block-local values and ordered statements with the primitive
IR. `ReferenceIrValidator.Validate` checks both the complete method closure and
an `ObjectTypeCatalog` with explicit class/interface/vector roots and their base/field/contract/element closure. Catalogs
use the same layout rules and aggregate bounds as `object-layout-v4`; every
included type must occur in declared value/storage types or their base/field/contract/element closure,
apart from the selected object base. Class names do not supply identity.

`IrType` distinguishes Int32, an exact class/interface/vector reference and the null stack type.
Parameters, locals and returns require Int32 or declared reference types; null may
occur in temporaries and block inputs. Loads and call results retain exact
declared types. Assignments, returns, arguments and edge copies accept exact
types, null into any reference, a concrete class into any ancestor class or
implemented interface, and an interface into any parent interface or Object.
Inherited contracts and diamond ancestry are transitive; an interface cannot
implicitly narrow to an implementing class or unrelated interface.
Downward and sibling-class conversions require explicit casts in the
object-operation profile. Interface casts/tests use exact contract membership in
that profile. One ancestry cache is shared across a program's method validation,
rather than rebuilt per method.
Validation checks declared joins; it does not infer their common type from CIL.

Detached arrays use structural identities anchored to the selected Object plus
Int32 or a nominal reference element. Reference-vector covariance follows nominal
element assignability; Int32 vectors are invariant, and every vector widens to
Object. Array entries, classes and interfaces share the 256-type cap. Array fields
retain logical reference slots and include their element's nominal closure.
Jagged/multidimensional arrays, other value-type elements, System.Array and array
framework interfaces remain outside this contract.

Reference equality/inequality returns Int32; reference/null truth branches are
permitted. Ordered reference comparisons, reference arithmetic, mixed
integer/reference operations and forged class identities are rejected. Calls
retain explicit method-exit fault edges; primitive arithmetic keeps its existing
fault rules. Iterative closure validation and existing per-method/program limits
also cover reference values and argument copies.

This API alone validates value flow: the object-operation profile below adds
method bindings, constructor/field operations and their access checks. Native
object lowering is supplied by the separate bounded profile below. Phase A root
assignment accepts interface references in validated object-operation IR.
The operation/native profiles now lower inherited fields and immediate-base
constructor chains, class casts and bounded class virtual calls. CIL stack inference
retains a unique most-specific shared class or interface after catalog discovery
([ADR 0042](decisions/0042-reference-join-inference.md)); ambiguous intersections
require explicitly typed locals or casts before the join.
Caller-constructed sources do not authenticate metadata declarations. Void
signatures are accepted only through the object-operation profile; they remain
invalid as value/storage types. The primitive C++ emitter rejects reference/null
signatures,
storage and values before returning an artifact. Internal reference-returning
IR does not establish a native reference ABI. See
[ADR 0023](decisions/0023-typed-reference-value-flow.md).

## Object operation translation

`ObjectTranslator.Translate` takes an explicit entry MethodDef and selected
runtime library. Its `ObjectIrProgram` retains complete method closure, a shared
class catalog and detached method bindings. `ObjectIrValidator` validates these
before consumption; caller bindings are structural assertions, not authentication
of arbitrary metadata. The entry has only Int32 parameters and result; references
stay internal. Instance signatures include their exact declaring receiver as
parameter zero. Other parameters, locals and results use Int32 or concrete class
references; void is a return signature only. Locals require initialization.

| IL/operation | Translation contract |
| --- | --- |
| `newobj` | Exact instance constructor; already-evaluated arguments in declaration order; distinct typed allocation definition with method-exit fault edge |
| `ldfld`, `stfld` | Exact FieldDef/declaring owner via direct or MemberRef binding; typed receiver/storage; ordered load/store and method-exit fault edge |
| `castclass` | Named concrete class or public nongeneric interface target; null stays null, compatible references retain identity, mismatch reports invalid-cast status 7 |
| `isinst` | Named concrete class or public nongeneric interface target; compatible references retain identity, null/mismatch return null; result has the target reference type |
| `call` | Exact ordinary static/instance declaration, including virtual declarations; explicit receiver for instance calls; direct body invocation without an automatic null-receiver check |
| nonvirtual `callvirt` | Ordinary nonvirtual instance declaration; explicit null-receiver check after argument evaluation |
| virtual `callvirt` | Public nongeneric concrete class declaration; exact canonical slot and complete receiver map; null/ownership/ancestry checks after argument evaluation; nearest effective body |
| void calls, `ret` | Separate invoke statement and void terminator; no void temporary or storage |
| `ldnull`, reference `ceq` | Null stack type; identity equality produces Int32 |
| reference `cgt.un` against null | Explicit identity inequality; other ordered reference comparisons are rejected |
| argument/local/constant loads, local stores, `dup`, `pop`, supported Int32 operations | Preserve the existing operand order, type rules and arithmetic fault edges |

Constructors are named `.ctor` definitions with instance/void signatures and
both constructor flags. Each constructor calls its immediate base exactly once,
using a direct parameter-zero receiver and assignable evaluated arguments.
Before that call, its own declared fields may be initialized and independent
arguments evaluated, including calls/allocations. Reading, aliasing, comparing or
passing the unfinished receiver, or writing inherited fields before the call,
is rejected. Initializers and argument faults retain their IL order. Same-owner
constructor delegation, skipped/repeated bases and readonly receiver aliases
remain unsupported. The selected Object constructor must be nongeneric ordinary managed IL
with no parameters, locals, handlers or nontrivial body beyond `nop`/`ret`; its
owner must be the exact explicitly mapped object base. No name-only intrinsic
substitute or host runtime probing is used.

Methods allow same-declaring-owner access or public members on externally visible
owners. Fields allow same-owner access (excluding PrivateScope), or public fields
on top-level public classes. Cross-owner nested-type field access, family/internal/
friend rules and inherited MemberRef owners remain conservatively unsupported.
Readonly stores require the declaring constructor and a direct parameter-zero
receiver. Reference field stores accept null or an assignable concrete reference.
Type/module initialization, reference-assembly/native bodies, generics,
abstract classes, broader MethodImpl remapping, method-return covariance, prefixes,
broader String APIs, byrefs and storage outside the bounded vector profile fail
without partial programs. Ordinary methods support branches/backedges with inferred
typed inputs, described below. Constructors remain straight-line; unreachable
blocks and unsupported operations/transfers are rejected.

Existing aggregate IL/method/IR bounds remain. Member binding, initializer scans
and cached type snapshots share a 65,536-unit metadata work ledger; repeated token
bindings are cached. Budgeted member-owner formatting and method snapshots have
a hard 64-level owner ceiling, including generic enclosing owners. Allocation
arguments and void receiver/argument copies share the 65,536 call-copy limit.
Multi-root catalogs retain the 256-type/4,096-field aggregate limits even when
snapshots were cached before catalog construction.
Native composition separately caps physical field copies across all included
concrete layouts at 65,536. This prevents inheritance from multiplying a valid
4,096-declaration catalog into unbounded generated storage/offset tables.

Evidence: the authored `ProbeNode` class creates two objects, initializes readonly
fields, mutates an Int32 field, connects reference fields and invokes void/instance
methods. A bounded test-only IR evaluator agrees with .NET on normal and wrapping
results, plus a null field fault. Explicitly authored raw IL oracles verify direct
null calls versus nonvirtual `callvirt` and null identity comparisons. These are
translation checks, not a native heap, rooting or GC experiment. The Int32 native
corpus remains the backend evidence. No object CLI command or native object ABI
is exposed. See [ADR 0024](decisions/0024-straight-line-object-operations.md).


## Native object checkpoint

`ObjectCppEmitter.EmitProgram` initially implemented `straight-line-objects-native-v1` from
revalidated `ObjectIrProgram`; `ObjectRootAssignment.Assign` supplies the separate
phase A plan. This compiler API emits C++20, without executing input assemblies
or launching a compiler. The CLI still exposes only the Int32 native profile.
Supported object operations above now run in a private instance-owned heap with
concrete reference parameters/locals/returns, zeroed fields, constructor and
void calls, identity comparisons and explicit null/allocation faults.

The C entry accepts/returns Int32; it creates a fresh context/heap per invocation.
No references, retained handles or persistent runtime cross that boundary. Output
storage must be valid and disjoint. Null outputs return 3 without writes; faults
leave the result unchanged and report image/MethodDef/IL origin. Statuses retain
1 divide-by-zero and 2 overflow, add 4 null, 5 allocation failure, 6 ownership/type
violation, 7 invalid reference cast and 8 call-depth limit. Execution storage adds
status 9 below. Bounded array operations add status 10 for bounds and 11 for element
type mismatch. These tags are not managed exception objects.

All reference arguments, locals and temporaries receive distinct slots, including
null temporaries. Frames publish before allocations/calls and pop on every exit.
New objects are rooted before constructor entry; reference call results enter a
caller slot before any later allocation. Field maps derive from checked native
`sizeof`/`alignof`/`offsetof`, independently of method roots. Membership is checked
before reading supplied references, and foreign heaps/types fail explicitly.

Limits: 1,024 roots per method, 64 active calls, 1 MiB of object storage and 4,096
allocations per invocation, plus existing object/IR budgets. The initial profile
required one block with no stack inputs; the control-flow expansion below now
provides native jumps/branches and typed inputs. Excess frames fail before source
is returned. The heap retains objects until
teardown; no GC, collection stress, phase B, performance, stable embedding,
exception-handler, class-library or broader platform claim follows.

Evidence is local macOS ARM64, Apple Clang 17, .NET 10.0.11/SDK 10.0.400. Seven
authored C# entries compare 1,463 result/fault cases per O0/O2 configuration under
ASan/UBSan, checking both private helpers and the C boundary. Allocation observers
inspect caller/callee/returned references and children retained through fields;
ownership, invalid pointers/roots, zero fields, size/alignment/reference maps,
allocation limits, constructor failure, frame cleanup and teardown have native
checks. Authored raw IL covers null calls/comparisons, identity and integer
self-comparison, plus a native-only bounded recursion check. Exact fault origins
are checked against the validated operation's IL offset; .NET supplies the
independent result/fault-kind oracle. See
[ADR 0025](decisions/0025-native-object-ownership-and-roots.md).


## Reference control-flow expansion

`object-control-flow-native-v1` expands ordinary object methods to the supported
Int32 branch family and reference truth/null/identity branches, with typed stack
joins and backedges. The primitive emitter remains Int32-only and retains its
status ABI. The object entry still accepts/returns only Int32; frame/heap ownership,
fault tags, teardown and limits remain those of the initial checkpoint.

Null joins a concrete reference; distinct references retain a unique most-specific
common class/interface, or Object when it is their only common type. Equal types
remain exact. Ambiguous intersections, depth mismatches, integer/reference joins
and narrowing an Object input into a class consumer fail without partial IR.
Inference revisits blocks until types converge, before complete operation validation.
Common-ancestor sets propagate intact through stack inputs until convergence;
selection therefore does not depend on predecessor order. Explicit producers retain
their static types and a real Object source cannot be narrowed into an interface.
Unreachable blocks, ordered reference comparisons, switches, leave/handlers,
argument stores remain errors. Constructors still require a single
block with one immediate-base call under the initializer rules above.

A separate 262,144-unit closure budget counts instruction visits, input materialization,
argument copies and edge slot visits plus one per edge. Reference refinement shares
that ledger for block visits, set operations, type selection and rewriting.
Cached call sites charge
call-copy limits once; cached field snapshots have token indexes. Existing aggregate
metadata/IR limits also apply. Root assignment includes block inputs and `(block,
value)` temporaries within the same 1,024-root limit per method. Distinct source and
input slots preserve parallel edge transfers, including reference swaps. Integer
inputs live at method scope; references alias root slots. Block temporaries have
C++ scopes, and all jumps follow frame publication. No termination proof or new
collection/liveness claim is made.

Evidence: seven authored C#/raw-IL entries compare 2,093 .NET result/fault cases at
each O0/O2 configuration under ASan/UBSan on the previously recorded local ARM64
toolchain. They cover linked-object traversal, nullable selection, sibling-class
widening, identity, allocating stack swaps and arithmetic fault exits/origins.
Observers inspect original references during loop allocations; allocation-budget
failure pops the frame. Compiler fixtures cover convergence, invalid/widened uses,
inference bounds and preserved rejection boundaries. See
[ADR 0026](decisions/0026-reference-control-flow.md).

Common-reference inference adds 210 .NET/native comparisons per O0/O2 build,
including parent-interface joins, sibling-class calls/fields and stack-carrying
loops with allocating safepoints. Input-slot root observations, exact fault origins,
resource failures and cleanup pass under ASan/UBSan and the 2 KiB static native-frame
gate on the recorded macOS ARM64 target. See [ADR 0042](decisions/0042-reference-join-inference.md)
for convergence, ambiguity and shared-work-budget boundaries.

## Budgeted object execution frames

`object-frames-native-v14` retains the budgeted frames that superseded the object control-flow and earlier emission
profiles ([ADR 0027](decisions/0027-budgeted-execution-frames.md),
[ADR 0028](decisions/0028-packed-boundaries-and-arena-diagnostics.md),
[ADR 0031](decisions/0031-native-inherited-fields-and-constructors.md)). It has a 1 MiB
per-context execution-storage budget independently of retained object storage.
Target-sized typed frames include parameters, locals, values, block inputs,
roots, metadata, scratch, typed virtual-call pointers and reusable outgoing
arguments. Frames have stable addresses and release in reverse reservation order;
root cleanup precedes reuse.
Existing root/IR limits and the 64-call limit remain.

Status 9 reports execution storage exhaustion/backing-allocation failure. It is
not a managed StackOverflowException. Callee-entry failures identify that method
and its first block's IL offset; argument and receiver faults occur first, then
depth precedes storage checks. Failed execution leaves the boundary result
unchanged. The private context constructor permits test budgets; the public
experimental Int32 boundary uses the default and budgets its entry packet too.

The experimental export is now `cilica_m_<hash>_<token>_objects_packed_try`, with
this fixed-size native signature:

```cpp
std::uint32_t entry(std::int32_t* result, std::uint32_t* method,
    std::uint32_t* il, const char** image,
    const std::int32_t* parameters, std::size_t count) noexcept;
```

`count` must equal the managed entry's parameter count. A zero-parameter entry
allows a null input pointer; other entries require a readable array of `count`
Int32 values. Null required pointers or a mismatched count return status 3 before
reading inputs, allocating execution storage or writing any output. Callers own
input storage; the runtime copies it to its budgeted entry packet. Outputs must
remain valid and mutually disjoint. Pointer validity beyond null checks remains
the native caller's responsibility. The new symbol prevents linking an old
individual-parameter caller to this incompatible ABI. The primitive ABI is unchanged.

ASan builds mark unused and released execution storage inaccessible, restoring
access during each typed reservation. Reservations use at least 8-byte alignment
in all builds, keeping neighboring live slots in separate tested ASan shadow
granules without changing budget accounting between instrumented and ordinary
builds. A negative subprocess regression confirms a stale read produces
`use-after-poison`; address reuse cannot distinguish a stale pointer from a
pointer to the new live occupant. This is arena diagnostics, not collection.

Small non-inlined native helpers bound per-function spill growth; a dispatch
loop advances between them without growing the native call chain. O0/O2 native
regressions cover finite/mutual recursion, exact/excess memory and depth, cleanup,
alignment, many call arguments and allocation-time roots. Apple Clang 17 macOS
ARM64 stack reports measure packed export maxima of 288 bytes (O0) and 224 bytes
(O2), for arities 0, 1, 1,024 and 4,096. The wide recursion translation unit,
including its complete test harness, has static per-function maxima of 1,520/768
bytes at O0/O2, below its 2 KiB threshold. Instrumented execution also enforces
that warning threshold. Both builds execute finite/mutual recursion through the
64-call limit on a pthread requesting 256 KiB; macOS supplies 268 KiB, checked
against a 320 KiB ceiling. This covers the authored call chains and runtime paths;
it is not a universal host-stack guarantee or validation of other targets.
Native runtime/library calls and dispatch overhead still count toward stack use.
Helpers retain at most two ordinary IR statements and four-copy argument/edge
batches. Field operations use individual steps and a non-inlined descriptor
lookup to bound O0 sanitizer spills. An opt-in local microbenchmark compared a four-statement candidate,
which reduced its three-workload executable size but exceeded the 2 KiB native
frame ceiling in reference-field sanitizer tests and was rejected. Highly
variable timing establishes no throughput improvement. See
[ADR 0029](decisions/0029-measured-helper-batching.md) and the
[benchmark instructions](../benchmarks/ExecutionFrames/README.md).
The separate primitive emitter is unchanged in storage/depth policy; collection
and broader managed-runtime capabilities remain planned.

## Native concrete inheritance

Since `object-frames-native-v3`, emission composes all ancestor fields into each concrete native
allocation ([ADR 0031](decisions/0031-native-inherited-fields-and-constructors.md)).
There is one Object header; inherited methods keep the allocation's original
dynamic type and identity. Hidden fields retain declaring image/type/token
identity. Descriptors contain immediate-base links, measured sizes/alignments,
all field offsets and complete inherited reference maps. Typed member-address
accessors select actual subobjects; execution does not manufacture member
pointers through byte-offset arithmetic or reinterpret a derived object as a
separate ancestor struct. Heap membership precedes ancestry and field access.

The 65,536 physical field-copy limit includes all catalog layouts, even if a
layout is only referenced rather than allocated. Runtime object bytes include
all inherited fields. The execution arena, depth limit, root limit and packed
export remain under their previous contracts. The derived receiver is published
before constructor entry and remains rooted across every base invocation.
Constructor faults unwind roots/arena reservations while partially initialized
objects become eligible for collection after their roots unwind (the original
inheritance checkpoint retained them until teardown).

Evidence: three independently authored C# entries supply 435 native/.NET result,
fault-kind and exact-origin cases per O0/O2 configuration. They include a
three-level hierarchy, private base integers, hidden readonly reference fields,
base-typed locals/arguments/returns and inherited calls. Observers inspect roots
and inherited child references while initializers and base constructors allocate.
Native checks cover initialization order, zero storage, identity, all allocation
failure stages, exact/short execution budgets, cleanup/reuse, ownership/type
rejections and typed destruction. Cross-image same-name/equal-token fields are
checked separately, including a 4,096-field accessor variant. Builds use
ASan/UBSan and the 2 KiB native-frame warning at both optimization levels on the
recorded macOS ARM64 toolchain. Collection, broader access and
other-target compatibility remain later work.

## Class casts and runtime type tests

`object-operations-ir-v3` and `object-frames-native-v4` add `castclass` and
`isinst` against exact TypeDef/TypeRef targets, using the existing descriptor
ancestry chain ([ADR 0032](decisions/0032-class-casts-and-type-tests.md)). Target
identity includes the image content hash; equal names/tokens across images do
not establish compatibility. Type-test-only targets enter the catalog and its
existing type, metadata, field and physical-layout budgets. Target access is
same-owner or top-level public. Interface targets are described below. Abstract,
value-type, generic, array and TypeSpec targets remain unsupported, including with
a null operand.

Both operations consume a reference/null value and produce the target reference
type. Successful casts/tests preserve object identity and dynamic type. Null
stays null; mismatch returns null for `isinst` and status 7 for `castclass`.
Membership checks run before descriptor reads; invalid/foreign pointers report
status 6. Both IR operations therefore require method-exit fault edges, although
an ordinary failed `isinst` is not a fault. Results use existing phase A roots
and arena cleanup; no allocation, boxing, RTTI or managed exception object is
introduced. Uninitialized constructor receivers cannot be cast or tested before
their immediate-base call. The packed Int32 ABI is unchanged.

Two authored C# entries provide 84 .NET/native result and exact fault-origin
cases per O0/O2 build. Coverage includes null, exact/ancestor/descendant and
sibling/unrelated tests, identity through casts, loops and allocating calls,
cast-before-argument/null fault order, root observation, exact/short frame
budgets, ownership checks and reuse after faults. Raw metadata/native fixtures
distinguish same-name/equal-token classes in separate images. Malformed targets,
unsupported shapes, forged IR and unfinished receiver casts/tests are rejected.
Native builds pass ASan/UBSan and the current 2 KiB frame warning on the recorded
macOS ARM64 toolchain. That threshold remains a conservative baseline; later
performance tuning may revisit it with representative workloads and complete
target-specific stack budgets, as recorded in ADR 0029.

## Concrete class virtual dispatch

`object-operations-ir-v4` and `object-frames-native-v5` add public nongeneric class
virtual calls ([ADR 0034](decisions/0034-native-class-virtual-dispatch.md)). The
translator revisits compatible concrete catalog classes after discovering each
wave of override bodies. Its metadata ledger, method/type limits and IR budgets
remain shared; dispatch maps are bounded to 65,536 total entries. Unsupported
override bodies fail with an inclusion path rather than producing partial IR.

Bindings retain canonical slot identity and name. Complete detached maps select
the nearest included same-slot implementation per compatible receiver, validate
signatures and reject final replacements. New virtual slots remain independent;
nonvirtual hiding does not replace an older slot. A direct `call` invokes its
named body, including a base implementation, while virtual `callvirt` selects
from the receiver's exact descriptor after evaluated arguments and null/ownership/
ancestry checks. No matching descriptor entry reports status 6 at the call site.

Typed lookup returns before invoking the managed body. Selected function pointers
occupy caller arena slots and are included in target-sized frame reservations;
there is no additional live dispatch-wrapper frame across recursion. Reference
and void returns retain roots, fault propagation and cleanup. The Int32 boundary,
64-call limit, execution/object budgets and retained-heap contract are unchanged.

Evidence: 172 authored .NET/native outcomes per O0/O2 build, observed caller roots
across allocating overrides, exact/short arena budgets, all allocation fault
stages, ownership checks, cross-image equal-token slot/body identities and virtual
recursion within a checked 320 KiB pthread stack ceiling. ASan/UBSan, static Clang
stack reports and the current 2 KiB frame gate pass on macOS ARM64.
Abstract classes, generics, covariance, class MethodImpl slot remapping,
collection and other-target execution remain unimplemented.

`class-virtual-target-v2` permits mixed class/interface dispatch on owners with
validated interface-only MethodImpl rows
([ADR 0041](decisions/0041-mixed-class-interface-dispatch.md)). Both exact resolvers
share the bounded contract graph and mapping validation. Class calls retain their
canonical class slots; explicit interface bodies remain reachable only through
interface maps or separately permitted calls. Every mapping in the hierarchy is
checked, even for class-only programs, without translating unused interface bodies.
Class-slot remapping, invalid body owners/signatures, unimplemented or inaccessible
contracts and duplicate rows still fail with diagnostics. Existing IR/native
profiles, calling convention, fault order and resource bounds remain unchanged.

## Exact interface target metadata

`AssemblyResolver.ResolveInterfaceMethodTarget` accepts an interface MethodDef,
concrete resolver-owned class receiver and optional shared metadata ledger
([ADR 0035](decisions/0035-exact-interface-method-targets.md)). Its result retains
the initially mapped method, optional canonical class slot, effective body and
explicit-mapping fact. Abstract interface declarations have no executable body
in this contract; the operation never executes inspected code or verifies IL.

The bounded profile covers public nongeneric interfaces and concrete nongeneric
class ancestors. Inherited mappings survive method hiding; reimplementation
starts a fresh owner/ancestor search. Exact MethodImpl rows take precedence over
same-owner public name/signature matches. Explicit bodies require public virtual
or private final newslot declarations with exact signatures/owners. Mapped virtual
bodies select overrides through their canonical slot; inherited public nonvirtual
bodies remain direct. Interface ancestry supports diamonds, with 256 distinct
interface types and direct-list entries, a 256-class bound, 64 declaring owners
and the existing shared 65,536-unit metadata ledger.

Default/static interface methods, generics, abstract classes, private contracts,
class slot remapping, interface remapping and differing return signatures remain
unsupported. Malformed cycles, duplicate edges/members/mappings, missing bodies,
foreign readers and unrelated contracts return diagnostics without partial targets.
Equal names/tokens across input images do not merge contracts or bodies.

Evidence: 47 authored .NET interface-map and trusted call comparisons, plus raw
selection, malformed metadata and exact/excess resource regressions. The class
metadata operation and conservative audits retain their previous contracts.
Detached interface catalogs and reference flow are implemented under ADR 0036.
The object compiler now integrates calls through those declarations under ADR
0038 and emits native calls under ADR 0039. Interface casts are covered by ADR 0040.

## Detached interface dispatch preparation

`ObjectInterfacePlanBuilder.Build` takes resolver-owned class/type roots, explicit
interface method declarations and a selected runtime library
([ADR 0037](decisions/0037-detached-interface-dispatch-plans.md)). It builds the
catalog, snapshots bodyless declarations, and prepares exact maps for every
compatible class using ADR 0035. Declarations permit Int32/reference parameters
and Int32/reference/void returns with public abstract instance flags. Selected
class facts retain their receiver type, member name/flags, exact signature and
canonical slot. Nonvirtual mappings retain their direct implementation. Private
final newslots remain mapping facts, without gaining executable public access.

`ReadMethodDeclarationInput` snapshots signatures, owner visibility/generic facts,
initializer obligations and the presence of a body RVA without reading IL. It
uses member/owner limits and shares the body reader's existing bounded provenance
logic. The body reader continues rejecting bodyless methods. Input signatures
remain resolver-owned; finished plans contain detached identities and types.

Plans share the catalog's 256-type/4,096-field limits. Interface declarations and
class method facts together permit at most 256 identities, each with at most
4,096 parameters including the receiver; aggregate parameter slots are bounded
to 65,536. Target entries share the existing 65,536 dispatch cap, with at most
256 receivers per declaration. One caller-owned metadata ledger, at most 65,536
units, covers declaration inputs, catalog reads and exact target resolution.
Exhaustion returns a diagnostic and no partial plan. Ownership is checked before
content-based caching; equal tokens/names across images remain separate.

`ObjectInterfacePlanValidator` checks flags, identity/ownership assertions,
signatures, canonical slot introductions, nearest included overrides, final
implementations, compatible receiver coverage and inherited mapping stability.
Class method hiding cannot change a mapping without reimplementation or a new
asserted explicit mapping. Duplicate/unknown declarations, receivers and method
facts, unsupported signatures and unrelated facts fail. These are structural
checks on asserted facts, not authentication of every metadata row or the absence
of omitted methods/MethodImpl rows; the builder obtains its choices from exact
metadata resolution. Body instructions and executable access still require
ordinary translation checks.

Evidence: 47 authored .NET map/slot comparisons, reference/void signatures,
detached lifetime/contract serialization for the fieldless fixture, cross-image
namesakes, forged maps, hiding/final regressions, ownership and exact/excess
method/parameter/shared-work budgets. The standalone preparation API remains
separate from executable translation below.

## Interface call translation and executable closure

`object-operations-ir-v5` attaches an optional detached interface plan to
`ObjectIrProgram` ([ADR 0038](decisions/0038-interface-call-translation.md)).
Interface declarations appear only in the plan. CIL `callvirt` obtains its exact
receiver/argument/result signature there; only selected class implementations
enter the executable method closure. Mapping and canonical-slot methods can
remain metadata facts, including when their unused IL is outside the supported
profile. Selected bodies still undergo all ordinary IL, access, constructor,
initialization and fault-order checks. An explicit private final newslot body
does not become accessible through ordinary cross-owner calls.

Each newly translated body can introduce receiver types or interface/class calls.
Preparation revisits the growing catalog until body discovery stops, caching
snapshots and exact declaration/receiver choices within the shared 65,536-unit
ledger. Call-owner classification reads one TypeDef row without decoding unused
static-owner fields. Executable bodies, bodyless declarations and mapping facts
share 256 distinct method identities. Class and interface maps share 65,536
target entries. A null-only interface call permits an empty map if no compatible
class is included; arguments still evaluate before the null check.

Validation requires identical plan/executable catalogs, matching body signatures
and bindings, every selected implementation body, and exactly the maps used by
interface calls. Calls retain receiver null checks and explicit method-exit fault
edges. Phase A assigns the usual parameter/local/value/block-input reference
slots and allocation/call safepoints for interface references; its existing
1,024-root method cap remains. These plans alone do not prove survival through
native calls or collection; native evidence is described below.

Authored .NET comparisons cover hiding, overrides, explicit implementation,
reimplementation, diamonds, two distinct contracts, recursion, reference fields,
reference/void results, implementation-discovered receivers/calls and argument
faults before null receivers. A bounded test evaluator supplies translated-IR
evidence only. Forged IR and unsupported CIL receive reduced regressions.
`object-frames-native-v6` now consumes validated interface plans with the same ABI.
Ambiguous inferred reference intersections, default/static/generic contracts,
abstract classes and class MethodImpl remapping remain unsupported. Mixed class
dispatch on owners with validated interface mappings is supported under ADR 0041.

## Native interface lookup and execution

`object-frames-native-v6` emits exact descriptor membership tables for every
included contract and typed body tables for its used methods
([ADR 0039](decisions/0039-native-interface-dispatch.md)). Membership includes
inherited class mappings and transitive parent/diamond contracts. Interfaces have
no object layout, wrapper allocation or adjusted pointer: references retain their
original object identity, descriptor and owner. Empty membership/dispatch tables
support null-only calls. Contract identities remain content-scoped across images.

Reference parameters/stores use ownership checks before descriptor inspection,
then class ancestry or exact interface membership as appropriate. Interface calls
check null/ownership/membership before selecting a body. Unknown descriptor
addresses or absent table entries report status 6 at the caller's method/IL origin;
null receivers report status 4, after argument evaluation. Class and interface
tables use exact return-category function pointer types, without function pointer
casts. Implementations share the arena argument packet and root storage; no fake
executable interface declaration is emitted. Callee depth and storage reservation,
fault propagation, reverse root/frame cleanup and packed Int32 exports are unchanged.

Eleven authored C# entries provide 170 native/.NET result and fault-origin cases
per O0/O2 configuration, including overriding/hiding/reimplementation, explicit
methods, two contracts, diamonds, metadata-only canonical slots, mixed class calls,
interface parameters/fields/reference and void results, empty maps, recursion and
mutual recursion. Allocation observers check returned references in caller roots
and interface fields across later allocations. Exact/one-byte-short frame budgets,
every object-count failure stage, cleanup/reuse, foreign/invalid/null/wrong-type
receivers and copied/unreadable descriptor addresses are checked independently.
A raw two-image fixture keeps same-name/equal-token contracts and private bodies
distinct in native execution. ASan/UBSan and the 2 KiB native-frame warning remain
enabled at O0/O2; uninstrumented stack reports and a 256..320 KiB recursion thread
check the existing native-stack policy on the recorded macOS ARM64 toolchain.

This original dispatch checkpoint used a retained heap. Its checks alone do not
prove roots under reclamation; milestone 4 adds that evidence separately. Neither
checkpoint establishes compatibility with other platforms. Ambiguous inferred reference intersections, default/static/generic
interface semantics, abstract classes and class MethodImpl slot remapping remain
unsupported. Performance optimization stays deferred.

## Interface casts and runtime type tests

`object-operations-ir-v6` and `object-frames-native-v7` accept named public
nongeneric interface targets for `castclass` and `isinst`
([ADR 0040](decisions/0040-interface-casts-and-type-tests.md)). Target-only contracts
and transitive parents enter the shared bounded catalog. A cast needs no dispatch
plan or executable interface body. Class/interface identity remains content-scoped;
compatible classes include inherited implementations and parent/diamond contracts.
Nested contracts require a public top-level owner and public visibility at every
nested level. Cast/test-only targets receive the same check as dispatch contracts,
including with null operands; inaccessible owner chains produce a build diagnostic.
Detached nested contracts must retain an explicit external-visibility assertion.

Native casts reuse exact descriptor membership after ownership checks. Successful
operations preserve the original pointer; null stays null. A mismatch returns
null for `isinst` or status 7 for `castclass`, while invalid/foreign pointers report
status 6 first. Membership compares unknown descriptor addresses without reading
their contents. Results retain existing rooted arena storage, constructor rules,
fault origin/order and cleanup. No allocation or boundary ABI change is introduced.

Three authored entries compare 144 .NET/native result and exact fault-origin cases
per O0/O2 build. Coverage includes identity, class/interface composition, explicit
implementations, inherited/parent/diamond contracts, empty target-only contracts,
casts versus argument/null fault order and roots across allocations/backedges.
Exact/short frame budgets, allocation failures, cleanup/reuse, ownership and
copied/unreadable descriptors are checked independently. Raw native cases keep
same-name/equal-token contracts across images distinct. Malformed targets, forged
IR and unfinished constructor casts/tests retain rejection regressions.
ASan/UBSan and the 2 KiB stack gate pass on macOS ARM64; static reports measure
maxima of 704 bytes at O0 and 640 bytes at O2 across the new driver/generated code.
Collection and ambiguous inferred intersections remain separate gates;
generic/nonpublic/array/TypeSpec targets and class MethodImpl remapping are unsupported.

## Detached array operation foundation

`object-operations-ir-v7` validates caller-constructed vector allocation, length,
element loads and element stores
([ADR 0043](decisions/0043-detached-array-contracts.md)). Lengths and indices use
the bounded Int32 contract; reference loads retain the static view's element type.
Every array operation carries an explicit fault edge. Covariant reference stores
still require a runtime check against the actual allocation's element type;
null must be checked before bounds, and bounds before a store-type mismatch.
Those lowering obligations are implemented by native v8, described below.

Root assignment includes array values and loaded references and marks array
allocation as a safepoint. Constructor validation prevents the unfinished receiver
escaping through an element store. Metadata/CIL lowering and native dynamic storage
are described below. Array descriptors remain distinct from their Object anchor;
no nominal Object layout is substituted for vector storage.

The detached assignment matrix matches .NET on all 196 modeled type pairs.
Fixtures cover cross-image identities, JSON round trips, covariance/declared flow,
field/element closure, root contracts, exact/excess 256-type limits, invalid
operations, constructor escapes and deep forged identities. These tests establish
validation behavior, not native array execution or collection.


## Array metadata and CIL translation

SZARRAY fields, parameters, returns and initialized locals accept Int32 or nominal
class/interface elements ([ADR 0044](decisions/0044-array-metadata-and-cil.md)).
Executable methods and standalone interface preparation share one signature grammar;
class/interface dispatch preserves exact array signatures. Nominal element closure
and vectors share the existing 256-type budget, field/member limits and read cache.

Supported instructions are `newarr`, `ldelem.i4`, `stelem.i4`, `ldelem.ref`,
`stelem.ref`, and the adjacent same-block `ldlen; conv.i4` idiom. Length/index/value
checks and method-exit fault edges use the validated array IR. Only the selected
runtime's exact Int32 identity maps a named allocation element to primitive storage.
Reference element loads retain the static receiver view. Element access following
an unequal-reference stack merge requires an explicitly typed array local;
exact/null array joins remain supported. General native integers, token-form
`ldelem`/`stelem`, `ldelema`, array casts, nested/multidimensional vectors and other
primitive/value elements remain diagnostic boundaries.

Authored C# comparisons use a bounded test-only evaluator for Int32 array allocation,
aliasing, calls/returns, mutation, zero initialization, bounds/null/negative-length
faults and Object[] field/identity behavior. Additional translation tests cover
reference covariance and array signatures in class/interface dispatch. These are
translation checks, not native array execution, runtime store-check or collection
evidence. The native checkpoint below provides separate execution evidence.


## Native vector execution

`object-frames-native-v14` emits bounded Int32/reference arrays
([ADR 0045](decisions/0045-native-array-storage.md)). A fixed header owns a separate
zero-initialized typed element buffer. Heap accounting includes header and payload;
failed allocation leaves counters/list unchanged, and instance cleanup frees both.
Empty arrays have distinct identity. Array descriptors retain exact vector identity,
reference-element acceptance and dynamic reference-slot inspection callbacks.

Null checks precede bounds; bounds precede reference-store type checks. Negative
lengths report overflow (2), allocation failure reports 5, bounds report 10 and
array element mismatch reports 11. Failed stores preserve the previous element.
Native method-entry argument checks and operation ownership checks retain status 6.
Exact descriptor membership implements covariance and safely rejects unknown
addresses without traversing their contents. Existing class/interface casts and
Object widening compose with arrays; array-target casts remain unsupported.

Reference slots, safepoints, execution storage and the packed Int32 boundary retain
their existing contracts. The new native tests check roots across array/object
allocations, dynamic slots, cycles through Object elements, exact byte/count/frame
budgets, failure cleanup/reuse and invalid/foreign references. O0/O2 .NET comparisons
also cover 155 values/fault-origin cases per build under ASan/UBSan, without C++
exceptions or RTTI. Both static stack reports peak at 656 bytes, within the 2 KiB
frame gate. Only the recorded macOS ARM64 target
is evidenced. Collection, jagged static types, framework array interfaces, general
native-sized integers, managed exception handlers and broader platforms remain gaps.


## Detached immutable UTF-16 strings

`object-layout-v6`, `typed-reference-ir-v5` and `object-operations-ir-v8` define a
selected String reference identity outside mutable class layouts
([ADR 0046](decisions/0046-detached-string-contracts.md)). It is sealed, widens to
Object, supports null and composes with fields and reference vectors. No String
framework interfaces or ordinary String method bodies are implied.

Five operations validate typed earlier operands and explicit fault edges: UTF-16
literal, code-unit length, indexed code-unit access returning Int32, ordinal content
equality returning 0/1, and two-string concatenation. Numeric ushort literal payloads
preserve zeros and lone surrogates in JSON. Literals are specified to share identity
by content within an instance; concat treats null as empty and reuses a sole
nonempty operand. Literals/concat receive allocation safepoints. No mutation or
general Char storage is added.

Limits are 65,536 units per literal, 262,144 units across all occurrences/methods,
and 256 distinct ordinal contents; unit limits are checked before copying/hashing. The String
identity counts toward the existing 256-type cap. Tests compare modeled assignment
pairs with .NET and verify malformed identities, UTF-16 round trips, resource
limits, fault edges and constructor/root rules. These are detached validation
results. String metadata/CIL translates under ADR 0047 below; ADR 0048 adds
native immutable storage, literal identity and content-operation conformance.

## String metadata and intrinsic CIL

[ADR 0047](decisions/0047-string-metadata-and-cil.md) connects the detached contract
to the selected library's public sealed nongeneric String definition, directly
based on selected Object and without type/module initializers. Its fields/interfaces
are not imported. String supports method parameters/returns, locals, fields,
reference vectors, dispatch signatures and named cast/test targets.

`ldstr` validates and bounds the encoded heap payload before copying numeric UTF-16.
The existing literal limits count every occurrence across methods, while caches
avoid charging stack-inference revisits twice. Intrinsic declaration snapshots
share method/metadata limits and never read framework IL bodies.

Exact supported members are `callvirt String.get_Length()` and
`callvirt String.get_Chars(Int32)`; `call` to static `String.Equals(String,String)`,
`op_Equality`, `op_Inequality`, and `Concat(String,String)`. Char/Boolean results
normalize only these calls to Int32 stack values. Operands retain source order,
null/index checks and fault origins. All other overloads/call forms, constructors,
String.Empty and general Char/Boolean storage remain unsupported.

Authored C# and reduced metadata tests cover composition, wrong signatures/owners,
initializer rejection, raw UTF-16, malformed heaps, shared limits and .NET comparisons
of translation through a test-only evaluator. Native execution evidence is recorded
separately below; collection remains unimplemented.


## Native immutable UTF-16 strings

`object-frames-native-v14` executes the five bounded String operations
([ADR 0048](decisions/0048-native-string-storage.md)). Each string owns an immutable
numeric UTF-16 buffer. Its header and buffer consume the retained instance heap's
byte/object limits; failed allocation leaves the heap and literal cache unchanged.
Equal literals across methods share an instance-local object. Empty concat uses
the same canonical empty as an empty literal; two nonempty operands produce a
fresh noninterned result. Null/empty concat reuses a sole nonempty operand.

Exact String descriptors join Object widening, fields, vectors and casts/tests.
Length/index operations check ownership/type and null before bounds; index values
are zero-extended to Int32. Ordinal equality supports null, embedded zeros and lone
surrogates. References use existing root slots, including both operands during
allocating concat. Allocation/combined-length exhaustion reports status 5; null
reports 4 and string index bounds use status 10. Faults preserve method/image/IL
origin and leave the packed Int32 result unchanged. No string reference crosses
that boundary. The literal cache dies with its instance; no process-global intern
pool or collector is present.

A metadata catalog that combines String with an interface actually implemented
by the selected String is conservatively rejected. A bounded interface-ancestry
scan includes nongeneric parents of generic interfaces without importing their
bodies or adding generic execution. This prevents incorrect casts and covariant
array stores while String's modeled ancestry is only Object. Unrelated user
interfaces with string arguments/results remain supported.

Local .NET/native conformance covers O0/O2 on macOS ARM64 with ASan/UBSan and the
2 KiB static frame baseline. It covers contents/identity, fields/vectors/dispatch,
null/index/cast/store faults and operand order, exact heap/execution limits,
foreign/forged references, root publication and cleanup/retry. This does not claim
wider platform support, thread safety, interop strings or roots across collection.


## Composed retained-heap checkpoint

Milestone 3 closes for the bounded profiles above. The reproducible
[object example](../examples/Objects/README.md) combines constructor/inherited
fields, class/interface dispatch, class type tests, Int32/reference vectors,
String[] covariance, immutable UTF-16 operations and branches/backedges in one
12-body inventory closure. A separate authored .NET oracle checks results and
exact null/bounds/overflow origins; the metadata-only emitter never executes the
input. Debug O0/O2 and Release O2 pass on the recorded macOS ARM64 target with
ASan/UBSan and the 2 KiB frame warning. This is integration evidence, not an
additional compiler profile or broader language/platform compatibility claim.

The original milestone 3 checkpoint retained allocations until instance teardown.
Milestone 4 adds collection, stress and private retained handles below. Exception
objects/handlers, generic execution, broader library APIs and mobile/browser
execution remain separate gates. Object emission remains
a compiler API; the example's dedicated metadata-only helper is not a new general
CLI command or stable embedding API.


## Precise reference visitation

Native v10 adds private, allocation-free root and object-edge visitors
([ADR 0049](decisions/0049-precise-reference-visitation.md)). Roots are the supplied
context's published frame slots and its instance's cached literals. Object children
are flattened inherited reference fields and reference-array elements. Null and
alias slots retain provenance; integers, string units, descriptor/owner links and
the retained heap list are excluded. Marking, reclamation and handle roots were
added in subsequent slices described below.

Contained reference values require instance ownership and exact known descriptors
before delivery. Callbacks receive borrowed frame/slot, literal-ID or owner/field/
element information. They may stop early; invalid/stopped walks may have delivered
a prefix and cannot justify a future sweep. Walkers preserve pending fault/origin
and runtime state. Callbacks cannot allocate managed objects, mutate references or
structure, or re-enter managed execution. Runtime-owned native storage/topology and
buffer extents remain trusted; this is not an arbitrary native-memory validator.

The supported model supplies one active context per instance. Frame walks respect
64 frames and 1,024 roots per frame; cached roots are limited to 256 authored
literal contents plus implicit empty. Field/vector extents retain existing bounds.
Local O0/O2 sanitizer and stack checks cover exact visitation, aliases/cycles,
invalid contained references, exact/excess limits and real frames at allocation
points. Those observations do not demonstrate safe reclamation, handles, multiple
active contexts or phase B liveness. See the [milestone 4 plan](milestones/04-collection-and-handles.md).


## Bounded marking and reachability diagnostics

Native v11 adds allocation-free marking using header scratch and the precise
visitors ([ADR 0050](decisions/0050-bounded-native-marking.md)). Cycles/aliases are
processed iteratively, with default caps of 4,096 retained objects and 262,144
root/child slots. Null/alias slots consume work. Ownership membership searches
remain bounded list scans; the marker does not claim linear time. Header scratch
is charged through the existing allocation sizes and byte quota.

Validate inventory ownership, canonical descriptors and count agreement first.
Only a complete reachable-graph trace permits per-object diagnostic classification.
Reports distinguish complete, invalid, limit, stopped and busy; counts are zero
unless tracing and optional reporting both finish. Diagnostic callbacks may have
received a prefix before stopping. Scratch clears before return, including after
failure; nested marking cannot alter an outer pass. Ordinary managed fields,
root/storage state, synchronization and pending faults remain unchanged.

A completed report is a snapshot, not a retained mark set or sweep authorization.
The v11 checkpoint did not invoke marking on allocation or free objects; v12 and
later add those behaviors below.
Local O0/O2 ASan/UBSan checks cover exact reachable sets, a 4,096-node cycle, budgets,
callback stop/reentry, invalid input, retries and real allocation-observer frames.
The later milestone 4 slices below cover reclamation/accounting, allocation retry,
poisoned collection stress and retained handles.


## Native reclamation and retry

Native v12 supersedes the retained-until-teardown allocation behavior described in
older checkpoints. [ADR 0051](decisions/0051-native-reclamation.md) adds synchronous,
non-moving mark/sweep with exact header/buffer accounting and at most one collection
attempt per allocating operation on quota or native allocation failure. Incomplete
marking frees nothing. Published frames and literal cache entries are roots; native
locals alone are not. Single active context per instance remains required. The
existing packed Int32 boundary is unchanged. Stress and retained handles are
implemented by subsequent profiles below.


Native v13 adds opt-in collection before eligible allocations
([ADR 0052](decisions/0052-collection-stress.md)). Local O0/O2 ASan/UBSan evidence
covers repeated execution, fault cleanup, live roots and reclaimed array/header
poisoning. This is the bounded phase A stress checkpoint, not broader platform or
language compatibility.


Native v14 adds private instance-owned strong handles
([ADR 0053](decisions/0053-instance-retained-handles.md)): 1,024 lazy native slots,
nonreused 64-bit token generations, duplicate/null roots, resolve/release and
stale/foreign-instance rejection. The table uses separately bounded native memory;
managed collection traces active slots. A resolved pointer is borrowed while its
handle remains active. These helpers do not define a public cross-library ABI.


## Milestone 4 completion

[ADR 0054](decisions/0054-stage-four-completion.md) records the bounded native v14
checkpoint: cycles are reclaimed, published roots and retained handles survive
allocation stress, and incomplete marking cannot authorize sweep. The inventory
example checks repeated execution and ordered faults in a persistent private
instance. All 196 selected local regressions pass, including O0/O2 sanitizers;
no additional platform compatibility is claimed. Public object embedding remains
planned, along with phase B liveness, weak/finalizable objects, concurrent collection,
named retention paths and per-type allocation statistics.


## Detached managed storage locations

[ADR 0059](decisions/0059-managed-storage-locations.md) adds a separate
`ManagedLocationIrProgram` contract around value IR. Locations name frame
local/by-value parameter storage or exact object fields. Nested inline fields
retain FieldDef paths; no numeric native offset or raw pointer enters IR.
The location's element is exactly Int32, an included reference or an inline
value. Null, void and nested-location elements are rejected. Locations are
invariant, with writable-to-readonly lending allowed and no reverse conversion.
Readonly fields/parents retain readonly locations.

Internal direct calls may synchronously borrow/reborrow locations, including
through void mutators. Locations cannot be returned, passed into the entry without
a caller, stored in locals/fields or carried through block joins. These explicit
restrictions establish frame lifetime without inferring provenance. Location
loads copy the element; stores and initialization require writable storage.
Memory access and heap-field address creation require method-exit fault edges;
frame address creation and nested field derivation are pure. Metadata access
rights and constructor authorization remain separate from this detached contract.

Root plans publish the heap owner of each location copy, never its interior
address as an object root. Frame locations borrow an active frame whose backing
reference slots remain rooted. Carriers count as two logical slots; expanded
storage and root limits remain 65,536 per program and 1,024 per method. Calls retain
safepoints and complete closure validation. Existing pure-copy/native profiles
reject these operations. This establishes no executable byrefs or collection
through mutable struct storage; native authentication, CIL value translation,
array/boxed locations, ref returns, byref locals/joins and native return packets
remain subsequent gates.

## Private native scalar locations

[ADR 0060](decisions/0060-native-scalar-location-carriers.md) adds checked runtime
helpers in native v15. Frame locations authenticate a nonreused token in the current
context and select a bounded typed slot; heap locations validate an owned receiver,
exact descriptor and inherited field selector. Reference compatibility and readonly
fields are checked before mutation. Failure preserves outputs and pending faults;
marking callbacks cannot access borrowed storage. Frame retirement prevents address
reuse from reviving an expired borrow. Tokens permanently exhaust rather than wrap.

Local O0/O2 ASan/UBSan tests include the 2 KiB native stack-warning gate, exact
registration bounds, nested borrows, stale/foreign/wrong-kind rejection and
collection through rooted frame slots and heap owners. Owners must stay rooted
across allocations: these carriers are synchronous borrows, not retained handles.
Native scope nesting and backing buffers are trusted generated-runtime invariants.
Existing public emitters still reject managed-location IR. CIL value translation,
whole-struct mutation/returns, inline heap storage and broader milestone 5 semantics
remain unimplemented.


## Native scalar location IR

[ADR 0061](decisions/0061-native-scalar-location-ir.md) adds an explicit
`ScalarLocationIrProgram` / `ObjectCppEmitter.EmitLocations` path in native v16.
Scalar local/parameter and heap-field borrows compose with existing object
operations, including allocation. Field addresses retain access checks; direct
static callees validate exact storage types and readonly capabilities. Each carrier
copy publishes a distinct owner root. Lending frames register tokens, and all
argument/carrier storage is charged to the execution arena. Existing object entry
points still reject location IR.

Local O0/O2 ASan/UBSan evidence compares authored IR against C# with collection
before allocations and checks cleanup after invalid/readonly carriers, allocation
failure and token exhaustion. Root/storage limits remain 1,024 per method and
65,536 logical slots per program. This does not translate CIL byrefs or expand
inline values. Whole-struct mutation/returns, array/boxed locations, byref locals/
joins, ref returns and borrowed instance/virtual signatures remain unsupported.

## Mutable inline value expansion

[ADR 0062](decisions/0062-mutable-value-scalar-expansion.md) adds
`ValueScalarLowering.LowerLocations`, returning validated scalar-location IR from
authored managed value/borrow IR and explicit static bindings. Struct copies and
locations expand according to the same field-path layout. Whole loads snapshot
before mutation, and nested readonly fields produce explicit readonly views.
Empty values have no scalar slots while retaining their calls. Bounds are checked
before carrier-array expansion and again on the output root/storage plan.

O0/O2 ASan/UBSan probes match C# for nested mutations, by-value isolation, swaps and
self-aliasing. A private harness verifies identity and collection of live embedded
reference slots and rejects an invalid final borrowed leaf before any mutation.
Source value IR contains no allocating object operations, so this does not prove
collection inside allocating whole-value source programs. CIL translation, inline
class/array fields and struct returns remain unsupported, along with the broader
milestone 5 capabilities still marked planned.

## Caller-owned struct returns

[ADR 0063](decisions/0063-caller-owned-value-return-packets.md) extends the location
lowering profile to v2. Each struct-returning call reserves typed caller locals and
passes their checked locations as hidden outputs. The callee returns void after
copying its snapshot; result loads run only after success. Reference destinations
remain rooted, nested callees cannot expose finished frames, and empty results
retain calls without output slots. Generated packet storage is included in all
existing parameter/local, work, root and arena-byte limits.

O0/O2 ASan/UBSan evidence covers nested/chained returns, C# arithmetic/fault parity,
unchanged packed output on failure, live reference identity and invalid output
capabilities. Exact measured arena storage succeeds and one byte less fails with
complete cleanup. This is authored IR support, not CIL translation or a public
struct ABI. Allocating value source programs, inline heap values and the other
planned milestone 5 capabilities remain separate work.

## Bounded value CIL discovery

`ValueProgramDiscovery.Read` supplies metadata inputs for direct static nongeneric
closures, Int32/value signatures, void returns and Int32/value byref parameters.
Fields, type operands and calls retain exact identities, access checks and shared
budgets (ADR 0064). Embedded references keep the existing layout grammar. This
API does not claim supported body opcodes or executable CIL. Standalone reference
signatures, modifiers, byref locals/returns and generic/instance methods remain
unsupported.

## Bounded value CIL execution

`ValueTranslator.Translate` adds executable stack verification and lowering to
ADR 0064 discovery (ADR 0065). It shares the native scalar location/return path.
The bounded static signature grammar remains unchanged. Supported value bodies
include locals, by-value argument addresses, nested fields, named-struct
`initobj`/`ldobj`/`stobj`/`cpobj`, Int32/reference indirection, direct calls, returns,
Int32 arithmetic/branches and exact stack joins. `conv.i4` only accepts Int32.
Null and exact reference values can flow through fields, but reference comparisons
and widening joins are unsupported. Borrowed locations cannot cross joins.

Type/member access and readonly restrictions are checked. Unsupported reachable
instructions and malformed stacks return diagnostics without partial IR. Local
O0/O2 ASan/UBSan tests compare real C# return, borrow and loop fixtures with .NET;
reduced whole-copy IL checks aliasing. This does not claim CIL allocation with live
embedded references, instance struct methods, byref locals/returns, boxing,
generics, exceptions or additional platforms. Existing CLI commands are unchanged.

## Allocating value/vector CIL

`ValueTranslator.TranslateArrays` opts into ADR 0067. It adds Int32 vector
signatures, `newarr` of the selected runtime Int32, `ldelem.i4`, `stelem.i4`, and
adjacent same-block `ldlen; conv.i4`. Reference identity comparisons and truth
branches are supported; widening joins and ordered reference comparisons remain
unsupported. The original `value-cil-v1` API retains its allocation rejection.

Local native evidence includes collection before every allocation through nested
struct copies, borrowed replacement, sole parameter roots and caller-owned returns,
plus final reclamation, fault provenance and exact/insufficient heap/arena budgets.
CIL reference/value array allocation, element-address borrows and class allocation/
constructors composed with structs remain unsupported. The CLI is unchanged.

## Allocating value/class CIL

`ValueTranslator.TranslateObjects` opts into ADR 0069. It adds nominal class/Object
signatures, class `newobj`, direct base-constructor calls and inherited
scalar/reference field operations to the value/vector profile. Static value methods
can borrow scalar heap fields with existing checked locations. Shared verification
enforces constructor initialization, member/type access and readonly receiver rules.
Metadata classification and catalogs share bounded read work and field limits.

Constructors require scalar/reference operations throughout their bodies; value
and borrowed parameters, locals, stack inputs and results are rejected. Ordinary
instance methods, dispatch, string intrinsics, inline heap values, boxing, generics
and managed handlers remain outside this profile. Existing CLI commands and older
value profiles are unchanged.

Local O0/O2 ASan/UBSan evidence includes allocation inside base/derived constructors,
live object graphs retained through struct copies, borrowed replacement and returns,
sole parameter-root transfer, final reclamation, division fault provenance, nested
allocation failure/context reuse and exact/insufficient heap/arena budgets.

## Detached boxed-value contracts

`BoxedValueIrProgram` opts into ADR 0070. `BoxValue` specifies fresh object identity
and a copied named-struct payload; `UnboxValue` specifies an exact-type copy;
`UnboxLocation` specifies a checked payload borrow. The latter retains an owner
root and uses existing readonly and nonescape rules. All require explicit fault
edges; only boxing introduces an allocation safepoint. Null and dynamically wrong
reference operands remain legal faulting IR.

Payload plans reuse bounded value paths and tracing maps, preserving distinct
TypeDef/image identities even for equal or empty layouts. Root limits count each
box and borrow owner. This establishes authored contracts only. Native execution,
CIL translation, primitive/enum/nullable boxing, reference-form `unbox.any`, boxed
interface dispatch and ValueType reference views remain unsupported. Earlier
profiles and native lowerers reject the new operations.

## Private native boxed payloads

`ObjectCppEmitter.EmitBoxedRuntime` emits a scalar-location program plus boxed
descriptors/helpers for a value catalog sharing its exact reference catalog
(ADR 0071). It uses typed scalar members, existing allocation/collection, Object
reference predicates and checked scalar heap locations. Box allocation copies;
unboxing checks null/ownership/exact identity; whole-copy helpers validate all
references before writing. Empty values retain fresh allocated identity and their
borrowed owner still needs a root. Class and boxed native slots share one limit.

Local O0/O2 ASan/UBSan probes cover copy isolation and mutation against .NET,
foreign/incompatible/corrupt references, empty image-scoped identity, cyclic graph
tracing, borrowed owners, final reclamation, exact/insufficient heap budgets and
object-count recovery. This helper API does not lower boxing IR or translate CIL,
and does not establish generated fault provenance, a public struct ABI, ValueType
reference views, boxed interface dispatch or additional platform compatibility.

## Authored native boxing execution

`ValueScalarLowering.LowerBoxes` and `ObjectCppEmitter.EmitBoxes` connect authored
boxing IR to the private runtime (ADR 0072). Scalar packets and payload slots are
validated against the exact value catalog. Every unbox emits a checked owner root,
including empty payloads; synchronous allocating callees preserve that lifetime.
Packets occupy bounded execution-arena storage. Lowering rechecks aggregate
argument, storage and root budgets, including hidden empty-owner temporaries.

Local O0/O2 ASan/UBSan evidence compares copy isolation, mutation and reference
identity with .NET and verifies collection stress, original fault origins,
unchanged packed output on failure, allocation recovery and exact/one-byte-short
arena capacity. This supports named-struct authored IR. CIL boxing, primitive/enum/
nullable boxing, boxed interfaces and ValueType views remain unsupported.

## Bounded boxed-value CIL

`ValueTranslator.TranslateBoxes` opts into named-struct `box`, `unbox` and value-
form `unbox.any` (ADR 0073). Type identities and visibility are checked before stack
translation; copy and borrowed results remain distinct. Empty C# structs with a
fieldless sequential one-byte size marker now retain zero logical payload slots.
Other sized or packed layouts are still rejected.

Real C# and reduced CIL probes at O0/O2 cover allocated class graphs inside boxed
structs, independent box identities, copied markers, shared reference targets,
return packets and allocating mutable-borrow helpers. Null/type faults preserve
callee/IL origins and packed output; allocation failure cleans up and allows reuse.
Primitive/enum/nullable boxing, reference-form `unbox.any`, generic TypeSpec operands,
boxed interfaces and ValueType views remain unsupported. The CLI is unchanged.

## Authored native delegates

`scalar-delegates-ir-v1` provides fresh delegate identity, traced captures, exact
static/closed-instance Int32 or void calls, checked receiver ownership and fault
provenance (ADR 0079). O0/O2 ASan/UBSan probes compare .NET behavior and verify
collection, arena/heap limits and cleanup. CIL binding and a public delegate ABI
remain unsupported at this checkpoint.

## Bounded delegate CIL

`delegate-values-cil-v2` binds public sealed nongeneric or exactly closed generic
single-target delegates through paired ldftn/newobj and callvirt Invoke (ADRs 0080,
0136). Parameters are Int32;
returns are Int32 or void. Exact static/nonvirtual instance captures compose with
class fields, generic structs and boxing. O0/O2 sanitizer probes verify .NET parity,
collection of captured cycles, null/target fault provenance and failure cleanup.
The owned Action<int,int> mapping and generic application delegates add 1,764
repeated native results. Specialization keys keep identical-shaped delegates
distinct; the integer-object composition supports exact casts/type tests and
invalid-cast messages. The explicit delegate-only wrapper retains its cast limit.
Private compiler-generated closure visibility, virtual capture, multicast,
reference/wide/value signatures and variance conversions remain diagnostic. No CLI
or public native delegate ABI is added.

## Managed exception metadata

`ReadManagedExceptionMethodInput` retains validated regions and catch signatures,
with 256 clauses per method and shared metadata work bounds (ADR 0081). Stage-A
filters must be acyclic and read only locals assigned in the current evaluation;
general filters remain diagnostic. This API does not establish native managed
exception objects or handler execution.

## Authored exception contracts

`managed-exceptions-ir-v2` validates intrinsic exception identities, explicit throw/
rethrow/leave/cleanup/filter terminators, region entry stacks and protected transfers
(ADR 0082). Type-test filters are pure and acyclic; saved identities reserve bounded
phase A roots. Earlier native profiles reject these operations. This checkpoint does
not execute managed handlers or expose an exception ABI.

## Private native exception storage

`EmitExceptionRuntime` emits v17 helpers for exception allocation, supplied message
references, rooted pending identity and saved-state restoration (ADR 0083). O0/O2
ASan/UBSan checks cover ownership, collection, exact heap capacity and emergency
allocation fallback. The emergency object is charged to the normal instance quota.
This helper gate does not execute handler IR or supply default/resource messages.

The authored `scalar-managed-exceptions-ir-v2` profile now executes catch, pure type
filters, throw/rethrow and leave/finally/fault through explicit native continuations
([ADR 0084](decisions/0084-native-exception-dispatch.md)). Messages preserve supplied UTF-16
and bounded invariant defaults; O0/O2 sanitizer checks cover rooted identity and
cleanup. CIL exception translation and a public structured-error ABI remain separate.

## Bounded exception CIL

`ReadExceptions` → `TranslateExceptions` → `LowerExceptions` → `EmitExceptions`
provides the compiler-library path described in ADR 0085 (translation performs its
own discovery). Twelve C# entry probes plus uncaught/OOM cases match local .NET at
O0/O2 with ASan/UBSan. Messages are invariant English; custom exceptions are public
top-level nongeneric fieldless declarations with verified forwarding constructors.
Only the selected Exception/8 runtime-fault parameterless and string constructors,
Exception.Message, String.Length/equality and pure type-test filters are admitted.
The packed boundary remains Int32/status/origin; CLI and public exception ABI are unchanged.

Exception message identity is covered by ADR 0086: formatted type-name defaults are
fresh per getter; fixed resources are shared within the instance, separately from
user literals; supplied and contextual cast messages retain their references.
Nine resource roots are bounded and charged to normal heap quotas.

## Milestone 5 completion

The bounded compiler-library milestone is complete (ADR 0087). The final sequential
local suite passed 1,855 tests, with zero failures/errors/skips/unrun tests, on .NET
10.0.11 and macOS ARM64/Apple Clang 17.0.0. Native O0/O2 sanitizer/collection probes
retain the 2 KiB stack gate. See the milestone record for the complete supported
boundary and deferred capabilities; this is not a full CLR or platform guarantee.

The composed compiler API is:

```csharp
var translated = ValueTranslator.TranslateExceptions(resolver, assembly, entry, runtimeLibrary);
// Inspect translated.Diagnostics and require translated.Success before lowering.
var scalar = ValueScalarLowering.LowerExceptions(translated.Program!);
var cpp = ObjectCppEmitter.EmitExceptions(scalar);
```

`entry` is the selected static Int32 MethodDef; `runtimeLibrary` is an explicitly
resolved implementation assembly. Translation performs bounded discovery and validates
the complete supported closure before emitting any native source.


Owned Guid support (ADR 0138) now covers numeric constructors, Empty, typed
identity/hash/unsigned comparison and standard D/N/B/P/X formatting through
managed IL. Four Int32 words are an internal representation, not a public layout
or serialization ABI. Boxed value type tests, parsing, random generation and
byte/span APIs remain outside the admitted profile. Exception IR v2 and native
v22 add exact System/Arithmetic/Format resources, including distinct explicit-null
behavior and resource identity. Native Guid evidence is pinned to the local
little-endian .NET oracle; this does not complete the real-kernel milestone.


The integer-object profile now supports closed inline values in heap fields
(ADR 0139), including nested references/Int64, inheritance, empty values and
checked borrows. Scalar components preserve declaring field identity and trace
embedded references without boxing. Direct struct-return call results assigned
to heap fields remain diagnostic pending destination/exception-order lowering;
ordinary value copies and explicit byref assignments are tested.


The direct-core dictionary trial (ADR 0141) adapts pinned bucket-chain lookup,
insertion, overwrite and resize into Cilica.Library.TrialDictionary. Five probes
cover closed integer and IEquatable struct keys, including mutable callbacks and
exceptions. Exact private helper grants select managed key bodies. System.Dictionary
reference mapping and collection/array interfaces remain pending; the trial's own
name prevents a partial collection contract from being advertised as compatible.


Selected-runtime vector interfaces now support casts, Count and indexed reads
through exact granted managed helpers (ADR 0142). Int32/reference vectors preserve
reference covariance, Int32/UInt32 bit identity, bounds messages and null checks.
Object layout v7, interface plan v2 and native v24 carry explicit array assertions.
Enum elements and unimplemented interface operations remain diagnostic. This does
not yet provide public collection implementations or native kernel equivalence.


The bounded public Dictionary mapping now preserves its exact nominal interfaces
and admitted method flags (ADR 0143). Default construction, Count, TryAdd,
TryGetValue, ContainsKey, setter overwrite and collection flags/SyncRoot are tested
through real reference calls. Missing bodies fail at build time. Nongeneric array
collection flags and SyncRoot use exact granted managed helpers, with layout v8
and native v25. The real kernel now reaches StringBuilder; native equivalence
remains pending.


Immediately proven sealed receivers now narrow public zero-argument virtual calls
before body discovery (ADR 0144). Canonical slots, typed receivers and null checks
remain intact. Branch/handler entry, nonpublic declarations and unproven Object
parameters do not establish a proof. This supports managed overrides without
claiming a general Object.ToString implementation.


Milestone 6 library additions (ADRs 0145–0147) now include bounded StringBuilder
char/string append and UTF-16 snapshots, exact Array.Empty<T> identity for Int32/
reference vectors, and managed List<T> growth/indexing with live read-only views.
Array interface Set and fixed-size Add preserve bounds, covariance and exception
semantics. Unsupported collection members remain build errors. These changes
have targeted O0/O2 .NET differential and native sanitizer evidence; they do not
establish real-kernel native equivalence.

Exact single-target delegates now accept Int32/reference parameters (ADR 0148),
including strings, interfaces and supported vectors, while retaining Int32/void
returns. Caller/callee managed roots protect arguments across allocations. Target
signatures match exactly; wide/value/byref parameters, recursive
signature closures and binding variance remain diagnostic. AC2.1 below adds
reference returns. Profiles at the ADR 0148 checkpoint were
object-layout-v9, typed-reference-ir-v7, interface-dispatch-plan-v3 and
object-frames-native-v29. Initialized static callback caches remain pending.


Ordinary Int32/reference static fields without type/module initialization now use
typed load/store IR and instance-owned, quota-charged native slots (ADR 0149).
Reference slots are collector roots, and exact closed owner identities separate
generic state. Readonly writes, addresses, initialized types and narrow/wide/inline
static values remain diagnostic. Object layout v10, typed reference IR v8 and
native v30 encode this foundation; initializer execution is still pending.


TypeInitializationException(String, Exception), TypeName and Exception.InnerException
are now exact granted integer-object operations (ADR 0150). Construction eagerly
formats the pinned invariant message and retains a precisely traced name/inner
payload; null names read as canonical empty strings. Managed/scalar exception IR
v3, typed reference IR v9 and native v31 encode this addition. Initializer execution,
failure caching, serialization and general Exception constructor overloads remain
outside this slice. Local O0/O2 sanitizer comparisons cover payloads and failures.


Authored integer-object IR can now request bounded per-instance initialization
(ADR 0151). Exact private initializer bindings, dedicated call/fault safepoints,
reentrant checks and retained managed/fatal outcomes use typed reference IR v10,
object operations IR v11 and native v32. Wrapper preparation precedes user IL;
preparation failure can retry, while started initializers never repeat side effects.
Native O0/O2 sanitizer tests cover roots, failure identity and quota/corruption.
CIL trigger insertion and initialized metadata admission remain pending.


BeforeFieldInit class initialization is now connected to CIL field reads/writes
(ADR 0152). Exact closed .cctor bodies execute through the ordinary compiler, with
readonly owner checks and a granted failure-wrapper constructor. The permitted
timing choice is first static field access. Generic failure names use the definition
name while state uses the closed owner identity. Four probes provide 1,176 local
O0/O2 differential results. Strict type/module initialization, initialized values,
static addresses and broader static storage remain diagnostic. The concrete kernel
still requires private nested cache access and has no native transcript evidence.


Nongeneric private nested cache owners now use explicit bounded declaring-type
chains (ADR 0153). Same-image containing/nested callers can access their admitted
public fields and public/assembly methods. Detached validation rejects contradictory
chains and visibility claims; missing facts grant no lexical privilege. Object
layout v11 and object operations IR v12 retain this evidence. Three probes provide
882 local O0/O2 differential results. Protected/generic lexical access and broader
private member access remain diagnostic. The unchanged kernel now reaches its
ValueTuple`2 dependency; native equivalence remains pending.


Bounded ValueTuple<T1,T2> fields/construction, typed equality and hashing now execute
as managed code (ADR 0154). Exact private key grants select managed ordinal string
operations as well as existing integer/inline keys. Equality copies component
receivers; hashing addresses the original fields. Fixed private hashes preserve
key consistency without promising cross-runtime numeric hashes. Four probes
provide 1,176 repeated local O0/O2 results. Object equality, tuple formatting,
comparison and structural APIs remain diagnostic. The concrete kernel next reaches
String.Format; no native kernel equivalence is claimed.


Boxed interface-bearing values now preserve authenticated interface ancestry and
variance in native descriptor predicates (ADR 0155). Fresh-box implicit conversions
are typed checked casts; payload storage, identity, unbox copies and precise roots
retain existing behavior. Three probes provide 882 repeated local O0/O2 results.
Boxed method dispatch remains diagnostic until its payload receiver is lowered.
Boxed value IR v2, scalar boxing IR v3 and native v33 retain the added scope.


String.Format(String,Object) now has an exactly granted path for a directly boxed
named non-IFormattable value (ADR 0156). Same-block operand proof selects an ordinary
managed generic parser and a private exact payload borrow/ToString call. Boxing
allocation, repeated callback mutation and fault ordering are retained. Four probes
provide 1,176 local O0/O2 comparisons. Aliases, other argument kinds, providers,
IFormattable and general boxed dispatch remain diagnostic. The kernel next requires
StringComparison; native transcript equivalence remains unproven.


The exact String.Equals(String,String,StringComparison) overload now accepts a
directly proven Ordinal constant through an approved grant (ADR 0157). It reuses
existing UTF-16 equality; nulls and operand evaluation retain existing semantics.
The pinned enum declaration is mapped, but other modes, aliases and general enum
storage remain diagnostic. One probe supplies 294 local O0/O2 comparisons. The
kernel now reaches the bounded metadata work limit; native transcripts remain pending.


String.IsNullOrWhiteSpace now executes as ordinary managed UTF-16 code (ADR 0159).
Selected String static IL bodies use normal discovery and validation; instance
methods and initialization retain explicit restrictions. Object operations IR v13
records this admission. Every UTF-16 code unit and mixed/null/empty strings match
.NET at O0/O2 with ASan/UBSan and forced collection. No native intrinsic was added.
The concrete kernel again reaches the unchanged metadata work bound; native
transcript equivalence and implementation/filter closure remain pending.


The exact readonly String.Empty field now requires a declaration-specific field
grant and maps to the runtime's canonical per-instance empty literal (ADR 0161).
O0/O2 sanitizer tests preserve identity with empty literals, Concat and StringBuilder
output, plus bounds faults and collection safety. Writes, missing approval and
altered field shapes remain diagnostic. The concrete kernel reaches an aggregate
compiler limit; native transcript equivalence remains unproven.


The shared method closure cap is now 512 after the concrete kernel exhausted the
previous declaration count while remaining within its byte, instruction and metadata
budgets (ADR 0162). Exact/excess discovery, detached validation and full-capacity
native O0/O2 checks pass. All other aggregate and runtime limits are unchanged;
interface declarations, facts and bodies continue to share the method cap.


Exact externally visible Int32-backed enums now use existing scalar storage for
signatures, fields, copies and borrows in the integer-object profile (ADR 0163).
Metadata shape and selected Enum ancestry are validated; closed generic signatures
retain nominal identity. Two native probes provide 588 repeated O0/O2 comparisons.
Other widths, enum boxing/arrays and general Enum APIs remain diagnostic. The
concrete kernel next needs Boolean field storage; native transcripts remain pending.


Narrow Boolean/Char/byte/short fields now have explicit read/write casts in the
integer-object profile (ADR 0164). Private Int32 slots preserve managed truncation,
sign extension and copy/zero semantics without a public layout claim. Thirteen
probes supply 1,662 repeated O0/O2 native comparisons, including stores without IL
conversions. Narrow addresses and arrays remain diagnostic. The kernel has reached
another metadata work bound; native transcript equivalence remains pending.


Closed value-vector storage now has invariant nominal element identity and an
explicit bounded scalar shape (ADR 0166; object layout v12/native v34). Typed
Int32/Int64/reference buffers preserve zeroing, exact heap quotas, per-instance
empty identity and collection of every element reference. O0/O2 sanitizer probes
cover storage and reclamation. Value element operations and CIL/library discovery
remain pending; this does not yet compile the kernel's `PlayerId[]` path.


Scalar value-vector component borrows now retain owner roots and validate exact
descriptors, indices, leaf kinds and reference predicates (ADR 0167; scalar location
IR v2/native v35). Twelve focused/composed checks include 288 repeated O0/O2
.NET/native outcomes with sanitizers and forced collection. Logical value element
operations and CIL/library admission are still pending.


Logical closed value-array loads/stores and synchronous element borrows now lower
to checked scalar components (ADR 0168; value array IR v2/value location lowering
v4). Shapes derive from the validated struct plan; copies snapshot fields, roots
retain references/owners, and empty structs retain null/bounds checks. Thirty-four
focused/composed checks include 576 repeated O0/O2 .NET/native outcomes. CIL and
collection/interface admission remain pending.


Closed value-array CIL now admits newarr, typed ldelem/stelem, value ldelema and
class/struct array fields; Array.Empty retains exact value identity (ADR 0169).
Seventy-nine focused/regression checks include 1,470 new repeated O0/O2 native
outcomes against .NET with sanitizers and forced collection. Primitive-width,
enum/nested arrays and readonly-prefixed element borrows remain diagnostic.
The concrete kernel passed PlayerId allocation and reached its bounded metadata
ledger in PriorityWindowRequest construction; native transcripts remain pending.


Boxes confined to comparisons, unboxing and proven ordinary helper parameters can
now coexist with unrelated interface dispatch (ADR 0172; boxed value IR v3/scalar
boxed IR v4). Detached logical and scalar validators independently check all uses
within bounded work. Escaping aliases and actual boxed interface calls remain
diagnostic. Two probes add 588 repeated O0/O2 .NET/native outcomes. The concrete
kernel next requires value-array interface assignment; native transcripts remain
unproven.


Value-array interfaces now preserve exact closed value arguments and use the
selected managed ArrayInterface<T> bodies (ADR 0173; object layout v13). Scalar
helper signatures derive from ordered element leaves, including explicit empty
values. Four probes add 1,176 repeated O0/O2 .NET/native outcomes with sanitizers,
forced collection, nested reference-bearing structs, fixed-size faults and live
List/read-only views. Unrelated value/reference interface casts remain false.
The concrete kernel now reaches switch in ResolutionKernel.Step; native transcript
equivalence remains pending.


The integer managed-object CIL v2 profile lowers bounded switch tables to existing
comparison branches (ADR 0174). Selector consumption, remaining stack values,
duplicate/default targets and exception-region ordering are preserved; synthetic
comparison offsets occupy only the existing table operand span. Five probes add
1,488 repeated O0/O2 native outcomes with sanitizers, forced GC and unwind checks.
Malformed targets, non-Int32 selectors and expanded block overflows are diagnostic.
The kernel now reaches readonly constructor receiver validation; full native
transcript equivalence remains pending.


Class constructors may now branch after one immediate-base call in their entry
prefix (ADR 0175; object operations IR v14). Body paths cannot re-enter that prefix,
repeat initialization or catch its failures. Stable direct receiver provenance
survives joins only when its argument slot is never addressed. A reduced readonly
null-coalescing constructor matches .NET in 294 repeated O0/O2 native outcomes;
mutable receiver slots and detached reinitialization/re-entry mutations are rejected.
The real kernel next reaches cast exception type-name resource validation.


Cast exception type names now admit compiler-generated angle-bracket names while
retaining existing length/text budgets (ADR 0176). A closed anonymous-type probe
compares the complete UTF-16 message against .NET at O0/O2 (294 repeated outcomes).
The real kernel now passes translation and detached IR validation, and native C++
emission succeeds. Native execution/transcript and final closure gates remain.


Milestone 6e now matches the unmodified private kernel's ten-step .NET transcript
against native O0/O2 builds, normally and with collection before every allocation.
All 10,752 scalar/canonical UTF-16 observations pass under ASan/UBSan, with stable
retained roots across repeats. Evidence is pinned to the source build and selected
6d.23 library; final implementation/filter closure and composed review remain.


Milestone 6 is complete for the bounded required-library/kernel profile (ADR 0177).
The public composition regression adds 294 repeated O0/O2 results combining
value-bearing collections, formatting, type-test filters, finally and forced GC.
The final 2,632-test scope is covered by the full local run and passing targeted
reruns after three stale boundary expectations were corrected; no production
limits were increased. Public ABI/mobile/browser, general filters and broader
library/language compatibility remain outside this completion.


## Bounded runtime type identity (ADR AC0001)

Profiles at the AC0001 checkpoint were `object-layout-v14`, `object-operations-ir-v15`
and `object-frames-native-v36`; other composed profiles retain their versions.

| Surface | Implemented bound |
| --- | --- |
| `typeof` / type `ldtoken` | Closed reference classes/interfaces, closed reference generics and admitted vectors; no value/open/method/field tokens |
| `Object.GetType()` | Exact dynamic descriptor identity, including supported strings, vectors and boxes; null throws; no host reflection |
| Handles | Traced RuntimeTypeHandle copies, Type.TypeHandle and Type.GetTypeFromHandle; default handle resolves to null |
| Type equality/hash | Canonical instance-local reference identity, operators and typed/object Equals; stable implementation hash, no numerical .NET parity |
| `Dictionary<Type, T>` | Existing supported constructor/Count/TryAdd/TryGetValue/ContainsKey/setter surface, subject to existing value-storage bounds; null keys reject |
| Object virtual equality/hash | Ordinary class receivers with known static class type; Object aliases and special runtime receiver dispatch remain diagnostics |
| Abstract ancestry | Closed integer-object catalog and inherited ordinary virtual bodies; abstract allocation/method bodies remain rejected |

Four identity fixtures give 1,176 repeated O0/O2 .NET/native comparisons, plus
exact/short execution-frame checks, forced GC and ASan/UBSan. Cache boundary probes
cover heap accounting, lifetime roots, instance isolation and corrupted foreign
slots. Missing grants, unsupported reflection/tokens, abstract allocations and
forged detached IR fail validation. The unchanged Alchemy engine metadata probe
now stops at missing `System.Nullable<T>` after passing the former Type mapping
blocker. This is not a full saved-game native replay or platform-portability claim.


## Alchemy compatibility baseline (AC0)

The [gap ledger](compatibility/alchemy-gap-ledger.json) records 260 observed
reference declarations and 52 probes from the bounded private audit, plus two
mapping-only experiments. It assigns follow-on slices and retains unresolved
observations; mapped declarations are not proof of executable library support.

`CompatibilityGapTests` runs 47 public, application-independent probes: 39 assert
specific current rejection diagnostics and eight compare native output with .NET
under forced collection at O0/O2. These are regression controls for future slices,
not full-game compatibility. The [AC0 outcome](milestones/06-library-and-kernel.md#ac0-public-probe-and-ledger-outcome)
records the integrated milestone 7 revision, diagnostic differences from the
private IL fixtures, validation and reproduction commands. No additional language
or platform support was implemented by establishing this baseline.


## Bounded nullable values (AC1a)

The owned core admits Nullable<T> construction, HasValue, Value and both
GetValueOrDefault overloads for supported closed value payloads. Local O0/O2
sanitizer/forced-GC evidence covers all supported integer scalar widths,
Boolean/Char, public Int32 enums, Guid, IDs and reference-bearing structs;
copy/return semantics, materialized heap fields and value arrays reuse the existing
value profile. The empty Value exception matches the selected invariant resource.

AC1b.1 subsequently admits nullable box/unbox.any for already boxable named
structs and implements direct conversion-operator bodies. AC1b.2 adds supported
integer/Boolean/Char and public Int32-enum boxes. AC1b.3 adds exact boxed type
tests, including nullable targets. Raw scalar/nullable unbox, key null/equality/hash selection, Equals, GetHashCode and
ToString remain diagnostics.
Existing internal/non-Int32 enum, static inline-field and direct-return-to-heap
limits remain. An ordinary struct containing a nullable field can still use
ordinary boxing. [ADR AC0002](decisions/AC0002-nullable-value-storage.md) records
these boundaries and tests. The 47-probe public baseline now has ten native
controls and 37 explicit rejection expectations; historical AC0 counts above
remain its original evidence.


## Nullable named-value boxing and conversions (AC1b.1)

Empty nullable boxes produce null without an allocation; present boxes retain the
underlying value's identity. Null unboxing produces an empty nullable, while
non-null unboxing checks the underlying type and copies its payload. Wrong-type
messages distinguish Nullable<T> from direct T casts. Named empty/wide structs,
Guid, closed generic/reference-bearing values and object arrays have native
evidence, alongside representative lifted integer expressions and direct operator
calls. Primitive/enum boxes and raw nullable unbox remain explicitly unsupported.

[ADR AC0003](decisions/AC0003-nullable-boxing-and-conversions.md) records the
managed-helper design, diagnostic flag and exact limits. The 27-probe nullable
suite passes 7,938 repeated comparisons on macOS ARM64 at O0/O2 with forced GC
and ASan/UBSan; heap-budget tests cover no-allocation empty boxes and failure
recovery. This extends the nullable suite, not the private saved-game replay claim.


## Integer and enum boxes (AC1b.2)

The selected Boolean, Char, signed/unsigned 8/16/32/64-bit integers, and already
supported public nongeneric Int32 enums now admit box/unbox.any, including their
nullable forms. Runtime-owned scalar slots preserve nominal box identity. Ordinary
unboxing allows Int32/Int32-enum compatibility; nullable copies require exact
underlying identity. Narrow normalization and all 64 bits have native evidence.

Scalar/nullable type tests are extended by AC1b.3 below; raw unbox remains
diagnostic. AC1b.2 conservatively rejected closures combining scalar boxes with
interface operations because primitive contracts are incomplete; AC1b.4 below
admits cases with proven confined boxes.
This does not admit new enum widths/visibility or general primitive APIs.
[ADR AC0004](decisions/AC0004-nominal-scalar-boxes.md) records the exact boundary.

AC1b.2 native v39 evidence includes 18 scalar probes (5,292 comparisons), 31
nullable probes (9,114), O0/O2 sanitizers/forced GC, and 146 focused regression
checks. The larger nullable compile is split into batches of at most 16, preserving
the 30-second compile timeout. Public baseline counts remain ten native controls
and 37 expected gaps; private saved-game replay is still open.


## Exact boxed type tests (AC1b.3)

isinst/castclass now admit the existing scalar/enum and named-struct box types,
including Guid and closed/reference-bearing values. Nullable<T> tests match T's
exact box descriptor. A successful test preserves object identity and allocates
nothing. Null isinst returns null; null castclass succeeds; a wrong non-null
castclass throws. Type tests do not permit ordinary unboxing's Int32/enum
compatibility. Failed castclass Nullable<T> names T, unlike nullable unbox.any.

[ADR AC0005](decisions/AC0005-boxed-type-tests.md) documents the checked Object
result view, descriptor-only roots, closed constraints and fault distinction.
Nineteen C# and ten direct IL native probes provide 8,526 repeated .NET comparisons
at O0/O2 with ASan/UBSan and forced GC on macOS ARM64 (native v40). Detached and
malformed-metadata checks guard both compiler stages. Raw scalar/nullable unbox,
scalar interface views/dispatch, nullable keys and general exception filters remain
diagnostic. The 47-probe public baseline and historical private replay blockers
are unchanged. Full AC1b and full saved-game replay remain unverified.


## Confined scalar boxes and interface composition (AC1b.4)

Scalar boxing can now coexist with independent interface calls, casts and tests
when the compiler proves every scalar box stays confined to supported operations.
Exact boxed type tests preserve an alias that is followed by the proof; nullable
copy helpers are allowed when their references remain confined. A scalar descriptor
used only as a test target is harmless. The proof runs at both validation stages
and retains its existing work/depth limits.

Direct scalar interface tests/casts/dispatch, reference returns, local/heap/edge
escapes, addressed parameters and recursive proofs remain diagnostic in these
compositions. This includes general nullable boxing helpers returning object
references. [ADR AC0006](decisions/AC0006-confined-scalar-box-uses.md) records the
boundary. Thirteen native probes pass 3,822 repeated .NET comparisons on macOS
ARM64 at O0/O2 with forced GC and ASan/UBSan; 129 existing regressions also pass.
Boxed IR v7/scalar boxed IR v8 change admission; emitted native v40 is unchanged.
Historical private audit counts do not prove full replay closure or raw-unbox
absence. Full AC1b and saved-game replay remain open.


## Exact reference delegate results (AC2.1)

The integer-object composition admits managed reference results from exact
single-target application delegates, including closed generic declarations.
Object/String, admitted classes/interfaces and supported vectors use the normal
rooted reference-return path. Null results are valid; callback faults propagate;
argument evaluation precedes a null invocation fault. Targets must match parameter
and return types exactly, even when C# permits a covariant method-group binding.

Profiles: object layout v15, delegate value/scalar IR v2, native v41. The typed
return is part of serialized catalog identity and closure validation. Unsupported
wide/value/byref results and recursive delegate signatures remain diagnostic;
framework Func/Action/Predicate/Comparison additions remain AC2.2. The narrower
standalone delegate CIL profile retains Int32/void results. No public ABI changes.

Thirteen new probes and public RefDelegate provide 4,116 repeated native/.NET
comparisons on macOS ARM64 at O0/O2 with ASan/UBSan, forced collection and frame/
root/heap cleanup checks. The public baseline has 36 expected gaps and 11 native
controls. See [ADR AC0007](decisions/AC0007-reference-delegate-results.md). These
results do not establish private command closure or full saved-game replay support.


## Required framework delegate contracts (AC2.2)

Owned Action<T>, Func<T,TResult>, Func<T1,T2,TResult>, Predicate<T> and Comparison<T>
now join Action<T1,T2>. Their pinned variance declarations and constructor/Invoke
mappings are verified. The integer-object composition admits exact Int32/reference
parameters and Int32/Boolean/reference/void results. Boolean uses the ordinary
normalized Int32 carrier, with declared scalar target identity checked before
lowering. No delegate variance conversions, multicast, Boolean parameters, other
narrow/wide/value/byref signatures or additional arities are claimed.

The composed CIL profile is integer-managed-objects-cil-v3. Native v41, layout v15,
delegate value/scalar IR v2 and public embedding ABIs are unchanged. Core source
and approved manifest bytes change, but intrinsic requests do not. Fifteen new
framework probes plus public FuncDelegate provide 4,704 repeated native/.NET
comparisons at O0/O2 with sanitizers and forced collection on macOS ARM64. Twelve
focused test cases and 112 distinct existing regressions pass.

The ledger has 35 gaps and 12 native controls. Where now reaches the missing
Enumerable declaration after Func`2 maps; LINQ and full private saved-game replay
remain incomplete. See [ADR AC0008](decisions/AC0008-framework-delegate-contracts.md).


## Concrete list mutations and snapshots (AC3.1)

List<T>.Insert, RemoveAt, Clear and ToArray now execute as managed library code
for Int32, admitted references and supported inline values. Shifts preserve order;
removal/clearing retain capacity and release reference slots. Nonempty snapshots
copy elements; empty snapshots share per closed T and are distinct from
Array.Empty<T>(). The empty cache uses existing traced per-runtime static storage.

Eleven new probes plus public ListRemoveAt/ListToArray provide 3,822 repeated
native/.NET comparisons with O0/O2, ASan/UBSan and forced collection on macOS ARM64.
Four GC checks verify reclamation while lists remain alive, including references
inside structs. 61 existing regression checks pass. The ledger now has 33 gaps
and 14 native controls. Profiles, grants and embedding ABIs are unchanged.

Interface mutation compositions, including read-only mutation faults, still hit
the explicit array-helper discovery gate. Two rejection fixtures retain that exact
diagnostic; concrete list support does not imply arbitrary IList mutation support.
CopyTo, equality-based search/removal, bulk operations and enumeration remain
outside this slice. See [ADR AC0009](decisions/AC0009-list-mutation-and-snapshots.md).


## Array-interface mutations (AC3.2)

Exact granted managed helpers now implement generic/nongeneric array Insert and
RemoveAt fixed-size faults. ICollection<T>.Clear throws the pinned read-only
exception; IList.Clear resets all typed elements and preserves array identity and
length. Int32, admitted references and supported inline values are covered.
The previously blocked list/read-only interface mutation probes now execute.

Thirteen array probes plus those two list probes provide 4,410 repeated O0/O2
.NET/native comparisons with ASan/UBSan and forced GC on macOS ARM64. Four GC
checks verify reclamation while arrays remain alive, including references inside
values. Exact grant denial, detached signature checks and 125 existing regressions
pass. Interface plan v4 adds the helper recipes; five grants are appended, while
native v41 and public ABIs stay unchanged. The public ledger remains 33 gaps and
14 controls; its supplemental interface mutation blocker advances. Broader
collections and private replay remain incomplete. See
[ADR AC0010](decisions/AC0010-array-interface-mutations.md).


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
See [ADR AC0011](decisions/AC0011-dictionary-removal-and-clear.md).


### Integer math and typed comparisons (AC5a.1)

Owned managed Math implements signed Int32/Int64 Abs and signed/unsigned 32/64-bit
Min/Max. Typed CompareTo methods preserve exact IComparable<T> declaration flags
and use existing scalar receiver lowering. Explicit minimum-value guards preserve
Abs overflow and its message; comparisons avoid overflowing subtraction.
Eleven authored plus three public probes supply 4,116 repeated O0/O2 .NET/native
comparisons; exact overload checks and 35 existing regressions pass. The ledger
has 29 gaps and 18 controls. No profile, grant or ABI changes. Broader math,
object comparison, comparer execution, Guid helpers and full replay remain open.
See [ADR AC0012](decisions/AC0012-integer-math-and-comparison.md).


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
See [ADR AC0013](decisions/AC0013-list-and-array-search.md).


### Ordinal text comparison (AC7b.1)

String.CompareOrdinal and IsNullOrEmpty now have owned managed bodies. Comparison
preserves the pinned .NET 10.0.11 numeric result, including short-string zero
padding and odd-prefix terminators, using safe UTF-16 indexing. Four authored
probes plus the public comparison provide 7,794 repeated O0/O2 native/.NET results
with forced GC and ASan/UBSan. All 71 relevant baseline/string regressions pass.
The ledger has 27 gaps and 20 controls. No profile, grant or ABI changes; cultural,
ignore-case and other text APIs remain separate work. Private replay is unverified.
See [ADR AC0014](decisions/AC0014-ordinal-text-comparison.md).


### Fixed-invariant numeric append (AC7b.2)

StringBuilder.Append accepts signed/unsigned 32/64-bit integers through existing
managed integer formatting and string append. This bounded core fixes numeric
text to invariant culture; it does not implement ambient CurrentCulture behavior.
The .NET oracle is explicitly pinned and restored by the tests. Six probes plus
public StringBuilderInt supply 2,058 repeated O0/O2 native/.NET comparisons, and
37 builder/baseline regressions pass. Forced GC, sanitizers, frame and cleanup
checks remain enabled. The ledger has 26 gaps and 21 controls. No new grants or
profiles; full replay remains unverified. See [ADR AC0015](decisions/AC0015-invariant-numeric-append.md).


### Array read-only views (AC3.5)

Array.AsReadOnly now returns a live nonempty view or a shared per-closed-type empty
view. Private caches use existing instance-owned static roots. Null validation,
covariance, original-array SyncRoot and readonly mutation faults match the pinned
runtime. Six probes plus the public baseline provide 2,058 repeated O0/O2 native/
.NET results; two cache lifetime/isolation checks and 54 existing regressions pass.
Sanitizers and forced GC remain enabled. The ledger has 25 gaps and 22 controls.
No new grants or profiles; enumeration, copying and full replay remain unfinished.
See [ADR AC0016](decisions/AC0016-array-readonly-views.md).


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
See [ADR AC0017](decisions/AC0017-concrete-list-enumeration.md).


### Dictionary key and value views (AC4.2)

Dictionary Keys/Values now expose cached live views through concrete, generic,
read-only and nongeneric contracts. They preserve identity, Count, SyncRoot and
read-only faults. Key lookup forwards ContainsKey; ContainsValue and value-view
lookup scan live entries with copied equality receivers. Nulls and removed slots
are handled explicitly. Ten probes plus public DictionaryKeys supply 3,234 repeated
O0/O2 native/.NET comparisons; two view-cycle GC checks, exact nested metadata,
unknown-equality rejection and 41 existing regressions pass. The ledger has 23
gaps and 24 controls. No new grants or native profiles. Copying, enumeration and
full replay remain incomplete. See [ADR AC0018](decisions/AC0018-dictionary-views.md).


### Concrete dictionary enumeration (AC4.3)

Dictionary.GetEnumerator now exposes its exact nested value-type contract. It
walks active entry slots, copies the current KeyValuePair and resets it at the end.
Only insertion of a new key increments the version: removal, clearing, overwrite
and failed TryAdd remain valid during iteration. Eleven probes plus the public
foreach baseline supply 3,528 repeated O0/O2 native/.NET comparisons, including
current-value survival across forced GC after clearing the dictionary. Forty-three
existing regressions pass with sanitizers enabled. The ledger has 22 gaps and 25
controls. No new grants/profiles; boxed interface enumeration, view enumeration
and full replay remain unfinished. See [ADR AC0019](decisions/AC0019-concrete-dictionary-enumeration.md).


### Concrete dictionary view enumeration (AC4.4)

KeyCollection and ValueCollection now expose their exact nested enumerator types.
They retain only the selected current key or value, preserve dictionary mutation
rules, copy independently and reset current at completion. Eight probes supply
2,352 repeated O0/O2 native/.NET comparisons; four precise GC checks prove the
key/value retention difference after clear and release at iteration end. All 35
view/baseline regressions pass with sanitizers and forced GC. No new grants or
profiles. The ledger remains 22 gaps and 25 controls; interface/array enumeration
and full replay remain unfinished. See [ADR AC0020](decisions/AC0020-concrete-view-enumeration.md).


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
and 29 controls. See [ADR AC0026](decisions/AC0026-internal-int32-enums.md).


### Metadata constant layouts (AC5b.2)

Validated primitive constant declarations no longer occupy class or struct storage.
Five probes pass 1,470 repeated O0/O2 native/.NET comparisons with sanitizers and
forced GC; 12 metadata boundaries, 97 layout/enum regressions and 21 baseline checks
pass. Direct field operations and malformed constants remain rejected. The frozen
state probe now reaches a private nested enum; replay remains incomplete. See
[ADR AC0027](decisions/AC0027-metadata-constant-layouts.md).


### Direct string calls (AC7b.3)

Exact String.Length/Chars now accept Roslyn's direct call form as well as callvirt.
Existing declaration and intrinsic permissions remain enforced. New fault/UTF-16
probes and direct-call layout regressions pass 2,646 repeated O0/O2 comparisons;
150 string/admission checks pass with sanitizers and forced GC. No profile/grant
changes. See [ADR AC0028](decisions/AC0028-direct-string-instance-calls.md).


### Nested enum storage (AC5b.3)

Exact nested Int32 enum payloads now use admitted layouts with separate lexical
operand access. Three probes pass 882 repeated O0/O2 native/.NET comparisons;
eight boundary checks and 30 previous enum/lexical checks pass. The frozen state
probe now reaches the unchanged 256-type catalog limit. Static value storage and
full replay remain unfinished. See [ADR AC0029](decisions/AC0029-nested-enum-storage.md).


### Wide static storage (AC5b.4)

Int64/UInt64 static fields preserve all bits in per-instance storage. Five probes
pass 1,470 repeated native/.NET comparisons; two persistence/isolation runs, 18
static/default checks, 25 initialization/packet checks and 21 public baseline checks
pass. Native profile v42 and layout v16 account for the wider private slot. Inline
static structs and full replay remain unfinished. See
[ADR AC0030](decisions/AC0030-wide-static-storage.md).


### Static inline-value copies (AC5b.5)

Whole static struct loads/stores now expand through validated value layouts into
per-instance scalar and traced reference slots. Eight probes pass 2,352 repeated
O0/O2 native/.NET comparisons; four GC scenarios, eight packet mutations, the
address boundary, 90 existing regressions and 21 baseline checks pass. Layout v17,
native v42. Frozen EngineStep advances to a nonpublic generic argument; state
lookup still reaches the type limit. Static addresses and full replay remain
unfinished. See [ADR AC0031](decisions/AC0031-static-inline-value-copies.md).


### Generic source-type provenance (AC8.1)

Closed method snapshots now retain declaration signatures/locals, and a bounded
resolver API retains original operand syntax before generic substitution. This
separates explicit private names from opaque type parameters for the next access
slice. All 54 selected metadata/generic checks pass. No new execution capability
is claimed. See [ADR AC0032](decisions/AC0032-generic-type-use-provenance.md).


### Private generic reference transport (AC8.2)

Original explicit type names are checked in their declaration scope before closed
substitution. Legal private reference arguments can pass through opaque generic
calls, locals, fields and delegates; foreign explicit names and member access stay
checked. Four probes pass 1,176 repeated O0/O2 native comparisons with forced GC
and sanitizers; 106 broad and 60 focused checks pass. Private T[] still requires
additional detached access evidence. Frozen EngineStep advances to String.Format;
StateLookup remains at the unchanged type limit. Full replay remains incomplete.


### Detached generic identity foundation (AC8.3)

Closed argument snapshots retain reader-free identity syntax and reproduce the
existing specialization hashes after bounded validation. All 28 selected checks
pass. This supplies identity evidence for later per-instruction array access;
it adds no new executable capability. See ADR AC0034.


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
