# ADR AC0091: List AddRange

Accepted 2026-10-07 for AC3.12.

Adapt the pinned List<T>.AddRange managed body. Validate collection for null, then
use runtime ICollection<T> detection. Read Count once; nonpositive counts do
nothing. For positive counts, grow from checked live size + count if needed,
call CopyTo with the then-current backing array and size, then add count to the
live size and increment version. Existing typed CopyTo already handles self-copy.
Do not snapshot the destination size across callbacks: reentrant mutation and
partial writes are observable. Other enumerables use MoveNext/Current/Add in a
using scope, preserving per-item growth/version updates and disposal precedence.

No compiler/native profile, grant, quota or platform boundary changes. This body
also supplies Concat.ToList's future per-source append path; query-specific
materialization remains a separate slice.

Eight authored probes plus the public AddRange probe pass 2,382 repeated
native/.NET comparisons: arrays/capacity, self-addition, wrapped self-enumeration,
references, reference-bearing wide values, null, a 26-pair collection callback grid
and a 28-pair enumerable fault grid. Collection cases cover zero/negative/overflow
counts, Count faults, Count mutation, partial CopyTo faults, backing-array
replacement, Clear/RemoveAt during copying, absent writes and version changes.
Enumerator cases cover GetEnumerator, MoveNext, Current, null cursor, Dispose and
Dispose replacing an earlier fault. Captured destination arrays expose partial
stores even when size is not published. Cleared scalar slots retain their pinned
behavior from AC3.10.

All 44 distinct selected checks pass across a/b logs, including list-copy,
mutation and the updated public baseline: 40 native controls, seven known gaps.
The a run's two failures were the wrapped-empty test's zero-static-root assumption;
empty List interface enumeration intentionally uses one shared cursor. The b run
splits empty/nonempty inputs and checks exactly one/zero extra roots. No runtime
change was needed. O0/O2 runs retain forced GC, ASan/UBSan, exact storage quota
recovery and cleanup, 2,048-byte frame warnings and 30-second subprocess caps.

Review checked pinned callback order, live-size/version updates, checked growth,
self overlap, captured-array lifetime through reentrant allocation, partial
mutation on failure, empty cache retention and finally precedence. Frozen Alchemy
inputs remain unchanged and engine/state still hit production type bounds; full
engine execution and saved-game replay are unfinished.

Evidence: artifacts/alchemy-list-add-range/ac3-12a-* and ac3-12b-*,
engine-and-state.json and core/library-build.json. Public fixture hash:
212f8cefb5f5978bbc38b2470aba9a37c2f78a64db61375bf5ba302d88fc3228.
Core: bb1edbf776ec6921c812b1b415683621cb98b62ee859d00aed91af5c45289e6e.
Manifest: e9c49307b93496702ad6642308662915ccccb1cfc7b17647fbc24578b7706803.
