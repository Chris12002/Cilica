# ADR AC0210: Measured initial interface-planning audit allowances

Recorded 2026-10-08 for AC8.66 after callback slice AC8.65 (7b7e0bf).

The callback fix exposes the next diagnostic-only discovery quota. Instrumenting the
unchanged 196,608-instruction allowance shows 196,602 already charged across 7,617
method bodies; ComparisonSort<Int64>.Swap decodes the six remaining instructions and
still has 29 IL bytes unread. This is aggregate exhaustion, not a giant method or an
unsupported operation. The counter-only reproduction takes 5,027ms; StateLookup
passes with 15 methods in 457ms. Evidence: artifacts/alchemy-instruction-audit.

Raise only the copied diagnostic compiler's aggregate instruction allowance to
262,144. The initial pending queue drains with 7,621 bodies, then the catalog finishes
and interface planning exhausts its existing 1,572,864 metadata budget. The aggregate
instrumentation reports 1,572,982 units across independent root ledgers, including
setup work; this is not a claim that one ledger exceeded its limit. The stop is in
BoardObjectState constructor member binding during interface planning. Engine scan
6,963ms; StateLookup 15 methods/11,616 aggregate units/467ms. Evidence:
artifacts/alchemy-instruction-262144.

Raise only the diagnostic metadata ceiling/defaults to 2,097,152 and repeat once.
The next stop is semantic: narrow-vector interface planning admits metadata-only
recipes, so nongeneric GetEnumerator is rejected. The path runs through
ContributeHandCardsModifier.Read -> Enumerable.Count -> OfTypeIterator.Count.
The scan charges 1,928,820 aggregate metadata units in 7,241ms; StateLookup still
translates 15 methods/11,616 units in 457ms. Evidence: artifacts/alchemy-metadata-2097152.
This reveals the next implementation slice; it does not complete interface planning
or the later dispatch fixed point, IR validation, native emission or game execution.

All probes reuse the exact callback/Array.Clear snapshot and owned core/manifest from
AC8.65. No frozen assembly is rebuilt or executed; all six input hashes and all 94
pinned upstream hashes already verify for that snapshot. Core
76679328e233017b36a27078e2a7d4d6e5e70ee9b2784be99aa169d906f16e6c;
manifest 87ec152387c1d90a4e9b68e392b3884825a5989f09cc3b31c7c3698b5e64f903.

Review confirms the allowance edits exist only in ignored diagnostic-source copies.
Production instructions, metadata member defaults and metadata ceiling remain 65,536;
production types/methods/parameters/locals/fields remain 256/512/4096/4096/4096.
Other diagnostic allowances stay types3072/methods8192/parameters16384/locals12288/
fields6144; per-method body/instruction limits are unchanged. Probes retain bounded
60-second subprocesses. No profile, runtime, core-library or game-source change is
made. No tests mirror this documentation-only update. Full engine, unchanged
fingerprints and saved-game replay remain unfinished.
