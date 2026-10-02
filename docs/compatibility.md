# Compatibility manifest

Recorded 2026-10-02. Capabilities are stage-specific. A translated operation does
not establish native execution or platform compatibility.

| Component/profile | Implemented stage | Boundaries |
| --- | --- | --- |
| Metadata frontend | Inspection, IL structure, explicit binding and bounded audits | Depth-level validation and incomplete dependency reports; no execution guarantee |
| `int32-pending-fault-v1` | Typed IR, C++20 emission and local native differential execution | Explicit static MethodDef and complete direct-call closure; nongeneric owner/method; Int32 parameters/return; initialized Int32 locals; typed block inputs; division/remainder/checked-arithmetic faults; no initializer obligations |
| C++ backend | C-linkage status entry, plus value-only entry for proven pure programs; private helpers, hash/token identity, IL comments and scoped block labels | Primitive pending-fault context; no managed exception objects/handlers; public IR is validated before emission |
| `object-layout-v1` | Bounded metadata snapshots and detached class/field graphs | Int32/CLASS/OBJECT field storage and logical reference slots only; no reference IL, byte layout, allocation, root frames or object execution |
| `typed-reference-ir-v1` | Detached caller-constructed IR validation | Concrete references/null, typed locals/joins/calls/returns; no CIL object translation, constructor/field operations, native emission or execution |
| `object-operations-ir-v1` | Bounded CIL translation and detached operation validation | Constructors, fields and nonvirtual static/instance calls; test-only .NET/IR conformance; no native reference emission, heap, ownership, root frames or collection |
| `object-frames-native-v2` | C++20 emission and local native differential execution | Budgeted execution arena, packed Int32 boundary, bounded native helpers, retained heap, phase A roots and typed branches/backedges; no collector |
| Broader managed runtime and library | Planned | Arrays, strings, GC, exception handlers, library and embedding support are unimplemented |

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
not managed exception objects or messages; catch/filter/finally remain unimplemented.
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
runtime library, then resolves a complete class-field graph. Types use content
hash/TypeDef identity; fields retain that declaring identity and their image-scoped
FieldDef tokens. Logical reference maps distinguish reference fields from Int32.
The immutable output survives reader disposal; input signatures remain
resolver-owned until the build completes. Ordinary layout fields accept Int32,
named CLASS or primitive OBJECT; self/mutual reference cycles are supported.

Classes are concrete, nongeneric, automatic-layout and derived directly from the
selected fieldless `System.Object`; enclosing generic owners are rejected.
Interfaces, other inheritance, packing/native layouts, initialization, reference
assemblies, static/literal/constant/RVA/marshalled/explicit-offset fields and
unsupported signatures fail with no partial model. Readonly and visibility flags
are retained for future access verification; layout inclusion does not grant
executable access. This is not a general CLI verifier or intrinsic classifier.

Bounds: 256 included types, 4,096 aggregate fields, 65,536 metadata inspection
units and at most 64 enclosing types. Field signatures share a 4,096-node decoder
budget per type; base decoding is separate. The model validator rejects forged
identities, duplicate FieldDefs across owners, missing targets, unrelated types,
invalid storage/base shape and aggregate excess. Independent fixtures verify
exact/excess limits, forwarding, cross-image namesakes, ownership and detached
lifetime. See [ADR 0022](decisions/0022-detached-object-layouts.md).

The layout model alone supplies no execution. Target layouts, allocation, ownership,
root frames and native object faults require the separate object emitter below;
collection remains unimplemented. The existing Int32 translation profile, emitted
C++ and native status ABI retain their semantics. `IrType` is now a record carrying a kind and optional concrete class
identity; this early compiler API is not a stable serialization contract.

## Typed reference value-flow foundation

`ReferenceIrProgram` is a detached validation API, not a CIL translator or native
entry. It shares block-local values and ordered statements with the primitive
IR. `ReferenceIrValidator.Validate` checks both the complete method closure and
an `ObjectTypeCatalog` with explicit class roots and their field closure. Catalogs
use the same layout rules and aggregate bounds as `object-layout-v1`; every
included type must occur in declared value/storage types or their field closure,
apart from the selected object base. Class names do not supply identity.

`IrType` distinguishes Int32, a concrete class reference and the null stack type.
Parameters, locals and returns require Int32 or concrete references; null may
occur in temporaries and block inputs. Loads and call results retain exact
declared types. Assignments, returns, arguments and edge copies accept exact
types, null into any reference, and a concrete class into the selected object
base. Object-to-class and sibling-class assignments require later casts.
Validation checks declared joins; it does not infer their common type from CIL.

Reference equality/inequality returns Int32; reference/null truth branches are
permitted. Ordered reference comparisons, reference arithmetic, mixed
integer/reference operations and forged class identities are rejected. Calls
retain explicit method-exit fault edges; primitive arithmetic keeps its existing
fault rules. Iterative closure validation and existing per-method/program limits
also cover reference values and argument copies.

This API alone validates value flow: the object-operation profile below adds
method bindings, constructor/field operations and their access checks. Native
object lowering is supplied by the separate bounded profile below.
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
| `call` | Exact ordinary static/instance declaration; explicit receiver for instance calls; no automatic null-receiver check |
| nonvirtual `callvirt` | Ordinary nonvirtual instance declaration; explicit null-receiver check after argument evaluation; virtual dispatch is unsupported |
| void calls, `ret` | Separate invoke statement and void terminator; no void temporary or storage |
| `ldnull`, reference `ceq` | Null stack type; identity equality produces Int32 |
| reference `cgt.un` against null | Explicit identity inequality; other ordered reference comparisons are rejected |
| argument/local/constant loads, local stores, `dup`, `pop`, supported Int32 operations | Preserve the existing operand order, type rules and arithmetic fault edges |

Constructors are named `.ctor` definitions with instance/void signatures and
both constructor flags. Their first effective operations load the direct receiver
and call the selected Object constructor before other work. This profile rejects
constructor chaining, repeated base initialization and aliases for readonly
writes. The selected base constructor must be nongeneric ordinary managed IL
with no parameters, locals, handlers or nontrivial body beyond `nop`/`ret`; its
owner must be the exact explicitly mapped object base. No name-only intrinsic
substitute or host runtime probing is used.

Methods allow same-declaring-owner access or public members on externally visible
owners. Fields allow same-owner access (excluding PrivateScope), or public fields
on top-level public classes. Cross-owner nested-type field access, family/internal/
friend rules and inherited MemberRef owners remain conservatively unsupported.
Readonly stores require the declaring constructor and a direct parameter-zero
receiver. Reference field stores accept null or an assignable concrete reference.
Type/module initialization, reference-assembly/native bodies, generics, virtual/
interface calls, prefixes, arrays, strings, byrefs and unsupported storage fail
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
violation and 8 call-depth limit. These tags are not managed exception objects.

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

Null joins a concrete class; different direct-Object classes join as the selected
Object base. Equal types remain exact. Depth mismatches, integer/reference joins
and narrowing an Object input into a class consumer fail without partial IR.
Inference revisits blocks until types converge, before complete operation validation.
Unreachable blocks, ordered reference comparisons, switches, leave/handlers,
argument stores and casts remain errors. Constructors still require a single
block initialized through their direct base call before other work.

A separate 262,144-unit closure budget counts instruction visits, input materialization,
argument copies and edge slot visits plus one per edge. Cached call sites charge
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

## Budgeted object execution frames

`object-frames-native-v2` supersedes the object control-flow and v1 frame emission
profiles ([ADR 0027](decisions/0027-budgeted-execution-frames.md),
[ADR 0028](decisions/0028-packed-boundaries-and-arena-diagnostics.md)). It has a 1 MiB
per-context execution-storage budget independently of retained object storage.
Target-sized typed frames include parameters, locals, values, block inputs,
roots, metadata, scratch and reusable outgoing arguments. Frames have stable
addresses and release in reverse reservation order; root cleanup precedes reuse.
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
No throughput or code-size claim is made for the conservative step dispatch.
The separate primitive emitter is unchanged in storage/depth policy; collection
and broader managed-runtime capabilities remain planned.
