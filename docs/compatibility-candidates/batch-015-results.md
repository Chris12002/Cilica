# Batch015: parameterless String.ToCharArray and decoder composition

The owned ToCharArray slice is complete: nonempty strings produce fresh Char arrays
with exact UTF-16 code units, and empty strings return the existing cached empty
Char array. Null receivers use normal fault semantics. Implementation uses existing
managed reads/allocation/stores and adds no compiler/runtime operation, profile,
grant, quota, temporary buffer or source-list entry. Range overloads remain diagnostic.

**27/27 focused and adjacent checks passed in59.236s.** New/repaired reductions supply
16,068 repeated native/.NET comparisons at O0/O2:2,016 full payload/independence,
12,288 groups covering every one of65,536 UTF-16 units,294 receiver faults,
882 readonly-reference composition,294 original ToCharArray-gap and294 UInt32
empty-array identity/bounds comparisons. Forced GC, ASan/UBSan, strict2KiB native
frames, exact root/borrow cleanup and exact/one-short managed frame checks remain.
Twenty final generated-source/executable hash pairs are retained, along with twenty
pairs and all logs from the initial implementation run.

## Composition results and substantive blocker

The independently authored readonly `in string` call through TrimEnd(Char[]) and
ToCharArray passes native/.NET comparisons. It covers a borrowed managed reference
across allocation and exact result payloads. This is minimal composition evidence,
not full candidate compatibility.

The independent `Func<char,bool>` passed to Enumerable.Any reaches **CIL2001**:
unsigned/narrow delegate parameters need later lowering. It never executes natively.
The final suite retains an exact diagnostic check rather than changing the candidate
to use Int32 or widening admission without width/identity/callback tests. This is the
next meaningful blocker for the screened Albireo.Base32 decoder at
1eabd2fcba515d0bd63881fd2a2879555c43648b (MIT/netstandard2.0).

No upstream candidate was downloaded, staged, compiled or executed. The prior pinned
online screen is reused; an eventual external trial still needs its exact approval.
The encoder's Decimal.Ceiling path, complete decoder closure and other excluded
candidate surfaces remain unverified.

## Repairs, evidence and cleanup

The baseline records2/2 CIL3010 failures for missing parameterless ToCharArray.
The initial implementation run passes25/28: two failures expose the narrow-delegate
gate, and the third is a stale Array.Empty<UInt32> rejection from before existing
UInt32 support. The latter now tests positive UInt32 cache identity, distinct fresh
and Int32 arrays, and bounds behavior; UInt64 remains an explicit negative and
exact-grant denial is preserved. No new UInt32 support was implemented here.

See [source/binary/native hashes and raw attempts](batch-015-evidence.json),
[passing log](batch-015/repaired-tests.log), [manifest](batch-015.json) and
[design/review](../decisions/compatibility-017-string-character-arrays.md).
Peer Decimal/SequenceEqual work remains unchanged. Peer Stack6408fc2 committed after
the final run (recorded timestamps); source/DLL/native hashes retain the tested baseline.
The suite was not repeated after that unrelated merge. Unity lane was informed before
heavy jobs and after completion; no clean timing interval was used. No candidate
copies required cleanup. Existing test harness cleanup follows native hash capture.
Full repository tests and Alchemy replay were not run. Next: independently design
and verify Char delegate-parameter lowering before preparing a decoder execution recipe.
