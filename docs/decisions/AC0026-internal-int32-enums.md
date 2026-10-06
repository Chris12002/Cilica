# ADR AC0026: Internal Int32 enum storage

Accepted 2026-10-06 for bounded AC5b.1.

## Decision

Separate scalar representation from declaration access. Exact top-level internal
Int32 enums may occupy locals, signatures, same-owner static slots and instance
fields in admitted object/value layouts. Existing value__ flags, underlying width,
literal declarations, generic arity, layout and initializer checks are preserved.
Direct initobj/ldobj/stobj/cpobj enum operands require the same assembly content
identity or external visibility. Generic argument and box access retain their
existing stricter nominal rules; nested hidden enums are not newly admitted.
The private payload of a public type can be represented without granting callers
access to its fields. No IR/runtime profiles or library grants change.

## Evidence and review

Five authored probes pass 1,470 repeated native/.NET comparisons at O0/O2 with
ASan/UBSan, forced GC and strict heap cleanup. They cover locals/ref parameters,
returns, object fields, struct copies, arrays of structs and same-owner statics.
Raw metadata checks admit same-assembly initobj and reject the foreign assembly.
Generic and boxed internal operands remain rejected. Existing enum storage and
public enum boxing pass 882 comparisons; the stale boxing-negative expectation
was corrected. Malformed enum, lexical access and public baseline checks pass.

The older NullableProbe internal-field fixture advances past enum shape validation
and still fails its independent cross-owner internal-field gate. It remains a
negative test with the new precise diagnostic; access is not broadened to make it
pass. Review checked caller identity, nominal enum identity, unchanged scalar width,
layout inclusion and the separation of storage classification from permission.

The unchanged private state-lookup probe advances to DamageModifierSpecification's
StatusKind constant declaration in object layout. Evidence and compiler hashes are
in artifacts/alchemy-internal-enum. This is diagnostic progress, not full state
lookup execution or saved-game replay. The public ledger remains 18 gaps/29 controls.
