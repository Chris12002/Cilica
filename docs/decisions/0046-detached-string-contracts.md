# ADR 0046: Detached immutable UTF-16 string contracts

Status: Accepted for detached validation and phase A roots. ADR 0047 subsequently
adds metadata/CIL translation; ADR 0048 adds bounded native execution.
Recorded 2026-10-03.

## Context and type decision

Strings cannot use ordinary mutable class fields or the array operation contract.
They need immutable UTF-16 contents, value operations separate from reference
identity, and literal identity rules that remain inside a runtime instance.
Establish those contracts before recognizing framework methods as intrinsics.

`object-layout-v5` adds an optional selected String TypeDef identity to the catalog
and standalone model. It remains a nominal, content-scoped reference identity, but
has no class field layout or interface declaration. It is implicitly sealed and
widens only to the selected Object in this initial profile. The identity must be
canonical, distinct from Object and disjoint from ordinary classes/interfaces.
It counts toward the same 256-type limit and must be reachable through roots,
value flow or field/array-element closure. Strings can be ordinary reference
fields and nominal reference-vector elements, including String[] -> Object[]
covariance. Framework interfaces implemented by System.String are not modeled yet.

Detached input asserts the selected identity; validation does not authenticate a
runtime assembly. Future metadata translation must resolve String from the
explicitly selected library and cannot infer it from a display name or host type.
`typed-reference-ir-v5` preserves that identity in references, null assignments,
parameters, locals, joins and returns. It never treats strings as integers or
mutable vectors. Intrinsic string identities cannot own translated ordinary
methods, constructors or field layouts. Detached cast/test targets may name String;
metadata recognition and native implementation remain separate gates.

## Operation and resource contracts

`object-operations-ir-v8` adds five operations with explicit method-exit fault
edges. Operands must be earlier values of the selected String type or null:

- `StringLiteral` carries an immutable array of numeric ushort code units. This
  preserves embedded zero, valid surrogate pairs and unpaired surrogates, including
  through JSON, without relying on text encoders that replace invalid scalars.
- `StringLength` returns the Int32 UTF-16 code-unit count; null faults.
- `StringCodeUnit` accepts an Int32 index and returns a zero-extended code unit in
  Int32 storage. Null precedes bounds faults; negative indices remain valid IR
  with runtime faults. This is not Unicode scalar iteration or general Char storage.
- `StringEquals` compares ordinal UTF-16 contents and returns Int32 0 or 1. Two
  nulls are equal; one null is unequal. It is separate from reference comparison.
- `StringConcat` combines two operands, interpreting null as empty. Reuse the
  nonempty operand when the other is empty; both empty produce the canonical empty
  string. Two nonempty operands produce a new immutable result, without interning.
  Subsequent lowering must check combined length/storage before allocating/copying.

Equal literal contents share one instance-local literal identity, including a
canonical empty literal. Literal lookup and concat may allocate, so both use
allocation safepoints even when a cached/reused result could avoid allocation.
All string references use phase A root slots. Literal initialization of a class
field before its base constructor is valid; unfinished receivers cannot be passed
into string operations. No string-content mutation operation exists.

Bound each literal to 65,536 code units, a program to 262,144 literal code units
counting every occurrence, and distinct ordinal literal contents to 256. Check
unit budgets before copying/hashing; repeated occurrences still consume work/data
budget. These are compiler literal-resource limits, not a runtime string-length
limit. Runtime values retain the bounded Int32 length contract and the later
runtime heap budget. All methods share the literal budgets. Empty payloads are
valid; default/uninitialized immutable arrays are rejected.

## Implementation boundary and validation

At this checkpoint the metadata builder and CIL translator still rejected String
storage and instructions; ADR 0047 subsequently adds bounded translation. At that checkpoint the native emitter explicitly rejected any selected-String catalog,
even when it has only String locals. This prevents a special string reference
from falling through to the ordinary class layout or array native backend.
The native profile remains v8, including working arrays; no string runtime claim
follows from detached validation.

Tests cover numeric UTF-16 serialization, all modeled String/Object/vector assignment
pairs against .NET, field/element closure, selected identity uniqueness/sealing,
exact/excess shared type and literal limits, cross-method literal budgets, null
operands, invalid operands/results/fault edges, constructor initialization roots,
unused string catalogs, interface-plan identity agreement and 4,096-level forged identities. These establish model and
validation behavior; content-operation execution and literal interning still need
.NET/native conformance after lowering.

Review completed with a warning-free solution build, 744 selected local regression
checks and a final 283-check focused run after adding the interface-plan regression,
all passing. These runs overlap and are not additive coverage counts. Review checked
catalog closure, bounded literal processing, constructor roots and the native gate.
Local main remains an ancestor; no hosted work or additional agents ran.

Next: 3f-b reads literals and String signatures and recognizes exact selected-runtime
members for the bounded operations; 3f-c implements immutable native storage,
instance-local literal identity, checked operations and differential execution.
