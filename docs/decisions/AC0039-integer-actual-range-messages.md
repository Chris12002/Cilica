# ADR AC0039: Int32 actual-value range messages

Accepted 2026-10-07 for bounded AC8.7.

Collection copying needs the selected three-argument range constructor and its
actual-value message. Admit null or a directly boxed selected Int32, independently
granting the exact constructor. Preserve its box in a fifth traced exception field.
Logical and scalar IR require the actual operand to be a same-block Null or exact
Int32 BoxValue/BoxScalars, respectively. Name/image checks identify selected Int32
and ArgumentOutOfRangeException; argument-resource validation now checks identity
as well as ancestry. The added scalar-box confinement allowance only retains the
validated box in this opaque exception payload. ActualValue getters, other payloads,
aliases and public boxed dispatch remain unsupported.

Message uses the existing base/parameter path and appends pinned invariant actual
value text on every read. Unsigned arithmetic avoids signed overflow for MinValue.
The suffix helper roots the intermediate message, preserves pending fault origin,
and respects allocation/depth limits. Platform newline is selected at native build;
only macOS ARM64 is executed here. Native profile v43 reflects the traced payload
change; both exception IR stages become v5. Layout v17 is unchanged.

Four probes pass 1,176 repeated O0/O2 native/.NET comparisons with ASan/UBSan,
forced GC, strict cleanup and frame quotas. Six detached mutations at both stages,
exact grant removal and four unsupported source cases are checked. Additional
O0/O2 native checks exercise retained box roots, failure at the intermediate and
final message allocations, saved origin and invalid root rejection. Review checked
all allocation boundaries, descriptor reference count/accessors, payload identity,
unsigned formatting, fault order and continued confinement of unsupported uses.

An O0 sanitizer test helper initially exceeded the 2 KiB frame quota; splitting
that helper retained all checks and the unchanged quota. The broader core test had
a stale private-cache diagnostic expectation from AC8.2; it now asserts the earlier
lexical rejection. Its eight native probes run in groups of three within the existing
process limit. Initial and corrected logs remain in artifacts/alchemy-actual-range.
This slice does not yet implement List(IEnumerable) or CopyTo and does not claim
full engine/state or saved-game replay support.
