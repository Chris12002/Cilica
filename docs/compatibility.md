# Compatibility manifest

Recorded 2026-10-01. Capabilities are stage-specific. A translated operation does
not establish native execution or platform compatibility.

| Component/profile | Implemented stage | Boundaries |
| --- | --- | --- |
| Metadata frontend | Inspection, IL structure, explicit binding and bounded audits | Depth-level validation and incomplete dependency reports; no execution guarantee |
| `int32-pending-fault-v1` | Typed IR, C++20 emission and local native differential execution | Explicit static MethodDef and complete direct-call closure; nongeneric owner/method; Int32 parameters/return; initialized Int32 locals; typed block inputs; division/remainder/checked-arithmetic faults; no initializer obligations |
| C++ backend | C-linkage status entry, plus value-only entry for proven pure programs; private helpers, hash/token identity, IL comments and scoped block labels | Primitive pending-fault context; no managed exception objects/handlers; public IR is validated before emission |
| `object-layout-v1` | Bounded metadata snapshots and detached class/field graphs | Int32/CLASS/OBJECT field storage and logical reference slots only; no reference IL, byte layout, allocation, root frames or object execution |
| `typed-reference-ir-v1` | Detached caller-constructed IR validation | Concrete references/null, typed locals/joins/calls/returns; no CIL object translation, constructor/field operations, native emission or execution |
| Managed runtime and library | Planned | Object, array, GC, exception and library execution support is unimplemented |

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
not managed exception objects or messages; catch/filter/finally, identity and GC
rooting remain unimplemented. The CoreLib/Alchemy filter gate is unresolved.

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

Object IL, target byte offsets, managed allocation, instance ownership enforcement,
method root frames, null/object faults and collection remain unimplemented. The
existing Int32 translation profile, emitted C++ and native status ABI retain their
semantics. `IrType` is now a record carrying a kind and optional concrete class
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

This API validates value flow only: method visibility, instance receivers,
constructor initialization, readonly/access rules, allocation, fields, ownership,
root assignment and safepoints require subsequent lowering. Caller-constructed
sources do not authenticate metadata declarations. Void methods are not yet
represented. The primitive C++ emitter rejects reference/null signatures,
storage and values before returning an artifact. Internal reference-returning
IR does not establish a native reference ABI. See
[ADR 0023](decisions/0023-typed-reference-value-flow.md).
