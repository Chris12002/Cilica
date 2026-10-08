# ADR AC0182: Copied value arguments on abstract class calls

Recorded 2026-10-08 for AC8.56.

The frozen engine's internal abstract GameOccurrence.Permits(PlayerId) requires a
copied value parameter. Admit values only in logical abstract parameter signatures;
reuse ValueLayoutPlanner and ExpandSignatureType in exception/delegate lowering.
Concrete implementations and declarations therefore use the same field order and
scalar/reference ABI. Keep original declarations when no value needs expansion.
Expanded declarations and bodies share the unchanged aggregate parameter budget.
Scalar validators independently reject remaining values or mismatched leaves.

Scalar/reference/void results are supported. Abstract value returns and borrowed
parameters remain actionable diagnostics. No abstract executable body, new native
ABI, special application logic or grant is introduced. Empty values expand to zero
leaves while receiver checks and argument evaluation remain intact.

Six probes cover nested Int32/Int64/reference structs, local copy mutation versus
shared reference identity, allocation during overrides, public and internal generic
owners, void/reference/wide results, null-only calls, empty structs and argument
fault ordering. They provide 1,764 repeated comparisons against .NET at O0/O2 with
ASan/UBSan, forced collection and exact cleanup. Eight new checks pass. Existing
abstract/internal/virtual-value tests pass after fixing unnecessary reconstruction
of unchanged scalar declarations (61 final checks including the eight new ones).
Four public-baseline checks passed in the earlier regression run: 65 distinct checks.
That earlier run had eight declaration-identity assertion/mutation failures; all
were rerun successfully after the fix. No full-suite or platform claim is made.

Review checked layout parity, nested reference rooting, bounded parameter growth,
bodyless identity, empty-value calls and unchanged abstract return/borrow guards.
CIL v78 and object IR v51 record the contract. Native v74, grants and limits remain
unchanged. Local main 48ecf14 is an ancestor. Concurrent candidate String.Split
work was committed separately as f479fe2 and is excluded from this slice.

Evidence: artifacts/alchemy-abstract-value-arguments{,-engine}. Compiler-only frozen
probe reused AC5a.8's archived core. Engine discovery now reaches the message/inner
constructor of EffectiveSpellCompilationException at DistanceFromFeet IL_008a:
1,019,328 metadata units in 4,126 ms. StateLookup remains 15 methods/11,547 units
(458 ms). Full engine, unchanged fingerprint and saved-game replay are unfinished.
