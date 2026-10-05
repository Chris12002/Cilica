# ADR 0164: Narrow scalar field storage

Status: Accepted in the integer-object profile

## Decision

Admit Boolean, Char, SByte, Byte, Int16 and UInt16 fields in classes, inline values
and static storage. Keep the runtime's private Int32 slots and record each resolved
field operand's declared width in compiler input. Emit explicit existing integer
casts on reads and writes, preserving sign/zero extension and truncation even when
IL omits conversion instructions. Boolean storage is an unsigned byte, preserving
noncanonical byte values just as existing narrow parameter/local boundaries do.

Whole-value copies and zero initialization retain the normalized slots. Field access,
readonly rules, receiver checks and initializer order remain in the existing paths.
Narrow field addresses are rejected before lowering; no Int32 borrow can escape as
a byte/short address. Narrow arrays, primitive boxing and width-aware borrow carriers
remain separate work. This is compiler lowering to existing IR, without new native
storage or public layout claims.

## Evidence and review

Twelve authored IL probes store full Int32 operands directly to heap/static fields
without conversion opcodes. Dynamic test-oracle methods execute matching authored
operations under .NET; no assembly runs in the compiler. Nineteen boundary values
cover signed/unsigned truncation and noncanonical Booleans. A C# probe covers nested
value writes, copies and zeroing. Together these supply 1,662 repeated O0/O2 native
comparisons under ASan/UBSan, forced collection, frame and cleanup checks. A direct
ldflda regression cannot escape the narrow field as a full-width location.
All 63 focused/composed checks and the full solution build pass. Review checked
closed field signatures, explicit casts, fault ordering, readonly behavior, copies
and address rejection. Runtime quotas and all bounds are unchanged.

The unchanged kernel now reaches metadata work exhaustion while reading a canonical
serialization callback. Native transcript equivalence and filter closure remain
pending.
