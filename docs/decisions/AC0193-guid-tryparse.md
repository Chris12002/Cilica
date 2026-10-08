# ADR AC0193: Nonthrowing string GUID parsing

Recorded 2026-10-08 for AC5a.10, following AC8.61.

## Design

Frozen TargetOptions.ParsePlayer calls Guid.TryParse(String, out Guid) at IL_0003.
Share existing D/N/B/P/X validation using a private status result. Null, malformed
input and UInt32/octet overflow return false and default(Guid); no FormatException
is constructed or caught. Successful parsing copies four words after full
validation. Pinned legacy +/0x spellings and Unicode whitespace remain shared.
The constructor retains g null naming and UInt32 OverflowException; Parse retains
input null naming and FormatException on overflow. Private -1 status distinguishes
UInt32 overflow; other status values preserve existing resource selection and
validation precedence. Bounded result/whitespace allocations remain possible.

Keep output borrows inside basic blocks: choose the delimiter failure kind before
storing it, and branch before writing the Guid result. This uses current borrowing
rules rather than broadening compiler admission. No intrinsic, compiler/profile,
native/runtime or production-limit change. Provider/span parsing, ParseExact and
random generation remain outside this slice.

## Validation and review

Six new probes cover exact value equality, success/failure output replacement,
repeated recovery, field/array output locations across GC, 22 whitespace units,
840 mutations and 153 corpus/seed pairs. They add 6972 repeated native/.NET
comparisons. All 32 selected TryParse/Parse/constructor/Guid checks pass in
179.151 seconds, O0/O2, ASan/UBSan, forced GC, exact cleanup and short-frame failure.
Compiler snapshot 30bc8d4 (CIL85/IR55/layout28/native77) plus owned GUID changes
excludes concurrent checked-index work. Evidence: artifacts/alchemy-guid-tryparse.
All 84 retained upstream hashes and six frozen input hashes were verified.
Review checked status propagation, partial-result clearing, constructor/static
exception policy, legacy prefixes, unsigned packing, borrowed output rooting,
and unchanged fixed compiler limits. Main 48ecf14 remains an ancestor.

Core hash: 9ddf0cb84603aa200c660c07eac5ff843dd1c63d900b21f2b95dfd4d558c397a.
Manifest: 36e20b62b48f7c5925ee22d1dcaec8f6e1d91e86010f26853f3b6431eb4a76d4.
With the existing diagnostic allowances, engine discovery advances to missing
Int64.Parse(String,IFormatProvider), with proven InvariantCulture, in
ShieldAcquisitionReceipt.Read token 0x06000eae IL_0036 (member 0x0a0005ad),
requested from ShieldModes.Run token 0x06001230 IL_090d. It charges 1124122 metadata
units/4673 ms. StateLookup translates 15 methods/11616 units/466 ms. Evidence:
artifacts/alchemy-guid-tryparse-{refresh,engine}. No engine emission/execution.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.
