# ADR AC0145: Pair tuple formatting

Recorded 2026-10-07 for AC7b.18.

## Evidence and decision

The committed 6509e50 diagnostic probe clears the retained StringBuilder receiver
and reaches ValueTuple<long,string>.ToString through Dictionary.Add in
EffectSchedule.Group IL_00cb. It charges 369,265 metadata units; StateLookup stays
at eight methods/9,927 units. No engine emission/execution occurred.

Adapt the pinned pair tuple ToString contract: render Item1, then Item2, replacing
null text with empty content and surrounding components with fixed punctuation.
Use by-reference field arguments to the exact private TupleElementText<T>.Render
declaration. Its separate grant selects ordinary managed invariant formatting
for Int32/UInt32/Int64/UInt64, string identity, or an exact constrained value
ToString target. Mutating struct callbacks update the actual tuple fields; a
boxed tuple updates only its box. Nested pair fields use the same recursion.
IFormattable on a component does not override ordinary tuple ToString semantics.

Nullable/enum/narrow components, non-string references and other tuple arities
remain diagnostic. The primitive wrappers deliberately reuse the existing
invariant formatter; no .NET allocation-count/cache/interning equivalence is
claimed. No boxing is introduced for tuple fields, and no runtime operation is
added. Integer CIL v62 records admission; IR/native profiles and all production
and diagnostic resource limits remain unchanged.

## Validation and review

Ten probes cover numeric widths, exact UTF-16/nulls, frozen-shaped dictionary
messages, repeated mutable calls, boxed copies, nested tuples, ordinary overrides
on IFormattable components, left/right callback faults and heap-field mutation.
The first callback can modify the second field through a cyclic owner reference;
forced GC verifies the owner and field locations remain live across allocation.
Eight probes use 49 input pairs, two callback probes use 77, all repeated three
times at O0/O2: 3,276 new native/.NET comparisons. The formerly negative authored
ValueTupleProbe.UnsupportedText supplies another 294 comparisons. The oracle
selects invariant culture. Exact grants, private callers and retained negative
boundaries are checked.

The first draft fixture encountered an existing managed-location join while
constructing a tuple with a conditional argument; evaluating that string before
construction keeps the reduced test inside its intended profile. No application
input or rule changed. Enum metadata and narrow-field negative expectations were
also corrected to exercise their intended admission gates.

Review checked live field rather than copied receiver semantics, callback order,
exception propagation, nested location/box identity, selected declaration shape,
byref integer sign/width and rooting. All 48 tuple, dictionary, boxed-integer and
core-build checks pass under unchanged 2,048-byte frames and subprocess caps.
Evidence: artifacts/alchemy-tuple-text/{tests-scoped,regressions}.log.
Four additional public-baseline checks pass on the final fixture binary: 52
selected checks total, 46 public controls and one retained gap. Local main remains
the ancestor 48ecf14. Full fingerprint and saved-game replay remain unverified.
