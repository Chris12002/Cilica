# ADR AC0194: Throwing invariant Int64 parsing

Recorded 2026-10-08 for AC5a.11, following AC5a.10.

## Design

Frozen ShieldAcquisitionReceipt.Read calls Int64.Parse(String,IFormatProvider) at
IL_0036 with a directly produced InvariantCulture. Admit that exact two-argument
signature under the existing exact-signature InvariantIntegerParsing capability.
The existing selected primitive identity and throwing-mode evidence distinguish
this implicit Integer grammar from Int32's three-argument Parse and four-argument
TryParse signatures. CIL and both independent IR validators authenticate parameter
count, owner, return width and directly proven provider. Null/unproven providers,
other overloads and ambient culture remain diagnostic. One exact grant is added.

Use bounded UInt64 magnitude accumulation with different positive/negative limits,
then unchecked signed conversion/negation to preserve Int64.MinValue. The grammar
matches the established Int32 parser while keeping arithmetic widths explicit.
After overflow, continue consuming digits before validating suffixes so malformed
text wins over overflow. Preserve ASCII leading/trailing whitespace and trailing
NUL rules. Null throws ArgumentNullException(s); format errors retain the entire
UTF-16 input and overflow retains the exact Int64 resource. Existing Int64 None
TryParse remains unchanged. Pinned Int64.cs confirms the implicit Integer style.

## Validation and review

All 40 selected Int32/Int64 throwing/nonthrowing checks pass in 63.022 seconds.
Four new probes provide 2202 repeated O0/O2 native/.NET comparisons, covering both
32-bit halves, signed boundaries, leading zeros, whitespace/NULs, malformed suffix
precedence, null and exact exception text. A separate managed oracle checks 262754
cases, including every UTF-16 prefix/suffix around ordinary/overflowing numbers.
New declaration/operand mutations and exact grant denial are rejected at both IR
stages. Native checks retain ASan/UBSan, forced collection, exact cleanup, bounded
frames and timeouts. Existing grant tests now select the exact TryParse member.

Evidence: artifacts/alchemy-int64-parse-throwing; fixed compiler base 1759646 plus
owned edits (CIL87/IR57/native77), excluding concurrent checked-index work. Initial
sandboxed restore stalled; a bounded, single-node local build using cached packages
passed. No package/service installation or hosted work. Review checked arithmetic
bounds, failure ordering, exact provider proof, forged identities/widths, preservation
of prior TryParse and Int32 Parse signatures, and generated native fault cleanup.
CIL87/IR57 reserve the next versions after separately developed CIL86/IR56; no new
native operation or production/diagnostic limit change is part of this slice.

Core: 07ff5ee47a3e25c535f5e32d27bb547f1f6ea593f874bce463f92181722acc37.
Manifest: 88d03d352a07c7861dcc54a1d7a28db6be5539f36f3180dcd23afd4630bf2bc4.
Frozen discovery advances to constrained ElementGender.ToString at IL_0063 in
SpellResolutionHandler/<>c__DisplayClass10_2.<ContinueConstructionContribution>b__3,
token 0x06001563. It charges 1171977 metadata units/4756 ms. StateLookup translates
15 methods/11616 units/465 ms. Evidence: artifacts/alchemy-int64-parse-throwing-
{refresh,engine}. No engine emission/execution. Full engine, unchanged fingerprint
and saved-game replay remain unfinished. Main 48ecf14 remains an ancestor.

Integration on committed checked-index base f494f5b also passes all 69 selected
parser and checked-index checks in 79.180 seconds (CIL87/IR57/native78). Evidence:
artifacts/alchemy-int64-parse-throwing-integration. The 40 parser checks overlap the
isolated run; this is 69 distinct checks, not 109. Shared source matches the tested
integration snapshot. The native78 change belongs to the separate checked-index
slice; no native emitter edits are included here.
