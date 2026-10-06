# ADR AC0030: Full-width integer static storage

Accepted 2026-10-06 for bounded AC5b.4, the first static-value foundation.

## Decision

Ordinary Int64/UInt64 static fields use a zero-initialized unsigned 64-bit carrier
in the per-instance static table. It is separate from 32-bit and reference carriers,
so there is no union aliasing or accidental integer tracing. Reads and writes retain
the existing declaring-owner, readonly and type-initializer checks. Allocation
continues to charge exact sizeof(StaticSlot), collect with rooted inputs, and retain
one table per runtime instance. The wider private slot changes the native profile
to object-frames-native-v42 and the layout contract to object-layout-v16.
Static addresses and inline static structs remain unsupported at this step.

## Evidence and review

Five probes pass 1,470 repeated native/.NET comparisons at O0/O2 with forced GC and
ASan/UBSan: signed/unsigned high bits, closed generic separation, overflow-before-
assignment and readonly initialization. Two native runs prove persistence across
contexts, instance isolation and zero reference roots for numeric slots. Eighteen
existing static/default/quota checks pass. Twenty-four initialization checks, the
new detached-width rejection and all 21 public baseline checks pass.

Review checked unsigned native storage, unchanged managed arithmetic, read/write
width agreement, zero initialization, exact allocation accounting and collector
separation. The logical catalog rejects a forged Int32 declaration for an Int64
store. Evidence: artifacts/alchemy-wide-statics. This prepares static struct leaf
expansion; it does not yet unblock RejectionId or full saved-game replay. Public
counts remain 18 gaps/29 controls, now revalidated on native v42.
