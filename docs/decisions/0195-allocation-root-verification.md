# ADR 0195: Separate allocation root verification from normal execution

Status: accepted. Recorded 2026-10-19. Supersedes ADR 0025's unconditional pre-allocation root
verification requirement; managed semantics and public embedding contracts remain.

Later [ADR 0196](0196-internal-reference-verification.md) also makes ordinary
internal membership scans diagnostic, while retaining the public and GC checks.

## Decision and trust boundary

Normal generated code no longer scans every published root and searches the
allocation list for each reference before allocating. The compiler establishes
internal reference flow; host references enter through validated handles. Roots
are still published in every build, and the collector still validates the heap
inventory and reachable references before sweeping. An incomplete or invalid
collection frees nothing. This change does not remove ordinary receiver/type,
borrow-lifetime, handle or reference-store checks.

The old scan only diagnosed invalid internal root contents. It neither published
roots nor performed collection. Its repetition on allocations that do not collect
multiplied root count by heap-list length and allocation frequency. That cost is
unnecessary for valid generated execution.

`CILICA_VERIFY_ALLOCATION_ROOTS` defaults to `0`. Compile generated C++ with
`-DCILICA_VERIFY_ALLOCATION_ROOTS=1` to restore the pre-allocation scan. Only values
0 and 1 are supported. Select the flag consistently across a native build and
record it in build/benchmark evidence; it is independent of `NDEBUG`, optimization
level and sanitizers. It adds no public ABI field, heap index or runtime allocation.

A private `Context.observe` callback still enables verification before observer
delivery in either mode. Invalid roots fail with status 6 before allocation or
observer delivery. All five allocation entry points use one helper: objects,
arrays, strings, static slots and type-initialization slots. Their earlier
argument/marking checks and fault ordering are retained.

In normal mode without an observer, private corruption of an unrelated root is
no longer guaranteed to fail on the next allocation. The collector's existing
validation remains active when collection runs. Tests that deliberately mutate
private roots must select verification or observation to require early rejection.
Raw memory corruption by native host code is not a new supported execution mode.

## Validation and performance evidence

`NativeAllocationVerificationTests` covers all five entry points at O0/O2 with
ASan/UBSan, both flag values, invalid unreadable roots, observation ordering,
foreign-instance roots under pressure and forced collection, and survival of live
roots through repeated collections. Existing object, array, string, tracing,
value/boxing, frame-budget, public-handle, static-storage and exception tests check
the composed runtime behaviour. The existing poisoned-root observation case now
explicitly attaches its observer.

The initial DiffPlex experiment replayed this exact production helper and its
five call-site changes onto the hash-pinned retained macOS generated C++ from
compiler revision `49dd0b1`. The original `codex/milestone-7-c-abi` branch predates
that build's additional frontend/library support. That isolated runtime-change
measurement is not evidence of fresh end-to-end DiffPlex compilation. Newly
emitted C++ was covered separately by the native regression tests above.

The reproduction script is [allocation_verification.py](../../tools/Performance/allocation_verification.py).
Full generated C++, binaries, logs and raw measurements are retained locally in
`artifacts/allocation-verification-20261019/replay`. The
[performance record](../performance/allocation-verification-20261019.md) reports
62 passing targeted tests and a 9.237x paired DiffPlex runtime-replay improvement
(138.849 ms to 15.031 ms median). The remaining gap to the fresh ordinary .NET
comparator is about 2,944x; this slice does not establish competitive performance.

The change is now integrated on `codex/runtime-performance`, based on latest
compiler tip `b8918ac` (native profile 90). Independently emitted baseline and
candidate C++ differ only in the intended runtime blocks. All 89 targeted tests,
both emission runs, four full DiffPlex transcript checks and two forced-collection
operations pass. Fresh paired timings improve from 143.511 ms to 15.350 ms,
**9.349x**. The [latest-compiler record](../performance/latest-diffplex-20261019.md)
supersedes the replay as current performance evidence.

## Review

The change adds no dereference of an unknown pointer, changes no object layout,
and removes no published root. Collector validation, handle generations and
instance admission are unchanged. Verification precedes observer delivery as
before. Observer mutation/reentrancy remains outside the private observer contract.
The macro defaults once per translation unit so multiple emitted closures compose.
Further removal of internal membership checks requires a separate audit of where
each reference originates; this slice does not assume all `owns` calls are redundant.
