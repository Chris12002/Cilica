# ADR AC0045: Managed string-array concatenation

Accepted 2026-10-07 for bounded AC7b.5.

Implement the exact String.Concat(string[]) overload as ordinary selected managed
code. Null arrays throw ArgumentNullException(values); empty/all-empty inputs return
the canonical empty string; one element returns that element or empty unchanged.
Multiple entries with a nonempty total produce a fresh string even if only one
entry contains text. Accumulate lengths in Int64, then preserve the pinned
OutOfMemory_StringTooLong message before any result allocation. The product of the
largest admitted array count and string length cannot overflow Int64.

Copy exact UTF-16 code units through a temporary Int32 vector and the existing
private FromCodeUnits helper. This uses extra bounded temporary storage compared
with upstream direct string allocation; no new native operation or grant is added.
Retain size-mismatch checks and defensive array snapshot/retry. That helper lives
in Cilica.Runtime because private String methods do not satisfy the separate
string intrinsic declaration gate. Concurrent array mutation is not validated by
the current bounded single-thread managed profile. Snapshot guards alone are not
claimed as multi-thread compatibility.

Six ordinary probes supply 1,764 repeated native/.NET comparisons at O0/O2 on
macOS ARM64. A separate oversized-total probe adds six comparisons with one input
pair: a 32,768-unit string shared by 65,536 array slots exceeds Int32.MaxValue,
without allocating a multi-gigabyte result. Both runtimes produce the same message.
All seven tests pass under ASan/UBSan, forced collection, strict cleanup and native
frame limits. A denied existing UTF-16 construction grant and the object-array
Concat overload remain diagnostic. 34 adjacent string-builder/boxed-formatting/
public-baseline checks pass. One stale private-caller assertion is corrected to the
earlier AC8.2 lexical rejection and passes separately. Initial helper-visibility
failure and corrected logs remain in artifacts/alchemy-array-concat.

Review checked empty/single/multiple identity, UTF-16 surrogates and NUL, bounds and
length arithmetic, snapshot ownership, array mutation after return and explicit
exception messages. The pinned String.Manipulation.cs is vendored with its hash.
All compiler/runtime profiles remain unchanged. Object-array/generic enumerable
concatenation and three/four-string overloads remain unsupported.

Frozen engine discovery now reaches missing predicate FirstOrDefault in
AddOpportunityAdvanceEvents IL_004c. StateLookup retains its 257/256 identity limit.
This is diagnostic progress only; engine execution and full saved-game replay
remain unfinished.
