# ADR 0147: Managed lists and live read-only views

Status: Accepted for bounded Int32/reference storage

## Decision

Adapt pinned List<T> construction, capacity, indexed get/set, Add, geometric growth
and AsReadOnly as ordinary managed code. ReadOnlyCollection<T> retains an IList<T>
reference, so its Count and indexed reads reflect later mutations and each wrapper
has independent identity. Retain exact nominal interfaces and member flags.
Unimplemented search, enumeration, copy and removal paths fail during compilation.

Use typed copy loops in place of Array.Copy, preserving allocation before storage
publication. Private empty storage uses Array.Empty<T>; its backing identity is
not exposed by the admitted APIs. Move constructor checks into managed helpers to
keep initialization straight-line without changing exception order. Growth reads
Capacity to retain explicit Int32 length conversion in supported IL. No application
rules or special kernel behavior enter the library or compiler.

Interface dispatch also needs array targets. Add exact granted managed Set and
fixed-size Add helpers. Mutation helpers use the interface's element view, while
array stores validate the actual receiver descriptor: a string[] through
IList<object> accepts strings/null and rejects unrelated objects after bounds
checks. Detached dispatch validation checks the helper's vector, element parameter,
return type, selected image and recipe. Interface plan v3/native v28 record this.

## Evidence and review

Six independent probes yield 1,764 repeated native/.NET comparisons at O0/O2 with
allocation-time collection. Coverage includes growth/capacity, interface Add,
reference identity/nulls, live wrappers, flags/SyncRoot, readonly mutation faults,
constructor/null/index messages, covariant stores, wrong-type stores, bounds-first
ordering and fixed-size Add. Exact metadata interface sets and admitted member
mapping are checked against the reference image; removing either helper grant
rejects compilation. Unimplemented Contains remains a build error.

Review checked overflow/capacity limits, version/mutation order, rooting across
replacement allocations, wrapper ownership, exact covariance and nominal contracts.
The full solution and 61 composed local collection/interface checks pass. The
unmodified kernel advances to a compiler-generated initialized static delegate
cache in canonical writing. Native kernel equivalence remains unproven.
