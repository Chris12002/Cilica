# ADR AC0185: Diagnostic field-budget audit

Recorded 2026-10-08 for AC8.58.

AC3.32 reaches the frozen engine's field metadata cap. Instrument only the copied
TypeInputReader and ValueProgramDiscovery to report requested fields, remaining
allowance and cached snapshot counts. At failure there are exactly 4096/4096 cached
field declarations, 1914 snapshots, 184 value roots, 1304 reference roots and 157
vector roots. The next closed type requires eight fields, with zero allowance.
Baseline remains 1,038,837 metadata units (4,309 ms in the counter-only run).

Raise only ObjectModelLimits.MaximumFields in the diagnostic source copy to 6144.
Do not change production 4096, input assemblies, core manifest, semantic admission,
profiles or native limits. The next measured stop is String.IndexOf(Char) in
TargetOptions.ParseCell IL_0003, after 1,041,357 metadata units in 4,429 ms. StateLookup
remains 15 methods/11,566 units (473 ms). No engine code is emitted or executed.

Evidence: artifacts/alchemy-field-budget-audit and alchemy-field-budget-6144 contain
reproducible scripts, exact copied source hashes, counters and reports. Both use
AC3.32's archived core/manifest and unchanged frozen inputs. Review confirms the
only field-limit edit is inside ignored diagnostic artifacts; repository production
limits and semantics are unchanged. No tests are added for this evidence-only slice.
Local main 48ecf14 remains an ancestor. Full engine, unchanged fingerprint and
saved-game replay are unfinished; the next semantic slice is character searching.
