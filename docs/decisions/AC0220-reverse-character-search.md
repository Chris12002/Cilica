# ADR AC0220: Reverse UTF-16 character search

Recorded 2026-10-08 for AC8.76 after AC8.75.

Frozen SurfaceOccupants.SettleDrops calls String.LastIndexOf(Char); the same source
also calls its start-index overload. Implement the coherent three-overload character
family in the existing StringSearching source, using pinned String.Searching.cs and
CoreLib resource messages. Keep the forward search unchanged and application inputs
frozen. No new files are needed in the source build list or upstream originals.

The full-range overload supplies Length-1/Length. The start-only overload uses the
pinned unchecked startIndex+1 count. The range overload reads Length first and returns
-1 for every empty string, including invalid start/count. On nonempty strings it checks
unsigned startIndex against Length, then unsigned count against startIndex+1. These
checks guarantee first=startIndex+1-count is nonnegative and bounded. Scan backward
through existing checked UTF-16 reads; count zero performs no reads and decrementing
zero to -1 terminates safely. Preserve exact startIndex/count messages and exception
precedence. Null receiver/argument evaluation retains ordinary managed call order.

## Validation and review

All 30 focused/adjacent checks pass in 28.109s on isolated 82f3fc8 plus this slice.
Seven new roots use 4 x 160 character/bound pairs plus 3 x 100 range/fault pairs;
940 x 3 repeats x O0/O2 = 5,640 new native/.NET comparisons. Include empty strings,
NUL, lone surrogates, truncation to Char, all boundary positions, negative counts,
Int32 extrema, count zero, dynamically allocated strings and null/throwing argument
precedence. Hash complete exception messages and parameter names. String-needle and
comparison overload negatives remain precise missing-method diagnostics. Existing
forward-search controls pass unchanged. ASan/UBSan, forced collection, exact roots and
borrows, exact/one-short frames and the 2KiB native frame cap remain enabled.

Review checks the empty shortcut against the pinned source, unsigned validation order,
bounded arithmetic and loop endpoints, no culture/code-point normalization, unchanged
forward search and no unsupported semantics hidden by game edits. CIL106/object IR63/
interface20/native79, grants, runtime and production quotas remain unchanged. Existing
CandidateHashAlgorithmOwner.State fixture warning is unrelated and unchanged.

## Frozen audit

Six frozen input hashes match inputs-verified.json. Core
5732aaceacfa26c481205a4a506026693bd6ade81bc8e861f3373d748fa27c19; manifest
2358cdd05fd3d4729066edc985d5b596703fc2aaad81a49e41a4806ba198aabf.
Unchanged diagnostic allowances now reach CIL3010 at ForcedMovementOperation.OneOf,
method0x06000cb5 IL0035/member0x2b0000cf: options.Contains(selected,StringComparer.Ordinal).
This is the explicit-comparer Enumerable.Contains overload. Engine work 3,304,545
units/17,019ms; StateLookup validates 15 methods/11,730 units/437ms. Both emission
fields are null; no engine C++ emission, compilation or native execution.

Evidence: artifacts/alchemy-character-last-index, -refresh and -engine. Next work must
preserve explicit-comparer Contains semantics (including null-comparer behavior and
array/list span snapshots), and the separately reduced non-sealed duplicate-text gap
from AC8.75 still stands. Full closure admission and saved-game replay remain unfinished.
