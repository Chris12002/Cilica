# ADR AC0156: Shared GUID constructor and static parsing

Recorded 2026-10-08 for AC5a.5.

## Evidence and decision

Committed AC3.27 (63f2515) clears the source-plus-ordinal set constructor in the
unchanged frozen engine. Discovery next reaches Guid.Parse(String) at
HandLimitHandler.Resolve IL_0141, after 820,572 metadata units (3,425 ms).
StateLookup remains 15 methods/11,511 units. Evidence is in
artifacts/alchemy-ordinal-set-source-{refresh,engine}. This is discovery only.

The existing reduced GuidProbe.Parse diagnostic passes before implementation
(red.log). Add the exact managed static entry point and share the constructor's
existing parser with an explicit constructor flag. The pinned Guid.cs uses All
for the constructor and AllButOverflow for Parse. Accordingly null uses parameter
g for the constructor and input for Parse; UInt32 overflow stays OverflowException
for construction and becomes FormatException with Unrecognized Guid format for
Parse. Byte overflow remains FormatException in both. The flag changes only those
fault policies. D/N/B/P/X dispatch, whitespace, legacy prefixes and fault precedence
remain shared. A private array constructor copies four words after complete parsing.

No compiler, grant, native boundary, profile or production/diagnostic limit changes.
TryParse, ParseExact, span/byte APIs and random generation remain unmapped.

## Validation and review

Six static probes reuse the constructor's exact corpus and add 6,972 repeated
native/.NET comparisons. The promoted existing Parse control adds 294 (7,266 total).
Cases cover full field equality, repeated parsing, heap/array/nullable/boxed storage,
22 whitespace units, 840 mutations and 153 malformed/corpus/seed pairs, including
UInt32 overflow in every component, byte overflow, null and exception messages.
Both optimized and unoptimized native builds retain ASan/UBSan, forced collection,
exact cleanup, 2,048-byte frame checks and 30-second process caps. All 24 focused
static/constructor/Guid library checks pass in 48.098 seconds. Evidence:
artifacts/alchemy-guid-static-parse/{build,red,tests}.log.

Review checked the pinned throw policy, null names, early fault ordering, unchanged
constructor results, result-buffer rooting/copying and exact managed signature.
Main 48ecf14fd7a2c72e63281b50a6a61ea1c090c187 remains an ancestor. Full engine,
unchanged fingerprint execution and saved-game replay remain unverified.

All four public baseline checks pass in 68.755 seconds: 46 native controls and one
expected diagnostic gap. Total validation is 28 distinct checks.
