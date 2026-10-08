# Batch 009: CRC execution proposal and CircularBuffer screen

The separately reviewable [CRC32.NET proposal](crc32-execution-plan.md) is ready:
commit `26c5a818a5c7a3d6a622c92d3cd08dba586c263c`, MIT, two unchanged implementation
files, netstandard2.0 with NETCORE/NETCORE20, and an owned 41-scenario adapter plus
explicit differential driver. Existing tooling, offline 2-CPU/8-GiB/30-minute
isolation only. **No CRC code, recipe or driver was compiled or executed.** Approval
is still required by the user's explicit third-party execution boundary.

Online screening of
[joaoportela/CircularBuffer-CSharp@9ebb911](https://github.com/joaoportela/CircularBuffer-CSharp/tree/9ebb911dfe27a62c2793d0b02c642f79bbbf524b)
found a one-source library, Unlicense, a netstandard2.0 asset and no library runtime
packages. Complete pinned tree, library/example/test projects, licence, entire
implementation and test file, package configuration and CI/publish scripts were
inspected before any download. No candidate copy was staged and no upstream code,
project, tests or workflow executed. The [manifest](batch-009.json) retains nine
file pins, dependency graph, reachable requirements and exclusions.

Four independently authored API reductions identify useful bounded gaps:

| Requirement | Current evidence | Priority |
| --- | --- | --- |
| `Array.Copy(Array,Array,int)` | CIL3010 missing Copy | First: every candidate constructor calls it |
| `Array.Copy(Array,int,Array,int,int)` | CIL3010 missing Copy | Follow-up: ToArray and overlap semantics |
| `Array.Clear(Array,int,int)` | CIL3010 missing Clear | Follow-up: Clear and reference release |
| `ArraySegment<T>` construction/getters | CIL3015 missing contract mapping | Follow-up: zero-copy segments, ToArray and enumeration |

The candidate is **mapped-blocked**, not semantically verified. These APIs are
explicitly absent from the current documented profile; diagnosing them is expected,
not a wrong-code defect. An Int32/same-type vector copy slice is a plausible bounded
extension using existing element/root machinery. General System.Array operations,
multidimensional/non-zero-bound arrays and primitive coercion are not implicitly
included. A design and ownership agreement should precede production changes;
there is no new compiler/runtime/library implementation in this batch.

Two focused checks pass on compiler/library e232f8b plus owned reductions. Peer
c64312e changes only diagnostic documentation and was preserved. One test checks all
four exact admission diagnostics; the other establishes normal .NET outcomes for
whole copy, overlapping self-copy, section clearing and live segment aliasing.
No native counterpart runs while translation is denied. Two initial test assertion
mistakes and their logs are retained, distinctly from actual compiler diagnostics.
The first build had the pre-existing CS0108 State-hiding warning; final incremental
build passed. [Evidence](batch-009-evidence.json) records exact executed binary/source
hashes and final 2/2 result. This is not a whole-repository test pass.

Coordination: Review milestone 6 was notified of array/segment screening and owns
its Alchemy additions. Profile Cilica performance owns instrumentation, hotspot
experiments and clean timing; this lane finished its host work before that window.
No overlapping production files were edited. Original DiffPlex evidence is unchanged.
The handover's earlier assembly hash was corrected: `514ff...` identifies the prior
correctness assembly; the benchmark assembly is `1da7fb72d688a9b346fdb52318d9683a2bd861bd4d09dcd3eca933c6da0368bb`.

Next: parent obtains a separate decision on the exact CRC proposal. Feature work
can design a bounded array-copy slice with overlap, fault order, partial writes and
GC/lifetime regressions, then reassess the remaining CircularBuffer closure. Keep
handcrafted exception, boxing, generics, dispatch and GC tests. No candidate copies
were created, so no cleanup is needed; all owned tests, plans and evidence remain.
