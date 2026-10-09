# ADR AC0245: Measured integer-object root-frame capacity

Recorded 2026-10-09 for AC8.101 after a56dfc7.

After compacting cast-message resources, frozen Alchemy reaches the 1,024-root
limit in GameState.With. A diagnostic-only 16,384-root inventory measures the
complete logical and scalar closure: six methods exceed 1,024; the largest is
SpellResolutionHandler.ResolveChain with 1,387 logical / 1,711 scalar roots.
GameState.With needs 1,088 / 1,104. The converted GameState.With independently
shows carried constructor arguments across conditional branches and final
allocation: Alchemy.Engine__2.cpp:28237-30222, especially 30187-30220; SHA256
`40464e461a6b15890181e01b2405e3483310d6357acbf6cb738d552222a599ab`.
The separate Unity capture has a different input revision. It guides the storage
shape, not Cilica's precise root counts or collector design.

## Decision and review

The integer-object profile admits at most 2,048 distinct roots per method; legacy
catalogs retain 1,024. Both logical and scalar planners select the same bound from
the validated catalog. The native emitter writes that selected constant into the
closure; reference tracing and borrow registration enforce it before reading root
or predicate buffers. Diagnostic text reports the actual selected limit.

Keep conservative distinct slots and the existing managed-location ownership
rules. Slot reuse would require separate proofs for parallel edge copies, aliases,
exception transfer and borrow lifetimes. Existing heap-backed execution frames
already charge their actual byte size against the host's execution budget. The
new root allowance does not increase runtime bytes, recursion depth, handles,
aggregate scalar storage or native stack limits. No game rule or generated source
from another compiler is substituted. Review checked every native root-count guard,
profile propagation through scalar lowering, slot/index widths, allocation cleanup,
root-buffer/predicate extents and exact/one-short execution-byte behavior.

The first native regression exposed the old independent 1,024 guards in tracing
and borrow registration. Both now use the generated constant; direct native
boundary consumers keep them aligned with both planners. CIL125/object IR78/native88;
layouts, exception IR10, core and grants remain unchanged. No outstanding findings
within this slice.

## Evidence

71 distinct selected checks pass across WideRootFrameTests, ScalarLocationIrTests,
BoxedValueIrTests, DelegateValueIrTests, NativeTracingTests, NativeScalarLocationTests
and ObjectNativePlanningTests. A 28-argument branching constructor first reproduces
the old root-limit rejection, then passes 294 O0/O2 native/.NET comparisons with
forced collection between partially prepared arguments. Heap/frame/borrow cleanup,
exact and one-short frame byte budgets, ASan/UBSan and the 2 KiB native-frame warning
pass. Both planners admit exactly 1,024/2,048 roots for their respective profiles
and reject one additional root. Native tracing and borrow registration exercise
the same exact and excess boundaries at both optimization levels. Initial test
consumer export-name collisions were resolved by isolating the two generated
profiles' names in the combined test source. Receipts:
artifacts/alchemy-wide-root-frames.

The diagnostic inventory passes all 15,254 bodies, complete logical validation,
scalar lowering and root assignment. It has 61,756 blocks / 371,161 scalar statements,
196,204 logical roots and 243,685 scalar roots; maxima are 1,387 / 1,711. Cast text
now uses 2,567 source parts + 371 target parts = 306,977 UTF-16 units; ordinary
literals use 352,478 charged units. Discovery remains 3,839,616 metadata units.
The first 150-second run stopped during the newly reached scalar validation;
one bounded 300-second follow-up completes the engine probe in 188,234ms under
shared host load. StateLookup remains successful (15 methods, 152 roots, maximum
24; 755ms). This copy uses explicit aggregate capacity allowances; neither native
emission, native engine execution nor saved-game replay follows from it. Production
aggregate capacity remains unresolved. Frozen input/core/manifest pins are unchanged.
Receipts: artifacts/alchemy-root-capacity-inventory and
artifacts/alchemy-root-capacity-inventory-r2.
