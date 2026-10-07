# ADR AC0090: General enumerable List construction

Accepted 2026-10-07 for AC3.11.

Widen the AC3.8 constructor factory to the actual IEnumerable<T> contract using
List<T>.CopyEnumerable(IEnumerable<T>). Keep allocation of the destination object
before managed validation through the private trivial constructor. Runtime
ICollection<T> sources keep the pinned fast branch: read Count once, use empty
storage for zero, allocate exact capacity otherwise, call CopyTo, then publish
size. Other sources initialize empty storage, acquire an enumerator and repeatedly
MoveNext/Current/Add inside a managed using scope. Growth, current-copy semantics,
partial work and finally-disposal therefore use existing owned/runtime behavior.

The existing ListCollectionConstruction capability now pins the exact renamed
bridge token in the newly built manifest. Discovery checks the exact original
newobj declaration and managed bridge flags, arity, IEnumerable<T> parameter and
List<T> result. Translation still requires assignment to that exact enumerable
contract or null. No general constructor lowering, new intrinsic capability or
native helper is added. Missing grant and detached arity/operand/type/fault-edge
mutations remain rejected. Integer CIL v28 records admission; IR/native profiles
and production limits remain unchanged.

Seven new probes plus the two previously rejected alias/yield probes pass 2,874
repeated native/.NET comparisons, including explicit fault grids. Coverage spans
empty/nonempty sequences and growth capacities, class and boxed-struct cursors,
compiler-generated yield/finally, reference and reference-bearing wide values,
null, dynamic collection detection and all acquisition/MoveNext/Current/disposal
stages. The 28-pair fault grid covers disposal overriding an earlier movement
fault and null enumerators. The ten-pair collection grid covers every Count/CopyTo
mode, including negative count and CopyTo throwing after its first store. Existing
collection construction tests retain exact copy capacity and callbacks.

All 36 selected checks pass across a/b/c logs. These include grant denial,
detached validation at both stages and the unchanged 39-control/8-gap public
baseline. A supplementary 16-pair cursor grid adds 96 comparisons for all eight
AC4.8 mutation modes, explicitly including mutation after exhaustion, capacity-only
changes and failed removal. O0/O2 runs retain forced GC, ASan/UBSan, exact execution
storage recovery/cleanup, 2,048-byte native frame warnings and 30-second subprocess
caps. No failures, timeout increases or hosted runs occurred in this slice.

Review checked newobj allocation/fault order, exact grant/signature matching,
dynamic collection dispatch, count snapshot and post-copy publication, value
copies/rooting, null acquisition, finally precedence and quota recovery. Frozen
Alchemy inputs still stop at production type limits; ToList remains the next
measured library gap beyond the diagnostic capacity copy. Full engine execution
and saved-game replay remain unfinished.

Evidence: artifacts/alchemy-list-enumerable/ac3-11a-* through ac3-11c-*,
engine-and-state.json and core/library-build.json. Public fixture hash:
a895789d9072fb432b05913dc3376807edbdd8a733d32098010b5ad0b6360276.
Core: 2b32a8002135c688fd33f7b4b1fd9b866ac5a800e3fd87acfc1569cdfa44f037.
Manifest: 2731fd4d46edf513117bc8121e08b2538ee390b490a2bee76ea17353e29c2b34.
