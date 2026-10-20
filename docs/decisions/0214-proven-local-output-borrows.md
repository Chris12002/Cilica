# ADR 0214: direct scalar local outputs into proven kernels

Accepted 2026-10-20. Native v107.

DiffPlex's BuildPieceHashes takes the address of local 3 at IL_007d and invokes
TryGetValue at IL_007f (method token 06000011). Roslyn's ordinary local address and
Unity's retained `&V_3` output contrast with Cilica's repeated carrier construction
and resolution. Keep the existing typed-kernel proof, and prove this local at the
caller as well. Reference investigation: [v104 profile](../performance/v104-profile-20261020.md).

Eligibility requires an adjacent AddressLocal/Call pair, one use of that address,
exactly one writable non-narrow Int32/Int64 borrow parameter, and an integer result.
The callee must belong to the existing closed typed graph and its transitive write
set. Blocks above 128 statements and scans above 16 candidates remain conservative.
Virtual/interface calls require at most four targets, all eligible and non-boxed.
The receiver's exact descriptor guards address elision; ordinary dispatch checks
still select the target, whose exact function pointer guards the direct call.

A private wrapper accepts the real caller-local pointer. It preserves reference
and array-storage input checks, copies the scalar into temporary storage, invokes
the kernel, and commits only on success. There is no allocation, unknown callback,
external reference write or escaping address inside the graph; its depth-eight
and 512-statement proof remains. Caller roots and borrow registration still exist.
A failed kernel discards speculative writes, reconstructs the ordinary carrier
from the live frame token/slot, and uses the existing typed/framed fallback. This
preserves faults, depth accounting and managed writes observable before a throw.

Diagnostic modes, pending errors, marking, top-level and depth-boundary calls retain
the ordinary carrier route. Heap, reference, narrow, readonly, multiple/aliased and
escaping outputs retain existing handling. The public ABI and checked ownership
boundaries do not use the private pointer entry. Shared modules declare and link
it using the same conventions as typed entries.

Review covered single-use/alias rejection, exact dispatch, unsigned scalar storage,
reference entry checks, staging and fault replay, live frame lifetime, null order,
root publication and cross-unit linkage. All 37 focused checks pass, including
O0/O2, typed mode off/on, diagnostics off/on, Int64 outputs, interface/null paths,
partial fault writes and aliased ordinary borrows. The depth test initially expected
arena exhaustion status 9; the existing depth-limit status is 8, and the corrected
assertion passes. Full DiffPlex release and two forced-GC sanitizer modes retain
all 523 observations on the constrained stack. See
[measurements](../performance/local-output-performance-20261020.md).
