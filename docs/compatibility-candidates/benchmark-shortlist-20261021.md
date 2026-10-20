# Benchmark candidate review: interval trees and priority queues

Date: 2026-10-21. Status: preliminary shortlist, outside the numbered batches.

This records the supplied review of two prospective integer-only library workloads
and an optional allocation microbenchmark. Neither new library has a pinned
reference manifest, a Cilica compilation result or execution/performance evidence.
Do not interpret this shortlist as compatibility acceptance or an execution plan.

The existing candidate records verify selected roots of CircularBuffer, Crc32.NET,
DiffPlex and Albireo.Base32 Decode against .NET. The Albireo Unity reference has
documented semantic differences; these are not four claims of whole-library support.
See the [candidate index](README.md) for the bounded results and retained evidence.

## Proposed candidates

| Candidate and selected shape | Additional coverage | Proposed workload | Main admission questions |
| --- | --- | --- | --- |
| [RangeTree](https://github.com/mbuchetics/RangeTree), `IntervalTree<int, int>` | Recursive object graphs, interval structs, comparison dispatch and result enumeration | Fixed-seed intervals; separately measure construction and point/range queries, with result checksums | General comparer-based sorting, exact LINQ/enumerator closure, recursive depth and allocation bounds |
| [BlueRaja priority queue](https://github.com/BlueRaja/High-Speed-Priority-Queue-for-C-Sharp), `GenericPriorityQueue<Node, int>` | Reference-array heap updates, comparison delegates, generic inheritance and Int64 insertion order | First a deterministic queue-operation trace; then Dijkstra on a flat integer-weighted grid | Comparer method-to-delegate construction, closed inherited members and exact array-copy/clear operand proofs |

The supplied review identifies both as small MIT-licensed candidates. Licence,
project imports, dependencies and an eligible .NET Standard asset must be captured
at the selected commit before admission. RangeTree's detailed source claims below
remain preliminary: this follow-up reached its repository page but could not retrieve
the requested source files. No pinned source inventory was produced.

### RangeTree

The supplied source review describes median-split class nodes, `RangeValuePair`
structs, `IComparer<TKey>` calls, sorting, materialization and `Select`/`Where`.
That combination could expose costs absent from the current character-diff benchmark.
Validate the actual overloads and reachable bodies at the pin, including custom
pair comparers rather than assuming all sorts use `IComparer<TKey>`.

Cilica documents integer `Comparer<T>.Default` (AC5a.2), comparison-delegate
`List.Sort` (AC3.10), and a specifically proven ordinal string-comparer sort.
These do not establish arbitrary `List<T>.Sort(IComparer<T>)` support. Treat that
as a likely gap, without predicting that its implementation will be small.

Use a brute-force interval oracle as well as .NET execution. Include duplicate
endpoints, nested/disjoint intervals, empty results and boundary queries. Define
whether result order is contractual before choosing an order-sensitive checksum.

### GenericPriorityQueue

The inspected, unpinned [queue source](https://raw.githubusercontent.com/BlueRaja/High-Speed-Priority-Queue-for-C-Sharp/master/Priority%20Queue/GenericPriorityQueue.cs)
stores a reference heap, an Int64 insertion counter and a comparison delegate.
Its default constructor passes through `IComparer<TPriority>` and captures
`comparer.Compare`. Support for the individual APIs does not prove this composition.
Record default-comparer and explicit-comparison constructors as distinct roots;
an explicitly scoped constructor choice must not conceal a failure of the other.

`Array.Copy` and `Array.Clear` support is bounded by operand provenance and element
shape. Compile the actual candidate calls before describing it as performance-only
work. Exercise equal-priority FIFO behavior, priority updates, removal, resize,
clear and reuse in correctness traces. Keep Release workloads inside the upstream
queue's valid-use contract; disabled debug guards are not Release guarantees.

Correction to the supplied review: [SimplePriorityQueue source](https://raw.githubusercontent.com/BlueRaja/High-Speed-Priority-Queue-for-C-Sharp/master/Priority%20Queue/SimplePriorityQueue.cs)
has a generic priority form, so integer priorities are possible. Its lock-based
wrapper and extra collections are the reason to exclude it from this bounded
first workload. The selected candidate remains `GenericPriorityQueue<Node, int>`.

For Dijkstra, use nonnegative bounded weights, define integer overflow behavior,
and compare the complete distance result to an independent small-graph oracle
before timing. Separate node allocation/setup from heap operations so the result
can distinguish allocation costs from comparison and array-update costs.

## Optional allocation workload and exclusions

A single-thread binary-trees workload could isolate allocation, tracing and
collection with a retained long-lived tree and repeated temporary trees. This is
a microbenchmark, not a third library. Pin a particular program and licence or
author an independent fixture; record depth, live-set size, node count and checksum.
No particular Benchmarks Game implementation has been screened here.

Floating-point and jagged/multidimensional-array requirements remain outside the
selected profile. This excludes candidate roots requiring those features, not all
pathfinding algorithms: the proposed integer-grid workload needs neither.
Chess is deferred for scope and incomplete source screening, without asserting
unverified Span/unsafe requirements for a particular release. The supplied leads
were [Rudzoft.ChessLib](https://packages.nuget.org/packages/Rudzoft.ChessLib) and
[raphaelbg/ChessLib](https://github.com/raphaelbg/ChessLib).

## Entry gates and measurement design

1. Follow the [candidate workflow](README.md): select exact commits, review build
   inputs/licences/dependencies and create pinned reference manifests before adding
   either candidate to a batch. Do not add to an unfinished batch by default.
2. Select exact roots and Release constants, compile their complete reachable
   closures and retain actionable diagnostics. No compiler/runtime changes are
   part of this review. Reduce a demonstrated gap before proposing a support slice.
3. Before designing support or optimization, use REA to inspect matching Roslyn IL,
   Unity IL2CPP and minimal-host NativeAOT references. Retain method identities,
   input/binary hashes, commands and tool versions; disclose unavailable references.
4. Establish .NET differential correctness, then appropriate O0/O2, forced-GC and
   sanitizer checks. A checksum supplements semantic checks; it does not replace
   identity, ordering, mutation and fault assertions.
5. Regenerate Cilica C++ from the selected compiler revision. Time paired Release
   runs against ordinary .NET default/non-tiered and matching IL2CPP where available;
   report NativeAOT separately. Keep DLL identity, workload, checksums and host load
   comparable. Separate untimed counters and sanitizer runs from timing.
6. Profile to determine whether object traversal, collection, interface/delegate
   dispatch or struct/scalar work dominates. Use these results to test whether
   proven-check removal and devirtualization gains generalize beyond DiffPlex;
   they are hypotheses, not promised improvements.

This documentation change launches no candidate execution, reference builds,
hosted CI or additional agents. Existing upstream execution approvals do not
extend to these libraries. Future work must confirm the exact active checkout and
compiler ancestry rather than reuse unrelated retained generated code.
