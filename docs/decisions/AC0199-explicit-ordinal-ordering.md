# ADR AC0199: Explicit ordinal string ordering

Recorded 2026-10-08 for AC6.22, following AC8.63.

Frozen ReconcileControlOpportunity IL_00a2 supplies StringComparer.Ordinal to ThenBy.
Conservative discovery followed the unused Comparer<string>.Default branch in the
shared iterator constructor. Do not substitute ordinal order for culture defaults.
Authenticate the exact selected Enumerable string-key overload and matching managed
bridge, require its OrdinalOrdering grant, and prove the comparer operand is the
selected Ordinal getter in the same block. Preserve that operand and its evaluation.
Direct application bridge calls and aliases/null/custom comparer operands remain
rejected. OrderBy/OrderByDescending take any otherwise admitted enumerable source.

ThenBy/ThenByDescending reuse the original source and parent sort chain through
owned IOrdering.GetSource. After closed discovery, require every compatible concrete
ordered source to be the exact selected OrderedIterator with the identical element
argument. Reject custom, covariant and boxed-value ordered sources conservatively.
This avoids bypassing custom CreateOrderedEnumerable callbacks. No general receiver
or culture assumption is introduced. Existing ordinary integer ordering and custom
integer ordered sources retain their original generic interface calls.

A private constructor stores an already resolved comparer; owned bridges and cursor
clones use it. Original constructor/default behavior remains available for existing
profiles. Sorting, key buffering, comparer argument order, stable ties, source
iteration/disposal, first/cache/Count/Any/ToArray/ToList behavior are unchanged.
CIL91 follows the separately committed array-length truth-branch CIL90 (e5faa49).
IR58/native78 and all limits remain unchanged; four exact managed bridge grants
share one new capability. No game code changes.

Validation: 89 of 91 integrated ordering/comparer/array-branch checks passed in
310.883s. Two singleton fixture checks then passed O0/O2 in 10.648s after repairing
an accidental -1 fault-sentinel collision and explicitly warming/counting the empty
array enumerator cache. Runtime/compiler code did not change during these repairs.
Thirteen new probes provide 3822 repeated comparisons; two promoted legacy
cardinality controls add 588, total 4410. Coverage includes UTF-16/null/equal keys,
ascending/descending mixed chains, deferred/repeated/interleaved enumeration,
selector/source/disposal failures, validation order, terminal shortcuts and empty/
singleton sources. Native checks retain ASan/UBSan, forced GC, exact retained roots,
frame exhaustion and subprocess bounds. Custom/covariant source, comparer provenance,
all four grant denial and direct bridge denial checks pass. The existing wide-key
Count rejection was independently stale on the pre-change snapshot (2 controls
passed, that one rejection failed); only its measured cardinality probe is promoted.
The old explicit ordinal Count rejection is promoted by this slice. Default string
comparers and unsigned delegate controls remain rejected.

Review checked exact metadata/argument proof, receiver-universe completeness and
variance restrictions, source/key validation order, retained comparer identity,
clone behavior and managed rooting across allocations. All 85 upstream hashes and
six frozen inputs verify. Local main 48ecf14 remains an ancestor. Evidence lives in
artifacts/alchemy-ordinal-order{,-refresh,-engine}; the snapshot is e5faa49 plus owned
changes and excludes subsequent compatibility candidate edits.
Core 4b540b4e162b176954fb9bba66a3ff69c13a97f1290a42789527384548e9ad82.
Manifest ed8b0904349192ef0ee9ddcb1aea0c07a80c24a6baea76c2b5bc559d025e10f1.
Frozen discovery now reports netstandard2.1 token 0x0200066f System.Linq.IGrouping`2,
confirmed by a metadata-only reader, while reading ForcedMovementPlanner.Plan
0x06000cc8 from ViewerSpatialContext.PreviewForcedMovement IL_01dd. Engine charges
1268750 metadata units/4864ms; StateLookup translates 15 methods/11616 units/463ms.
No engine emission/execution. Full engine, unchanged fingerprint and saved-game
replay remain unfinished; GroupBy/grouping support is the next measured gap.
