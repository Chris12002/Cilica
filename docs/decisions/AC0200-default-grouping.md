# ADR AC0200: Default grouping and retained group graphs

Recorded 2026-10-08 for AC6.23, following AC6.22.

The frozen ForcedMovementPlanner.Plan local signature requires IGrouping<TKey,TElement>.
Implement only GroupBy(source,keySelector), the nominal grouping contract and its
read-only IList element view. Adapt pinned .NET 10.0.11 Grouping.cs, Grouping.SpeedOpt.cs,
Lookup.cs and PartialArrayEnumerator.cs. Preserve source/key-selector validation,
first-appearance group order, per-group element order, first equal key identity,
null/default keys, full buffering before the first result, retry after callback faults,
fresh terminal lookups and repeated/interleaved cursor behavior.

The lookup has seven initial buckets, stored-hash collision chains and a circular
first-appearance group chain. Resize rehashes stored hashes without new callbacks.
Existing KeyOperations perform default hashing/equality on copies, including mutable
value keys. Managed arrays/loops replace spans and Array.Resize. Private helper fields
are exposed only through managed properties on internal types, and an owner-local
array factory allocates the private grouping table. These satisfy existing compiler
visibility rules; no access relaxation or compiler change is introduced.

Group element cursors preserve pinned PartialArrayEnumerator behavior: Current before
start faults, Current after exhaustion retains the last element, Dispose is empty and
Reset restores before-start state. Disposing the outer query clears Current/state but
retains its buffered lookup. Retaining one group retains the circular group graph.
Query markers preserve Count/Any/First/ToArray/ToList behavior and explicitly diagnose
optimized Skip. Comparer, element-selector, result-selector and ToLookup overloads
remain unimplemented. Existing default comparer admission limits still apply.

Validation: 34 grouping checks pass in 163.589s on macOS ARM64 at O0/O2 with ASan/UBSan,
forced collection, frame exhaustion and bounded native subprocesses. Sixteen probes
cover 63 argument pairs, three repetitions and both optimization levels: 6048 native/
.NET comparisons. Reference-containing inline elements and group copies are covered.
Two lifetime checks retain exactly 20 objects with the disposed query, 13 with only a
group, and one emergency exception root after release. Adjacent materialization/First
checks pass 26 of 27 in 149.506s; the UInt32 rejection is independently stale on the
pre-grouping snapshot (0.914s). Promoting that exact former gap adds two passing native
checks in 5.338s and 294 comparisons. Total 62 distinct selected checks and 6342 new
comparisons; no production code change was needed for the stale control.

Review covered allocation/rooting order, published bucket/group state, copied-key
callbacks, overflow-safe growth, traversal/disposal and exact retained graph cleanup.
All 89 pinned upstream files and six frozen assembly hashes verify. Snapshot baseline
90291f9 integrates committed 3d0bb53: CIL92/IR59/native79; limits and grants unchanged.
Local main 48ecf14 remains an ancestor. No game changes or frozen assembly rebuilds.

Evidence: artifacts/alchemy-groupby{,-refresh,-engine}. The first engine harness lacked
the new IGrouping mapping; corrected mapped-result.json is authoritative. Metadata-only
reading confirms next token 0x020009bc is System.Collections.Generic.Stack`1, requested
while reading ForcedMovementPlanner.Plan 0x06000cc8 from PreviewForcedMovement IL_01dd.
Engine charges 1268928 metadata units/4776ms; StateLookup translates 15 methods/11616
units/466ms. Diagnostic allowances remain types3072,methods8192,metadata1572864,
instructions196608,parameters16384,locals8192,fields6144. No engine emission/execution.
Core 3bc5432baaff4752b56a06b71c888c529628b71172e60ab3c70b5b078e0ff90c.
Manifest bb428ba93e8b856d8275bbe7f2e057e133e87e3fd3e3ec66ec37f948e7070c99.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.
