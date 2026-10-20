# ADR 0203: Typed scalar kernels with framed fault fallback

Status: accepted. Native v96. Recorded 2026-10-20.

Small validated scalar-only methods can use typed unsigned native arguments,
block-local values and initialized native locals. Admission is structural: at most
four parameters, eight locals, sixteen blocks and 96 statements plus block inputs;
no handlers, managed calls, heap accesses, allocation, stores outside locals or
addresses. Shared integer lowering preserves signed interpretation, wrapping,
shift masking, checked operations, division and wide conversions. Edge sources
are immutable locals, preserving parallel block transfers.

Direct internal call sites bypass argument packets. Existing packet-based entry
symbols still work. Cold entry, pending exceptions/faults, depth exhaustion and
arithmetic failures retain the original noinline framed implementation. A failed
speculative kernel has no externally observable effects; it restores depth before
retrying, so the framed path preserves fault origin, ordering and exception
materialization. Each active kernel counts toward the existing 64-call bound.
Valid nested calls no longer require arena space. No public ABI changes.

`CILICA_TYPED_INTERNALS` defaults on with optimization and off otherwise; explicit
0/1 permit differential and diagnostic runs. The fast body does not publish roots
because this slice contains no references, allocations or managed calls. Large
methods and unsupported operations retain existing storage and emission.

## Review and evidence

Reviewed admission versus every emitted expression, unsigned arithmetic, edge
copies, fallback replay, depth restoration and cross-unit declarations. Existing
module getter coverage links the shared runtime with separate method units.
Four forced-mode/O0/O2 differential cases cover loops, Int32/Int64 edge values,
shifts and caught arithmetic faults with forced GC and ASan/UBSan. Two native
checks cover zero-arena fast calls, cold/depth/pending fallback, cleanup and a
62-level generated recursive chain calling kernels on a 512–576 KiB pthread
stack at O0/O2 under sanitizers. Existing getter, direct-edge and execution-frame
checks pass (19 distinct checks across final runs).

Fresh DiffPlex C++ matches all 523 expected values. Alternating release runs
measure 167.498 to 163.697 us median (1.0232x; 2.27% less time), 21 samples each.
Ranges overlap, so the measured gain is modest. Largest O2 native frame remains
400 bytes. Exact source/compiler hashes and all measurements are retained under
`artifacts/typed-internal-20261020` and the summary/provenance under
`docs/performance/typed-internal-20261020`. No hosted work was run.
