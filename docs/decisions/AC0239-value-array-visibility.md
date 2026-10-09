# ADR AC0239: Lexical visibility of direct value-array allocations

Recorded 2026-10-09 for AC8.95 after 7b743bb.

The full-IR inventory retains three direct array failures: LiveSpellModeChange in
ChooseComponentModeOperation.Run, and LiveShieldComponentSelection empty arrays
in SpellResolutionHandler.ContinueShieldContribution and
SpellShieldContributionPayload::.ctor. The internal structs are visible to these
same-assembly callers, but the inline layout catalog discarded their declaring
scopes. The converted LiveSpellModeChange representation and typed array copies
in Alchemy.Engine__6.cpp (3949-3955, 33612-33644; SHA256
`21455d5f4a89a1a37a981cbe305a4d91c5dfab7c553f63b5bcd738eb91368886`)
continue to guide ordinary value-copy/reference tracing expectations. The capture
is a different assembly snapshot and supplies no access permission to Cilica.

## Decision and review

ValueTypeLayout now retains the metadata snapshot's lexical declaring-type chain.
Layout validation checks bounded structure, own visibility flags, external
visibility and consistent ancestry with other values, reference types, enum
arrays, statics and delegates. Composed object validation additionally checks
consistency with method bindings. Both logical and scalar array admission use the
existing ObjectLexicalAccess.Visible rule, with exact assembly identity and its
established nongeneric nested-private/assembly-access bounds. Opaque generic uses
retain AC0238's separate instruction-specific proof. No global visibility grant,
field access relaxation or new native array algorithm is introduced.

Missing chains cannot supply new lexical permission. Existing legacy empty-chain
catalogs preserve their previous public/owning-type contract. Physical scalar
layouts, tracing and allocation are unchanged. CIL119/object IR72/value layout
model5 identify the new facts; native86, core, grants and production budgets stay
unchanged. Review covered source capture, public/attribute agreement, conflicting
parent facts, exact image/owner identity, generic restrictions and preserved facts
through scalar lowering. No remaining findings within this slice.

## Evidence

119 selected tests pass: 78 focused checks in 25.159s and 41 adjacent lexical-call,
interface-fact, enum-array and native-value-array checks in 27.302s. Six new roots
add 1,764 native/.NET comparisons with O0/O2, ASan/UBSan, forced collection, exact
cleanup and frame-budget boundaries. They cover same-assembly internal types,
nested-private structs, public structs inside private containers, readonly value
fields, empty-array identity, allocation faults and copied reference-bearing
payloads. Both IR stages reject missing/default chains, changed visibility,
forged public facts, wrong own tokens and contradictory parent facts. Hidden
generic proof and foreign explicit-name rejections remain tested.
Receipts: artifacts/alchemy-value-array-visibility.

The bounded diagnostic IR inventory clears all three direct value-array failures.
32 first-body validation failures remain: 25 isinst, four castclass, two constructor
prefix checks and one delegate capture. All 15,254 bodies translate; discovery
charges 3,839,271 units and the engine probe takes 67,222ms. StateLookup passes
logical/scalar checks in 700ms/11,759 units. Limits, inputs and core/manifest pins
match AC0238. No engine program, native emission or saved-game execution is claimed.
Receipts: artifacts/alchemy-value-visibility-ir-inventory.
