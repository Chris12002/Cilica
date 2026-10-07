# ADR AC0137: Whole-vector generic Array.IndexOf

Recorded 2026-10-07 for AC3.24.

The frozen engine reaches Array.IndexOf<string> in EffectOperationDescriptor.Validate
IL_0013 after 327,656 metadata work units under the measured diagnostic capacities
from AC8.36. StateLookup remains eight methods/9,925 units. No engine execution.

Add only Array.IndexOf<T>(T[], T) as ordinary owned managed code. Validate array
null with ArgumentNullException("array"), then call the existing exact-granted
closed array search over array.Length. Reuse copied scalar/string candidates and
live borrowed IEquatable value candidates. Mutating or allocating value equality
therefore preserves the existing receiver and roots; first matches short-circuit,
empty/no-match results are -1, and argument evaluation still precedes validation.
No new compiler branch, intrinsic grant, native operation or profile is required.

The required string path preserves null and exact UTF-16 equality. Int32/Int64,
nominal Int32 enum arrays and admitted IEquatable structs share existing lowering.
Nullable/reference fallback equality, start/range overloads and narrow byte/char
local borrows remain diagnostic. Broad Array API support is not claimed.

Review covers exact generic mapping, null/argument ordering, live struct mutation,
throwing/allocating equality, root cleanup and unchanged native emission. The
private helper test now expects the earlier lexical-access rejection rather than
a later intrinsic-caller rejection. It still verifies external calls are denied.
Full engine execution, frozen fingerprint execution and saved-game replay remain open.

All 28 selected checks pass: 11 new search/grant/boundary checks, 12 existing
list/array search checks and five public-baseline checks. Eight probes supply
2,352 repeated O0/O2 native/.NET comparisons with forced GC, ASan/UBSan and
exact frame quotas. Six unsupported probes remain diagnostic; removing either
search or equality permission denies compilation. Evidence:
artifacts/alchemy-array-indexof/tests-fixed.log. Baseline remains 45 controls/two gaps.
