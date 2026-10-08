# ADR AC0196: Diagnostic parameter and method allowances

Recorded 2026-10-08 for AC8.62.

AC7b.23 measures 12290/12288 parameters and 7138/8192 locals at
SpellContributionSources.TryAdd token 0x060003f3 IL_010d. Copy that archived tested
compiler/core and change only IrLimits.MaximumParameters from 12288 to 16384.
This reaches 6144/6144 method declarations, 386832/1048576 IL bytes,
155957/196608 instructions and 397757 remaining metadata units, at
SpellContributionSources.TakeShieldCard token 0x060003f9 IL_009c. Work is
1175166 units/4669 ms; StateLookup is 15 methods/11616 units/460 ms.

Copy that experiment and change only MaximumMethods from 6144 to 8192. Discovery
now reaches the HashSet<T>.UnionWith sentinel through SpellResolutionHandler's
ResolveChain token 0x060010ae IL_06e7. Work is 1203387 units/4424 ms; StateLookup
remains 15 methods/11616 units/449 ms. Evidence:
artifacts/alchemy-parameter-budget-16384 and artifacts/alchemy-method-budget-8192,
including exact commands, source hashes and repeatable scripts.

Review verifies one IrModel.cs allowance change per experiment. Production remains
4096 parameters/512 methods; all other allowances and compiler semantics stay
unchanged. No engine code is emitted/executed. No tests added for documentation-only
work. Full engine, unchanged fingerprint and saved-game replay remain unfinished.
Main 48ecf14 remains an ancestor.
