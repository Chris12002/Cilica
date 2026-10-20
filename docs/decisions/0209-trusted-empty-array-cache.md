# ADR 0209: Trusted internal empty-array cache access

Status: accepted. Native v102. Recorded 2026-10-20.

Profiling v101 finds empty-array lookup consuming 7.75% of application samples,
all beneath List construction. Each cached entry scans the entire allocation list
to establish membership. Enlarging the object quota makes this cost worse even
though collection becomes less frequent.

Use `managed_reference` for ordinary empty-array cache traversal. Entries are
published only after successful array allocation, remain rooted by the cache and
cannot be collected between lookup and return. This is the same trusted internal
reference contract already used by generated execution. Default execution checks
the owner and known descriptor without a heap-list walk. Setting
`CILICA_VERIFY_INTERNAL_REFERENCES=1` restores membership-before-header validation.

Keep the traversal count bound, array-kind check, zero-length check, marking guard
and exact descriptor match. Exact matches preserve separate closed generic empty
identities and per-instance ownership. Allocation failure publishes nothing.
`visit_empty_arrays`, collection and public handles keep checked membership in
both modes. Arbitrary private cache-pointer corruption requires diagnostic
execution; it is always rejected by collection. No layout, quota, ABI or managed
semantic change.

Review covers cache publication and roots, allocation failure, header access
ordering, descriptor identity, cross-instance isolation and collection independence.
The focused suite contains 20 passing checks. Cache tests run O0/O2 with both
diagnostic settings under ASan/UBSan, including forced collection, detached
readable storage, cloned descriptors, foreign entries, nonempty entries, cycles,
arbitrary pointers at checked boundaries and marking-time rejection.

Fresh compiler emission reproduces the accepted v101 baseline exactly before the
change. Paired release timing is 62.065 versus 57.263 us/diff, 7.74% less time.
Full-workload validation and retained provenance are recorded in
[the performance report](../performance/empty-cache-performance-20261020.md).
