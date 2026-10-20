# ADR 0220: Optimized checked frame-borrow helpers

Accepted 2026-10-20. Native v113.

Expose eight existing checked helpers to optimized inlining: find_borrow,
frame_location_storage, location_storage, frame_location, valid_location_argument,
load_location_integer, store_location_integer and load_location_reference.
All retain noinline at O0. Heap/array/static storage subhelpers, wide operations,
reference writes and registration policy remain unchanged. Every helper body is
unchanged; this does not expand typed kernel eligibility or authorize escapes.

Full token matching, bounded cache-collision fallback, retired registration checks,
readonly and narrow-width rules, type/extent checks, caller roots, staged outputs
and diagnostic borrow-chain validation remain active. Macro attributes contain no
linkage keyword, so independently compiled modules retain one runtime identity.
Roslyn and Unity comparison evidence motivating the change is retained in the
v111 investigation; these are compiler-wide helper policies, not method rewrites.

All 56 selected carrier, scalar-location, typed-borrow, typed-lookup, local-output,
shared-module and narrow-borrow checks pass. Review confirmed the eight bodies
are byte-for-byte unchanged after removing attribute prefixes. Character time
improves 4.29%; line time improves 0.84% with overlapping sample ranges.


Correctness, shared-module, constrained-stack and release timing evidence is in
[the acceptance report](../performance/v113-borrows-20261020.md).
