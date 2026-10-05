# ADR 0096: Narrow primitive scalar storage boundaries

Status: accepted for slice 6c.5. Recorded 2026-10-05.

## Decision

The explicit integer CIL profile accepts SByte, Byte, Int16, UInt16, Char and
Boolean parameters, initialized locals and returns. Evaluation-stack values use
Int32. Parameter loads, local stores and returns insert the explicit unchecked
conversion from ADR 0095, retaining signed extension for SByte/Int16 and zero
extension for Byte/UInt16/Char/Boolean. Boolean storage retains its low byte;
truth tests use nonzero, rather than silently rewriting every byte to 0 or 1.

Reuse existing IR calls, locals and integer casts. Structural member resolution
still distinguishes metadata signatures even when their stack representations
coincide. Inserted conversions count against normal statement/resource budgets.
Legacy Int32 profiles remain unchanged. This does not yet admit enum identities,
narrow fields/arrays/borrows, argument mutation, or a public native ABI.

## Validation and review

Seven authored C# closures exercise overloaded narrow calls, returns, loop-carried
locals, characters, Boolean conditions and transitive checked-conversion faults.
Six authored IL local probes omit explicit conversions to expose storage
truncation. All 4,320 results match .NET at O0/O2 UBSan. Twelve extra call-boundary
probes pass full Int32 bits to narrow parameters/returns without caller casts;
their 144 O2 results also match an explicitly authored .NET IL oracle.

The focused regression run passes 120 tests; the additional call-boundary case
also passes (121 distinct checks). Review checked normalization placement,
signature-preserving binding, width mismatches, noncanonical Boolean bytes,
statement ordering/bounds and overflow propagation. No actionable findings
remain. No hosted work was used.
