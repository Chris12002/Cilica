# ADR AC0190: Diagnostic local-budget audit

Recorded 2026-10-08 for AC8.60.

AC7b.22 records exactly 6145/6144 aggregate locals and 10345/12288 parameters
at ContinueShieldContribution token 0x06001085 IL_0597. Copy that archived diagnostic
compiler and reuse its isolated core/manifest and unchanged frozen assemblies.
Change only diagnostic IrLimits.MaximumLocals from 6144 to 8192. Production remains
4096; compiler/native semantics, profiles and all other diagnostic limits remain
unchanged. This is a bounded discovery experiment, not production closure admission.

The next stop is missing Enumerable.Skip, reached by ContinueAcquiredShieldChoice
token 0x0600106e IL_05c8 (method specification 0x2b0000ec), requested from
ContinueShieldWave token 0x06001082 IL_0049. Discovery charges 1075533 metadata units
in 4336 ms. StateLookup discovers 15 methods/11590 units/467 ms. No engine code is
emitted or executed. Evidence: artifacts/alchemy-local-budget-8192, including copied
source hashes, exact command, audit and repeatable script. This excludes the other
chat's unfinished dictionary changes. Full engine, fingerprint and replay remain
unfinished. Review checked the exact one-limit diff against AC7b.22's archived copy;
no tests are added for documentation-only work. Main 48ecf14 remains an ancestor.
