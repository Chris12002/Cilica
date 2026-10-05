# ADR 0182: Exact Integer64 embedding

Accepted 2026-10-06; milestone 7c.3. Desktop evidence only.

## Context

The native runtime and integer-object compiler already preserve 64-bit integer
bits, but the v3 boundary only transfers Int32 and reference handles. Phone hosts
need exact wide values, including values beyond the exact integer range of a
JavaScript number. The current IR uses one Int64 storage type for both managed
`long` and `ulong`; signedness is carried by individual operations, not that type.

## Decision

Add `EmbeddingValueKind.Integer64` / `CILICA_V3_INTEGER64` (3). All 64 bits of the
existing UInt64 payload are significant. This is an integer bit-pattern contract,
not a claim that detached signatures distinguish signed from unsigned declarations.
Managed arithmetic retains its signed/unsigned and checked/wrapping semantics.
Host adapters must preserve bits and choose the application's intended interpretation.

An Integer64 contract shape must match the logical IrType.Int64 parameter/result
and cannot carry a reference type ID. Validation still runs before composed value
lowering. Neither Int32 nor a flattened struct is an implicit wide scalar. Tagged
arguments must match the declared kind exactly; reserved bits remain zero.

Manifest signatures use `integer64`, so changing an export's width changes its
fingerprint. Modules with at least one wide parameter/result advertise capability
bit 512. Existing signatures, hashes, record layouts, statuses and symbols remain
unchanged. Wide imports need no handle reservation and continue to work with a
zero handle quota. Existing execution budgets and fault/origin copying apply.

Both native packet paths use their existing `wide_bits` field. Results use C++
bit_cast to UInt64, avoiding truncation, floating-point conversion and signed
conversion ambiguity. The admission guard, collector and exception machinery are
unchanged.

## Evidence and review

A separate C11 consumer checks signed/unsigned identity, wrapping and checked
addition, division and mixed Int32 shift counts against authored .NET execution.
The 13-by-13 boundary matrix across eight exports produces 2,704 comparisons at
O0/O2, including Int64 limits, UInt64.MaxValue, values above 2^53, overflow and
zero divisors. It also checks wide object fields, mixed handle/wide arguments,
void setters, zero-arity wide results, zero handle quotas, execution-budget failure
and malformed tags before application effects. Ordinary object packet checks
exercise high bits and wrapping independently of the composed exception path.
Native builds use ASan/UBSan, no C++ exceptions/RTTI and the 2 KiB frame warning gate.

Review checked logical versus lowered signatures, manifest/capability selection,
all-bit input/output transfer, tag rejection, width-specific storage and fault
cleanup. No wider platform claim follows from these local macOS ARM64 checks.
Structured values, detailed exceptions and physical phone execution remain open.
