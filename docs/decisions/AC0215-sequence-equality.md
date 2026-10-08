# ADR AC0215: Bounded sequence equality with observable traversal order

Recorded 2026-10-08 for AC8.71 after AC8.70.

Frozen RedirectResolutionSpellEdit.CanApply compares two GridPosition routes via
Enumerable.SequenceEqual. Preserve the game IL and implement a reusable owned
library operation, rather than replacing the application's comparison.

Retain the exact pinned System.Linq SequenceEqual.cs original and the existing
MemoryExtensions.cs provenance. Both two-argument and explicit-comparer overloads
validate first, then second. For two ICollection<T> sources, exact array/List<T>
shapes capture backing arrays and logical lengths using existing QueryStorage.
This reproduces span-backed traversal even when a comparer grows or clears a list.
Subclasses and covariant arrays fail the exact runtime-type check and use the
ordinary collection path. Length mismatches stop before equality. Other collection
pairs compare counts, then indexed pairs capture the first count and read left then
right. General sequences acquire left then right cursors, advance left before right,
read Current in order, short-circuit on mismatch and dispose right then left through
nested using regions, including acquisition/callback/disposal faults.

Default equality uses existing granted KeyOperations on a copied left argument;
explicit comparers receive both values in the same order. No additional default
comparer cache, Span declaration, compiler special case or runtime operation is
introduced. Only admitted key types can compile either branch; explicit comparers
do not broaden unsupported T shapes. Nullable remains a tested negative. Exact
runtime shape checks and storage snapshots reuse previously validated library tools.
CIL102/object IR62/interface-plan19/native79 and all production limits are unchanged.

## Validation and review

The isolated 30fe50d snapshot validates all eleven roots at both IR stages (2.398s).
All ten SequenceEqual checks pass in 44.002s. Eleven probes x 120 pairs x 3 repeats x O0/O2
provide 7,920 new native/.NET comparisons. Coverage includes Int32/Int64/string/null
items, early inequality/length mismatch, mutable allocating IEquatable values,
arrays/lists/read-only lists, traced count/index access, custom cursor acquisition/
MoveNext/Current/disposal faults, allocating explicit comparers, first/second null
precedence, and comparer-triggered list growth/reallocation and clearing. Checks
retain forced collection, ASan/UBSan, exact roots/borrows, exact/one-short frame budgets,
2 KiB frame warnings and bounded subprocesses. Existing production bodies are unchanged;
this is targeted evidence, not a full-suite or broader compatibility claim.

Review compared the pinned count/span/index/cursor branches and short-circuit order,
confirmed array roots survive callback replacement of list storage, and verified
that default mutable values are copied before equality. All 99 pinned originals and
six frozen application hashes verify. Main 48ecf14 remains an ancestor. Other lane
character-trimming work is preserved and excluded from this snapshot/evidence.

## Frozen follow-up

Core 35ced342e23578eb97196fc53dfaebd1142b4a6a36b6e89ae3f3fb86ea55666b;
manifest 1e89f1f6f91e38faed36e6f1201d0ada1fde494d6f896951eec39e4323aefd15.
With unchanged diagnostic allowances the scan advances past SequenceEqual to
System.Collections.Generic.Stack<T>.System.Collections.IEnumerable.GetEnumerator,
whose body intentionally rejects traversal. It is requested through OfTypeIterator.Count
from ContributeHandCardsModifier.Read. The engine charges 2,668,468 metadata units in
10,731ms; StateLookup remains 15 methods/11,616 units in 464ms. This is partial compiler
closure discovery, with no native game emission, execution, fingerprint or replay
claim. Evidence: artifacts/alchemy-sequence-equal{,-refresh,-engine}.
