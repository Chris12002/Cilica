# ADR AC0015: Fixed-invariant numeric append

Accepted 2026-10-06 for bounded AC7b.2.

## Decision

Implement StringBuilder.Append(int/uint/long/ulong) by existing IntegerText
formatting followed by existing string append. Reuse its exact cache grants,
managed decimal algorithms, chunk growth and root tracing. No new intrinsic,
profile or native ABI is required. All input arguments are evaluated before the
ordinary null receiver check. Integer formatting handles signed minima without
negating them in their signed width.

The selected bounded core has a fixed invariant numeric-text contract. Default
.NET append uses ambient culture; Cilica does not support changing that culture.
A matching differential host must explicitly select InvariantCulture. The
culture choice was presented while independent ordinal work continued; absent
further steering, this limited contract was selected and is recorded at the
public ledger entry. Do not advertise arbitrary ambient-culture equivalence.

The implementation creates intermediate strings, unlike the pinned span fast
path. Allocation counts and CLR memory-exhaustion timing are not equivalent.
This is a bounded runtime adaptation, not a new claim of general StringBuilder
or culture support. Float, decimal, object and other overloads remain unmapped.

## Evidence and review

Six authored probes and public StringBuilderInt supply 2,058 repeated native/.NET
comparisons on macOS ARM64 at O0/O2 with ASan/UBSan and forced GC. Cases cover
all integer widths, signed extrema, maximum unsigned values, null argument-order
faults, builder return identity, exact UTF-16, length/capacity and snapshots across
chunk boundaries. All 37 existing builder/baseline checks pass. Test culture is
restored with finally and does not change input metadata or execute assemblies
inside the compiler.

Review checked overload binding, exact formatter reuse, null-receiver ordering,
root lifetime across intermediate allocation, preserved append mutation order and
scope documentation. Native v41 and interface plan v5 are unchanged. The ledger
has 26 gaps and 21 controls. Private saved-game equivalence remains unfinished.
