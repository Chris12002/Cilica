# ADR 0099: Wide managed object CIL

Status: accepted for slice 6c.8. Recorded 2026-10-05.

## Decision

The explicit `ValueTranslator.TranslateIntegerObjects` compiler-library API
selects `integer-managed-objects-cil-v1`. It reuses the existing closed generic,
value, borrow, boxing and managed exception pipeline, setting the wide catalog
capability only for this entry point. Existing entry points retain their bounds.

UInt32 shares Int32 stack storage; Int64 and UInt64 share Int64 stack storage.
Opcode semantics determine signedness. Binary operands and comparisons require
matching widths; shift counts remain Int32. Integer conversion opcodes retain
their explicit checked/source-signedness semantics. Field layouts, indirect
loads/stores and method/local types retain exact widths. Generic specialization
keys preserve primitive identities even when stack storage is shared; integer
arguments satisfy value/default-constructor special constraints. Structural
metadata acceptance does not grant execution support to other profiles.

## Validation and review

Four independent C# probes combine wide class fields and constructors, allocating
borrowed mutation, nested generic value returns, boxing/copy identity, unsigned
generic fields, checked arithmetic, division faults, catches and finally blocks.
All 1,176 repeated native results match .NET at O0/O2 under ASan/UBSan with forced
collection. Exact peak frame budgets succeed; one byte less fails with complete
cleanup. Old execution entry points reject the wide closures.

All 152 distinct focused checks pass, including the related exception, generic,
value, array and delegate regressions. Review checked signature admission, generic identity, exact borrow widths,
integer fault edges, rooting and packed Int32 entry boundaries. Array element
storage is deliberately unchanged. Narrow heap/enum storage, wide arrays, value
instance calls and broader generic dispatch remain subsequent work.
