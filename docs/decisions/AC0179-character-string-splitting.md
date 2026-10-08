# ADR AC0179: Character string splitting

Recorded 2026-10-08 for AC7b.20.

Add exactly String.Split(Char,StringSplitOptions) and the flags mapping for the
unchanged damage-replacement answer reader. Adapt pinned String.Manipulation.cs
and archive StringSplitOptions.cs from the same runtime commit. None, RemoveEmptyEntries,
TrimEntries and their combination work; netstandard exposes only the first two
named values, so trim bits are tested numerically against .NET 10.0.11. Invalid
bits throw the exact ArgumentException with parameter options. This does not
enable general Flags formatting/reflection.

Two managed scans over immutable UTF-16 replace private stack/pool separator
storage. Allocate an exact result, reuse Substring for rooted copies and the
existing UnicodeWhitespace table for trimming. Preserve input identity unless
trimming changes it. No-separator empty removal returns the canonical empty array;
all-removed splits containing separators get a fresh empty array, matching pinned
Array.Resize. No user callbacks or mutable input occur between scans. Private
scratch allocation counts are not a compatibility contract. Public Trim and
count/array/string-separator split overloads remain unmapped.

Six probes supply 3,696 repeated O0/O2 native/.NET comparisons: empty/null/adjacent/
trailing/no separators, invalid flags, full UTF-16 and fault text, result identities,
dynamic strings, unpaired surrogates, null characters, Unicode whitespace and
excluded boundaries. All 12 focused and 17 adjacent/negative/public-baseline
checks pass (29 distinct checks), with forced GC, ASan/UBSan and exact frame/cleanup
checks. All 69 pinned originals and six frozen inputs were hash-verified.

Review checked validation/null order, empty identity, bounded indexing, trim/remove
combinations and rooted copies. Profiles, grants and all limits are unchanged.
Main 48ecf14 remains an ancestor. Evidence: artifacts/alchemy-string-split{,-refresh,-engine}.

Frozen discovery next reaches Int64.TryParse(String,NumberStyles,IFormatProvider,
Int64&) with None and InvariantCulture at RecordAnswers IL_006e: 990,636 metadata
units in 4,077 ms. StateLookup remains 15 methods (11,547 units/447 ms with the
added explicit type mapping). Full engine/fingerprint/replay execution is unfinished.
