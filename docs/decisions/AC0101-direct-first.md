# ADR AC0101: First-element terminals and captured list storage

Recorded 2026-10-07 for AC6.15.

## Evidence and decision

The committed 953a24f diagnostic compiler reaches missing non-predicate
FirstOrDefault<UnitState> in InitiativeSlots.Enter IL_0053, through AdvanceSlots
IL_0415, after 127,423 metadata-work units. StateLookup still translates eight
methods with 9,917 units. Evidence: artifacts/alchemy-dictionary-add-read-{refresh,engine}.
All six frozen assembly hashes match. These probes do not execute the engine.

Add non-predicate First and FirstOrDefault using the existing QueryFirst helper:
validate source, dispatch owned query terminals, otherwise read IList Count once
and index zero, or acquire an enumerator, move once, read Current and dispose.
First distinguishes an empty sequence from a present default/null element and uses
"Sequence contains no elements". The predicate overload retains its distinct
no-match message and source-before-predicate validation.

Review found a pre-existing observable mutation defect in predicate First and
filtered-list first-element helpers. Pinned .NET captures list backing storage and
starting count, whereas our fallback enumerator rejected mutations. The reduced
ListTerminals regression reproduces this at O0/O2 on mode 1/kind -4. Reuse the
existing checked QueryStorage capture for those paths; return the element loaded
before the callback. Arrays remain traced while callbacks replace list storage.

A separate private traversal selector, terminal 5, propagates first-element
semantics through Select chains to filtered lists. It does not change ordinary
iteration, cardinality or materialization modes. Query cursors remain independent.
No intrinsic grants, compiler/native profiles, runtime layout or resource limits
change. Default-value overloads outside the pinned netstandard2.1 contract are not
added. Existing limits on generic payloads and query forms still apply.

## Validation and review

Sixteen probes supply 9,792 native/.NET comparisons: fourteen probes at 105 pairs
and two mutation probes at 81 pairs, each repeated three times at O0/O2. Coverage
includes empty/default/null, source faults, IList Count/index callbacks, iterator
acquisition/move/current/disposal faults and precedence, collection counts that
must not be read, value/reference payloads, skipped Distinct hashing, ordered
selectors, concatenation, OfType and independent existing cursors. Mutation cases
cover growth, clearing, removal, replacement backing arrays, element replacement,
callback faults and references retained across collection.

The final 45-check run passes: direct and predicate first, distinct regressions,
and the 42-control/five-gap public baseline. Forced GC, ASan/UBSan, 2,048-byte frame
warnings and unchanged 30-second native subprocess caps apply. Earlier runs caught
an unsupported native-integer fixture expression and an omitted default-comparer
root expectation; both fixture issues were corrected explicitly. The root check
requires precisely one extra permanent object for Ordered. The reduced mutation
regression failed at both optimization levels before the helper fix.

Review checked null/empty distinctions, loaded-element copies, roots across list
replacement and callback allocation, disposal precedence, cursor independence and
terminal-mode separation. No compiler/native/runtime admission changes. Local main
48ecf14 remains an ancestor. Full engine/replay remains unfinished.
Evidence: artifacts/alchemy-direct-first/ac6-15{a,b,c,d,e}-tests.log and hashes.json.
Core: 4b20433cf3d5ebb68a8a58a7379dcd9763eb5752da377105c2f7669df1be51ae.
Manifest: b98233e1de6112392e09f419a0e7486624202fb8b4e32653057c661fd86f6fe8.
Fixture: 595ce9495e566418b28332f65543e0eb6770e5af54149a1f4dd5dd016c53cc09.
