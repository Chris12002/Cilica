# ADR 0201: Direct character-string allocation

Status: accepted. Recorded 2026-10-20.

## Decision

The selected core's Char.ToString(char) calls an exact, private
StringConstruction.FromCharacter(char) intrinsic. Its manifest grant is tied to
the built declaration, signature and image. Discovery permits only the selected
core caller and exact static InternalCall declaration. Application callers cannot
invoke the private allocator directly. The existing array construction grant and
behavior remain unchanged.

StringFromCharacter is a throwing scalar-to-reference operation validated in
logical and scalar IR: an earlier Int32 operand, exact String result and fault
edge are mandatory. Native lowering copies the low UTF-16 unit to native scalar
storage and calls the existing string allocator. That scalar survives collection
without a managed root. The allocated result uses the normal precise result slot
and rooting machinery; allocation failure uses the normal fault/materialization
path with the owned Char.ToString intrinsic call's method and IL origin.

Every successful call creates a fresh one-character string, including NUL and
unpaired surrogate units. No character interning or cache is introduced. The two
temporary managed arrays disappear. Thus smaller managed memory/arena budgets can
succeed; preserving artificial temporary-array exhaustion is not a semantic goal.
Genuine allocation failure remains catchable OutOfMemoryException. No general
mutable string buffer, unmanaged pointer API or uninitialized string is exposed.

Native emission advances to v95, object IR to v79 and integer-object CIL to v126.
One private capability is appended without renumbering existing grants. Managed
object layouts, borrow carriers and public ABI remain unchanged.

## Review and validation

Review checked private grant admission, both detached validators, low-bit conversion,
allocation/fault ordering, safepoint classification, result rooting and identity.
A catalog dependency mistakenly inherited from array construction was corrected;
the direct operation requires only the String descriptor. Twenty-five focused
checks pass, including eight authored checks, array constructors and StringBuilder.

The authored native runs execute all 65,536 UTF-16 values at O0 and O2 with forced
collection and ASan/UBSan. Each conversion allocates exactly one managed object.
702 repeated native/.NET comparisons cover narrowing, static and instance calls,
fresh identity and roots across another allocation. Additional checks cover caught
and uncaught object-limit failure with exact current method/IL origin, omitted
grant, hostile caller and malformed operand/result/fault edges at both IR stages.
Twenty adjacent final checks cover direct flow, borrow diagnostics, exceptions,
budgets, native stack and array concatenation. No outstanding review findings.

A source-state audit found the emitter reverted to v92 after the measured borrow
candidate was emitted. Commit cbebe58 restores exactly the emitter hash recorded
in that candidate's provenance; all baseline compiler/core source hashes match.
The cause of that local overwrite was not established. Direct-flow tests now also
assert emitted branch presence, and the benchmark runner checks the emitted
profile and detects source changes during preparation. Final validation and fresh
emission use the restored implementation; the old reverted state is not accepted.

See [measurement and Roslyn/REA trace](../performance/execution-slices-20261020.md).
