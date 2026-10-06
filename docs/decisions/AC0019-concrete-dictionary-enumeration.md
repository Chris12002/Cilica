# ADR AC0019: Concrete dictionary enumeration

Accepted 2026-10-06 for bounded AC4.3.

## Decision

Add the exact Dictionary<TKey,TValue>/Enumerator nested value-type mapping and
concrete GetEnumerator, MoveNext, Current and Dispose bodies. Keep dictionary,
version, index and copied KeyValuePair current state. Scan active entries in slot
order and skip free entries. Materialize the pair before storing it to preserve
existing bounded value-return assignment rules. Ordinary internal dictionary
accessors preserve the generic lexical private-field gate.

Add an Int32 version field. Increment with wrapping arithmetic only after a new
key has been inserted, whether in a reused slot or at the high-water index.
Successful overwrites, duplicate TryAdd, Remove and Clear do not change it. This
matches the pinned .NET 10.0.11 behavior. MoveNext checks version even after it
has ended; successful completion sets index to count+1 and current to default.
Copies advance independently. Dispose does nothing. Reset has a managed body but
boxed interface execution and nongeneric Current/Entry/Key/Value are not admitted.
No general interface factory implementation is claimed.

## Evidence and review

Eleven authored probes plus public DictionaryForeach supply 3,528 repeated
native/.NET comparisons at O0/O2 on macOS ARM64, with ASan/UBSan, forced GC and
frame/cleanup checks. Coverage includes slot order through growth/removal/reuse,
removing current/future entries during iteration, clearing, overwrite and failed
TryAdd, free-slot insertion invalidation, independent copies, empty/default/ended
states, disposal/finally, reference and inline-value snapshots, allocating loop
bodies and retained current after dictionary clear. A subsequent MoveNext clears
that current, as .NET does. All 43 affected dictionary/view/baseline regressions
pass; main remains an ancestor and no milestone checkout files were changed.

Review checked version increments occur only after successful insertion, matching
fault order; no failed allocation publishes a new version. Count+1 is explicitly
unchecked and compared unsigned as in the pinned implementation. The dictionary
reference and pair payload remain traced across allocations. Native v41,
interface plan v5 and intrinsic grants are unchanged. The ledger has 22 gaps and
25 controls. Key/value and boxed-interface enumeration remain open; private
saved-game replay has not been executed.
