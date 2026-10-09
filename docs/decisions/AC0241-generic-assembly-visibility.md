# ADR AC0241: Assembly visibility from closed generic callers

Recorded 2026-10-09 for AC8.97 after 9b7fd78.

Six Alchemy type-test failures name internal ShieldActivationContext or
PreparedComponentSpellEdit from three EffectOperationInstruction specializations.
Source access checking already admits them. The detached validator instead
applied private generic nesting restrictions before checking assembly visibility.

After validating the declaration chain, ObjectLexicalAccess.Visible now accepts an
assembly-visible target only when the caller and target have the exact same image
hash. Assembly visibility does not require reconstructing closed generic ancestry.
Private or protected-only nesting still takes its previous restricted path;
private member containment, foreign image checks and generic owner restrictions
remain in force. No operation proof, generic identity, runtime cast, layout or
native ownership rule changes. CIL121/object IR74 record the admission change;
native86, value-layout model5, owned core, grants and production budgets stay fixed.

Review checked chain validation precedes this rule, the same-image test precedes
assembly access, private chains cannot enter the new path, and both IR compositions
use the shared rule. The converted reference remains a semantic comparison, not an
access authority: frozen metadata establishes these internal type declarations.

35 selected checks pass in 27.465s: five new roots add 1,470 O0/O2 native/.NET
comparisons with ASan/UBSan, forced collection, exact cleanup and frame-budget
boundaries. They cover isinst/castclass, public nested types in internal containers,
constructed internal types and reference-bearing value arrays from closed callers.
Raw metadata controls check same-image internal success, foreign internal rejection
and foreign public success. Existing lexical containment, generic-private negatives,
foreign image and interface declaration-fact controls pass.
Receipts: artifacts/alchemy-generic-assembly-access.

The bounded diagnostic full-IR inventory clears all six targeted failures, leaving
only two constructor initialization paths and one delegate capture among 15,254
translated bodies. It measures 3,517 distinct literals / 352,478 charged UTF-16 units
as validation reaches later operations. Diagnostic limits, frozen inputs and
core/manifest pins are unchanged from AC0240. The engine still produces no validated
program, native binary or replay. Receipts: artifacts/alchemy-generic-assembly-ir-inventory.
