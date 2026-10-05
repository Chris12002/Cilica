# ADR 0134: Invariant integer formatting APIs and cache identity

Status: Accepted

## Decision

Map Int32, UInt32, Int64 and UInt64 ToString(IFormatProvider) to ordinary managed
bodies in the owned core. Preserve the reference methods' implicit IConvertible
implementation flags through a bounded interface declaration. These methods call
the pinned-source decimal helper, compiled into the same zero-reference image.
No native decimal-formatting algorithm is introduced.

The currently admitted contract requires a directly proven InvariantCulture
result at each CIL/IR call. Each exact method requires the new reviewed
InvariantIntegerFormatting capability. Null, alternate providers and unproven
parameter/local aliases remain build diagnostics; general provider dispatch,
format strings and parameterless formatting are not implemented. The managed body
also checks provider identity. Validation carries the precondition until scalar
lowering has validated the original calls.

Match the pinned CoreCLR source's shared [0, 300) decimal string cache across all
four widths. Private selected-core Find/Publish declarations require separate
exact grants and reject callers from other images. Managed code computes decimal
text; native publication copies it into a bounded per-instance traced cache.
The copy keeps numeric-cache identity distinct from string literals. Larger and
negative values retain fresh string results. Entries obey existing object/byte
limits and are freed with the instance; no global cache or host assembly execution
is introduced. Lookup and tracing are bounded by 300 entries.

Object operations IR advances to v10 and the private native runtime to v21.
Delegate/MulticastDelegate compiler declarations support Roslyn's operator binding;
their presence adds no new executable delegate API. IConvertible remains a partial
contract, without primitive boxing or interface dispatch.

## Evidence and review

All 81 distinct focused checks pass, including existing tracing/collection and
primitive/provider regressions. Eight mapped-core probes match .NET in 2,352
repeated native O0/O2 ASan/UBSan results. A further 8,976 comparisons check every
code unit and length across all four widths, and 60 comparisons cover cache
boundaries and cross-width/literal identity. Heap-field receivers remain rooted
across provider and formatting allocations. Native cache regressions cover
allocation limits/recovery, invalid/foreign pointers and collection retention.
Missing individual capabilities, external private-cache calls, unsupported
providers and corrupted detached IR are rejected. The full local solution builds.

Review preserved implicit interface flags and corrected the frame-exhaustion
harness: warmed cache hits can use less call depth than the first invocation, so
the short-frame check uses the measured warmed peak. Native cache inputs are
checked before reading headers. No remaining actionable review issue was found.

The reviewed local fixture build records 202 inputs and 65 requested grants.
Its image SHA-256 is
`bba99c0828c0c7272661b1173b5519a5c19784fb1e1881a686ec178c530755a0`;
its manifest SHA-256 is
`76bb5983bb1f29072efeddb113fc8d2e9b8ad22f8604a8fe31429e246cba9eb1`.
Production baseline approvals remain empty. The real kernel's mapped compilation
now stops at the missing System.Collections.Generic.IReadOnlyList`1 contract
(token 0x020009b0), before native execution. Collections, StringBuilder, broader
provider proofs and complete kernel equivalence remain milestone work.
