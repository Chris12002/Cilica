# ADR 0215: inline small exact descriptor predicates

Accepted 2026-10-20. Native v108; based on committed v107.

The v107 profile found 6,498 short descriptor-predicate calls per DiffPlex diff.
Their bodies already implement exact address comparisons; the remaining overhead
comes from a historical blanket noinline attribute. Roslyn retains ordinary
reference operations in IL; Unity's matching generated C++ and runtime expose
small checks to Clang. See [the investigation](../performance/v107-profile-20261020.md).

Sets with fewer than eight descriptors now request always_inline only when the
native compiler defines `__OPTIMIZE__`. O0 retains noinline. A single membership
plan selects identical attributes on forward declarations and definitions.
Broad sets keep their existing checked address tables and noinline boundary.
No candidate descriptor is dereferenced, and null, foreign and colliding addresses
remain rejected. Public ownership, GC validation and managed checks are unchanged.

The attribute contains no C++ linkage keyword: single-file emission remains static,
and the existing module builder promotes namespace-scope definitions to inline.
This preserves one descriptor/function identity across translation units, including
predicates stored in field/root metadata and invoked through function pointers.

Review covered attribute agreement, the seven/eight-entry threshold, exact boxed,
nominal, interface and array membership, address-taking, shared linkage and O0
stack behavior. Existing exact-set/collision tests pass at O0/O2 under ASan/UBSan.
Expanded module checks test all emitted sets, invalid addresses and peer-unit
membership. A test-harness main initially exceeded its 2 KiB O0 frame warning;
moving checks to a reusable helper retains the original limit. All twelve focused
checks pass after that harness fix. Full DiffPlex release and two forced-GC
sanitizer modes retain all 523 observations on the constrained stack. See
[measurements](../performance/v108-small-predicates-20261020.md).
