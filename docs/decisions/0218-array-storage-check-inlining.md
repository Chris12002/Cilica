# ADR 0218: optimized array-storage validation

Accepted 2026-10-20. Native v111, following v110 commit 0e797b5.

The [v109 investigation](../performance/v109-profile-20261020.md) measured remaining
calls to valid_array_storage after primitive read inlining. This bounded check
validates size, length, stride/overflow and required/forbidden backing buffers.
Expose its complete body to optimized native inlining with
CILICA_ARRAY_STORAGE_CHECK. O0 retains its previous compiler-default treatment.

No condition is removed or weakened. Callers must still authenticate references
and descriptors before accessing headers. Typed entry points still reject invalid
storage with fatal status 6 before framed replay; diagnostic kernels repeat the
same validation. This adds no new reference trust, eligibility, GC or ABI rule.
Runtime bodies already live in the common header for independent shared modules;
the attribute macro deliberately adds no linkage keyword. Roslyn array IL and
Unity direct/inline accesses informed the preceding investigation, but neither
supplies a substitute for Cilica's private storage validation.

Review checked exact generated-body equivalence, authentication-before-header
access, native arithmetic and pointer provenance, O0 policy, shared linkage and
fault replay. Added boundary tests cover too-small/mismatched allocation sizes,
zero/mismatched/oversized lengths, surplus scalar backing pointers, unchanged
outputs and recovery. A genuine empty allocation distinguishes storage fault 6
from bounds fault 10, including exception allocation with forced collection.
The initial test synthesized an empty array by altering a live object's charged
size; the collector correctly rejected inconsistent heap accounting. The fixed
test uses a separate, rooted real allocation. No production fix was needed.

All 54 distinct selected checks pass across the initial run (50) and corrected
four-case follow-up, including independent shared-module compilation without LTO,
O0/O2, typed off/on, diagnostics off/on and sanitizer coverage. All 523 DiffPlex
observations match release and both O1 forced-GC ASan/UBSan modes on a checked
512–576 KiB stack. Maximum frames remain 432/384/384 bytes. No unresolved review
issue. Paired timing improves another 4.20%; see
[the final measurements](../performance/v111-array-storage-inlining-20261020.md).
