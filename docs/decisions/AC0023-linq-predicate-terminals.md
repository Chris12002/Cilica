# ADR AC0023: Predicate LINQ terminals

Accepted 2026-10-06 for bounded AC6.2.

## Decision

Implement Any<T>, All<T> and Count<T> with Func<T,Boolean> predicates. Validate
source before predicate even on empty arrays. Any/All stop at the first decisive
result; Count visits all items. These overloads must ignore collection Count.
Arrays use direct live reads; other admitted sources use foreach with disposal.
Count's fallback increment is checked, while the array path is bounded by its
Int32 length. Callback receiver identity and reference element identity survive
allocations. Caller-visible faults retain their original precedence.

Direct managed array loops replace pinned spans. A covariant reference array also
uses that loop; its read order, callback count and no-op disposal are equivalent
to the pinned array enumerator. Owned List span borrowing is not implemented;
reachable boxed List factories still fail compilation. Predicate arguments are
limited to the already admitted Int32/reference delegate signatures. Inline value
callbacks, indexed Where and other deferred operators remain diagnostics.
No compiler grants, profiles or native implementation changed.

## Evidence and review

Twelve authored probes over 72 pairs with three repetitions at O0/O2 provide
5,184 native/.NET comparisons. They cover empty results, exact callback traces,
short circuit, mutation of a future array slot, reference/covariant inputs,
allocating callbacks, Current/predicate/disposal faults and null precedence.
An ICollection<T> fixture throws from Count, proving predicates never consult it.
Forty-two existing/boundary regression checks pass, including the advanced Count
predicate boundary, unsupported inline-value delegates and the full public ledger.
Forced collection, ASan/UBSan, 30-second process and 2,048-byte frame limits remain.

Review checked array bounds and overflow, delegate and item roots across callbacks,
foreach exception order and the distinction between parameterless and predicate
shortcuts. The authored mutation fixture's throw sentinel was corrected after
int.MinValue data accidentally triggered it; only that affected group was rerun.
The ledger remains 20 gaps and 27 controls. No private replay or deferred LINQ
support is claimed.
