# ADR 0102: Inline value constructors

Status: accepted for slice 6c.11. Recorded 2026-10-05.

## Decision

The explicit integer-object CIL profile admits exact struct constructors with
managed IL and initialized locals. A distinct value-constructor binding retains
the original declaration flags and requires a writable managed location of the
exact declaring struct at parameter zero. Constructors may accept admitted
scalar, reference, value and synchronous borrow arguments.

`newobj` evaluates arguments first, initializes a bounded temporary inline local,
invokes the constructor on its location and copies the resulting value. A direct
constructor `call` uses its supplied location without an implicit reset. Existing
copy, return-packet, rooting and exception machinery handles both forms. Hidden
locals and their scalar expansion remain subject to existing resource bounds.

A readonly field may expose a writable location only when it belongs to the
declaring value constructor and the receiver expression is its direct parameter
zero. Other receivers and nested readonly fields retain their restrictions.
After source IR validation, constructors become private static calls over
expanded checked carriers; they do not add heap allocation or a public struct ABI.
Class constructor rules are unchanged. General struct instance methods remain
unsupported.

## Validation and review

Five independent C# probes cover wide readonly fields, reference-containing
nested values, generic struct constructors, boxes and return packets, direct
construction, borrowed arguments, empty values and exceptions after allocation.
All 1,470 repeated O0/O2 ASan/UBSan results match .NET under forced collection,
exact frame budgets and failure cleanup checks. The empty-value probe confirms
that constructor side effects are retained even when there are no payload fields.

Review checked argument order, temporary initialization, partial-failure behavior,
readonly receiver provenance and declaration normalization. Invalid IR targeting
another struct's readonly field or naming a missing field is rejected. No native
runtime representation or hosted workflow changed. All 141 distinct focused
checks pass.
