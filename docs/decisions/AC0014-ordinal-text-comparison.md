# ADR AC0014: Ordinal text comparison

Accepted 2026-10-06 for bounded AC7b.1.

## Decision

Supply String.CompareOrdinal(String,String) and IsNullOrEmpty as ordinary managed
code over existing string length/indexing. Preserve the pinned runtime's exact
numeric comparison result, not only its sign. Compare the first two UTF-16 units,
then pairs through the shorter string, supplying zero for terminator/padding reads.
An ordinary internal OrdinalText helper avoids treating a private String helper as
a public runtime intrinsic. Do not relax the String declaration gate.

The common index never exceeds the shorter length; the last paired index can
reach that length but cannot overflow Int32. Code-unit subtraction is in
[-65535,65535], length subtraction in [-Int32.MaxValue,Int32.MaxValue]. All reads
use managed string indexing after a length guard. No allocations, native layout
changes, new grants or profile changes are introduced. These bodies are adapted
from the already pinned String.Comparison.cs and String.cs behavioral sources.

## Evidence and review

Four authored probes supply 7,500 repeated exact O0/O2 comparisons against .NET
10.0.11. Public StringCompareOrdinal adds 294. Cases include null/empty, identity,
distinct snapshots, odd/even and long prefixes, embedded zeroes, arbitrary selected
UTF-16 units, unpaired surrogates and supplementary pairs. ASan/UBSan, forced GC,
frame and cleanup checks pass. All 71 baseline/ordinal/empty/metadata boundary
regressions pass after isolating the next numeric-append slice from this baseline.
The initial helper-on-String rejection was corrected by moving the ordinary helper;
no authorization check was weakened. Review checked arithmetic bounds, padding,
null/identity order and unchanged admission gates. The ledger has 27 gaps and 20
controls. Prefix APIs, substring, formatting and encoding remain open; no private
replay or non-macOS execution evidence is claimed.
