# ADR 0219: Optimized allocation helper inlining

Accepted 2026-10-20. Native v112.

Expose allocation_safepoint, allocation_room, allocate_string and
string_from_character to optimized native inlining. O0 retains the safepoint's
compiler-default policy and the other three helpers' noinline policy; the room
forward declaration carries the same attribute as its definition. Bodies, quota
checks, allocation observers, pool fallback, collection and rooting are unchanged.
The attributes introduce no linkage keyword, preserving shared header identity.
The choice follows Roslyn IL and Unity/native evidence retained in the v111
investigation; no application method names or DiffPlex-specific policy are added.

All 32 selected allocation-verification, string-pool, string, inline-string,
character-helper and shared-module tests pass. Review confirmed unchanged helper
bodies, O0 policies, checked safepoints and separate-TU linkage. Character timing
improves 4.03%; the line result differs by only -0.11%, with overlapping process
medians, and supplies no evidence of a line improvement.


Correctness, shared-module, constrained-stack and release timing evidence is in
[the acceptance report](../performance/v112-allocation-20261020.md).
