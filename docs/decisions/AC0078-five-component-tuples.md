# ADR AC0078: Five-component tuple storage and typed equality

Accepted 2026-10-07 for AC5b.8.

The public audit contains a five-component tuple field read. Add the selected
ValueTuple<T1,T2,T3,T4,T5> declaration to the owned core and mapping list, with
five public fields, its exact constructor and the six public interface contracts.
Existing generic-value lowering supplies inline copies, returns, heap/vector
storage, boxes and nullable composition. No compiler special case is needed.

Typed equality follows pinned ValueTuple.cs: compare components in order and
short-circuit. Each component receiver is copied immediately before its default
key-comparison callback, preserving mutable value behavior and changes visible
through shared references. The existing exactly granted key operations perform
selection. Hashing, comparison, text, object equality, structural APIs and tuple
indexing have explicit unsupported managed bodies. No raw external tuple layout
or complete tuple-family compatibility is claimed.

Ten authored probes pass 2,940 repeated O0/O2 native/.NET comparisons. They cover
all fields, copy independence, Int64 bits, reference aliasing, returned values,
heap/vector/box/nullable storage, null/string equality and callback mutation,
short-circuit and exceptions. The promoted public TupleFive adds 294 comparisons.
Reference metadata checks compare all five fields, supported member signatures and
six interface contracts. Existing pair operations and public expectations pass:
26 selected checks total, ac5b8c-tests.log, 64.634 seconds. ASan/UBSan, forced GC,
exact cleanup, 2,048-byte frames and 30-second subprocess bounds remain enabled.

The first callback fixture held a constructor receiver borrow across conditional
argument evaluation, hitting the existing managed-location join restriction.
The supported probe computes arguments first; a separate reduced negative now
pins that exact restriction alongside four exact unsupported-member diagnostics.
No game input/rules or compiler guard was changed to make the probe pass.

Review checked contract identities, copied receivers, callback order, generic
field ownership/rooting and exact unsupported bodies. No profile, grant or limit
changes. The public baseline advances to 34 native controls and 13 diagnostic gaps.
Native remains v55/layout v21. Frozen engine/state inputs are unchanged and still
stop at the snapshot/catalog limits; full saved-game replay remains unverified.
Evidence: artifacts/alchemy-tuple-five/ac5b8b-*, ac5b8c-*, engine-and-state.json.
Core: 3b7a0e4eefb8a83156723876c7cd3f427e5cc644053a499846f5f3ab1178e686.
Manifest: 2ec192a11ad22c18323b4c822c3dab1ee920cbe00d69f0b7843ab126d4eaa18b.
