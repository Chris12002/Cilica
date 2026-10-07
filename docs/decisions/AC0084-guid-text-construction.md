# ADR AC0084: GUID text construction

Accepted 2026-10-07 for AC5a.3.

Guid(String) delegates to an owned managed parser adapted from pinned Guid.cs.
It preserves D/N/B/P/X dispatch, edge Unicode whitespace trimming, removal of
all whitespace inside X, and the pinned legacy +/0x forms. X short components
parse UInt32 then truncate; byte overflow produces FormatException, while UInt32
overflow produces the constructor-specific OverflowException. Error resources
and their precedence follow pinned Strings.resx. Static Parse/TryParse, byte/span
constructors and random generation remain unmapped.

The existing four-word representation is unchanged. A private four-Int32 result
buffer is copied into the receiver only after successful parsing. X may allocate
a Char vector and String to remove whitespace. Every allocation uses existing
rooting and quotas. String.IsNullOrWhiteSpace and this parser share the same BMP
Unicode White_Space predicate, excluding BOM, zero-width space and surrogates.
There are no new compiler operations, grants, profiles or resource bounds.

Initial admission checks rejected a private Boolean borrow, a borrow held below
a conditional delimiter across a join, and a compound scalar-array write emitting
ldelema. The adapter instead uses an Int32 status, computes the delimiter before
borrowing, and writes explicit array loads/stores. All existing guards remain.

Six probes add 6,972 repeated comparisons: round trips and full field equality,
repeated parsing, heap/array/nullable/box storage, whitespace, 840 character
mutations and 153 corpus/seed pairs. Invalid inputs cover null, malformed delimiters,
length, dashes, overflow at each width, trailing text and malformed UTF-16.
The advanced public constructor probe adds 294 comparisons. Native O0/O2 runs
retain ASan/UBSan, forced collection, exact cleanup, 2,048-byte frame warnings and
30-second subprocess caps. Existing GUID tests, all 65,536 UTF-16 whitespace units
and the public baseline also pass. There are 29 distinct selected passing checks
across ac5a3e-tests.log and ac5a3f-tests.log. The e run's sole failure was a stale
negative assertion forbidding every String instance method, superseded by AC7b.8;
it now tests a virtual instance method and keeps its initializer rejection.
No validator was relaxed. The focused three-check rerun passes in 12.669 seconds.

Review checked indexing before access, length arithmetic, UInt32 wrap/truncation,
legacy zero/prefix behavior, constructor commit order, exception precedence,
shared whitespace semantics and allocation rooting. Frozen inputs are unchanged.
Engine Step still reaches 256 snapshots at UnitRoster.WithViews IL_000d via
PrepareForfeit; StateLookup reaches 257 catalog identities. These are partial
closures, not emitted runtime layout counts. The public baseline is 39 controls
and 8 gaps; full saved-game replay remains unfinished.

Evidence: artifacts/alchemy-guid-text/ac5a3a-* through ac5a3f-*, engine-and-state.json.
Core: 9bfdf3df0ba61bf34af807529354a8a817fb8615b6c38ddec0fb5036630c55b4.
Manifest: a9183ae23c7ba6b65ecb922734eb0a267691766817be722b8381fb5662591a86.
