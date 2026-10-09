# ADR AC0248: Check boxed dispatch eligibility before table scans

Recorded 2026-10-09 for AC8.104 after 251d018.

Full engine module emission reached its 300-second bound during scalar validation.
BoxedTypeRelations.ValidateDispatch was scanning complete interface dispatch tables
for every box/owner pair, including owners that the payload cannot implement.
The converted engine's typed calls distinguish receiver eligibility from dispatch;
this change concerns Cilica's own compile-time validation only.

## Decision and review

Keep the existing work charge for every pair, then continue immediately when
Accepts(type, owner) is false. Compute payload dispatch only for eligible pairs.
The two callers already validate the complete logical/scalar interface and class
maps before reaching this stage. A new reduced negative removes a dispatch map
from the existing NamedWithScalar case and verifies rejection at both stages.

Review confirms the same pair traversal, work-limit boundary, assignability test,
confinement proof calls and diagnostics for accepted/eligible flows. No allocation,
rooting, native boundary, runtime or admission behavior changes. Unrelated invalid
maps remain rejected by the preceding validators. No outstanding findings.
No profile/version change is needed; four roots emit byte-identical C++ against
verified baseline/candidate compiler DLLs and one fixed fixture image: NamedWithScalar
(3 methods), PayloadReturn (3), ClassWithScalar (5), NativeModuleProbe.Run (10).
61 targeted BoxedDispatchEligibility, BoxedInterface, BoxedValueIr and ScalarBoxedIr
checks pass (22.319s), including their existing native O0/O2/forced-GC/sanitizer
checks. Receipts: artifacts/alchemy-boxed-dispatch-eligibility.

## Full diagnostic closure

A follow-up under the unchanged 300-second allowance completes. Each measured
boxed-dispatch validation charges 585,706 pairs across 869 owners; only 541 pairs
need dispatch-table inspection. Recorded passes take roughly 0.22 seconds for
that stage. These are diagnostic work counts and shared-host timings, not a
controlled benchmark or product throughput claim.

EngineStep's 15,254 methods now emit through the production module API into 67
source files plus one 31,833,867-byte header. The largest source is 8,388,322 bytes;
total output is 466,820,581 bytes. Translation/validation/emission takes 137,351ms.
The module manifest SHA256 is
`eccbe80b7c1720932ff739b72695918eaebbf57eacea07e9981e192d0beaa9e0`.
StateLookup emits two source files plus its header (1,424,461 total bytes) in 854ms;
manifest SHA256 is
`a3a58eada50db2728d50e515ae9e24c5cc3ec276b94bb8094042bb5400b77547`.
All frozen input/core pins, diagnostic aggregate ceilings and production 2,048-root
checks remain unchanged. Receipts: artifacts/alchemy-boxed-dispatch-module-emission.

The first full O0/ASan/UBSan build compiles the shared header, then the 2 KiB
native-frame warning rejects allocate_box_storage: 12,448 bytes for the full
catalog. The bounded container exits in 23.649s and is removed. This is a real
native layout issue to reduce and fix next, not a waived warning. Native engine
execution, production aggregate-capacity selection and full saved-game replay
remain outstanding. Receipt: artifacts/alchemy-module-native-run/engine-O0.
