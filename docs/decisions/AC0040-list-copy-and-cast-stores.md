# ADR AC0040: Direct list copying and copy-specific cast stores

Accepted 2026-10-07 for bounded AC3.6.

Admit the direct generic List.CopyTo overloads taking a destination and optional
index. Snapshot private items/count first, preserving a direct null-receiver fault
before destination validation. Keep null, negative-index and insufficient-space
checks in pinned order. The negative index retains its boxed actual value through
AC8.7 and uses the pinned invariant message. Copies do not change size or version.

A managed loop with normal stelem has the wrong fault: Array.Copy reference
downcasts name the actual object and destination element types. Differential tests
caught this before commit. Add a private exactly granted ArrayInterface<T>.CopyStore
intrinsic, validating its original generic signature and selected-core caller. Its
StoreElement.CopyCast operation uses existing bounds/store checks, then raises the
existing cast fault against the actual vector element descriptor. Earlier stores
are retained, with no additional copy buffering or heap ownership model.

Both detached stages check typed packets and fault edges. Copy stores require the
integer managed-exception profile; ordinary stores keep their old fault semantics.
Only reference-copy programs add bounded dynamic target cast resources. Native
profile v44, object operations IR v17 and integer-object CIL v5 record the change;
exception IR v5 and layout v17 remain. Review checked rooting during cast-fault
allocation, descriptor lookup, null/bounds/type order, partial stores, exact grants,
private access and preservation through scalar lowering.

Six native/.NET probes plus one raw-CIL null-receiver regression pass 2,058 repeated
O0/O2 comparisons, ASan/UBSan, forced collection, strict cleanup and frame quotas.
A removed grant, private external caller and four packet mutations at both stages
are rejected. 47 selected copy/array-mutation/public-baseline checks pass; 12 copy
checks are rerun after restricting cast resources to reference stores. Initial
build/diagnostic/covariance failures and corrected runs remain in
artifacts/alchemy-list-copy. Authored probes distinguish direct copying from the
still-unsupported interface dispatch route; no game inputs changed.

Interface CopyTo needs an array helper, including source/destination compatibility
rules beyond per-element stores. Section/nongeneric copies and List(IEnumerable)
remain unsupported. This prerequisite does not advance the frozen engine's missing
List(IEnumerable) constructor, so no redundant closure re-probe is claimed. Full
engine, state and saved-game replay execution remain unverified.
