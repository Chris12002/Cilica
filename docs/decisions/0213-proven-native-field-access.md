# ADR 0213: exact-layout native field access

Accepted 2026-10-20. Native v106.

Repeated field accessor calls remain a measured DiffPlex cost after string-header
pooling. Emit small inlined accessors for fields present in at most four ordinary
native layouts. Each branch checks the exact descriptor and accesses the actual
C++ structure member. Inherited fields use the member in the composed derived
structure; hidden fields retain distinct declaring identities. Other layouts,
exception storage and larger descendant sets retain the original accessor.

This avoids the pointer-provenance uncertainty of the earlier diagnostic raw-byte
offset experiment. Standard-layout headers and exact descriptor checks establish
the native pointer conversion. Existing receiver ownership, type and null checks
remain at their original callers. No borrow, GC or public boundary is relaxed.
Optimized builds inline these helpers; O0 retains a noinline original-accessor
wrapper to preserve the diagnostic stack budget. Shared modules promote static
linkage once; do not combine static with an explicit inline token there.

Review covered inherited and hidden members, wide/reference storage, missing guards,
unknown-layout fallback, exception layouts and separate translation units. All 46
focused checks pass, including O0/O2 sanitized differential probes and module tests.
A stale leaf-getter assertion was corrected: AddOne already had a typed kernel and
framed fallback since earlier typed execution; it is not a newly admitted getter.
All 523 DiffPlex observations match release and both forced-GC sanitizer modes on
the constrained stack. See [measurements](../performance/proven-field-performance-20261020.md).
