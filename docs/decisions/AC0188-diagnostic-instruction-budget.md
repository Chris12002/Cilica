# ADR AC0188: Diagnostic instruction-budget audit

Recorded 2026-10-08 for AC8.59.

The AC5a.9 frozen-engine probe exhausts its diagnostic instruction allowance.
Counter-only instrumentation reports 131071/131072 accumulated instructions;
the next decoder has allowance 1 and stops at IL offset 1. Partial discovery has
5147 declarations, 5096 bodies, 324841 IL bytes, 10035 parameters and 5994 locals.
The next declaration is engine token 0x060011ea, requested by ContinueBorrowedShield
(token 0x06001071) at IL_048e. The counter run uses 1049093 metadata units/5073 ms.

Copy AC5a.9's archived CIL v82/IR v53 diagnostic sources, core and manifest. Raise
only the copied instruction allowance from 131072 to 196608. This deliberately
isolates the budget experiment from concurrent Boolean-vector compiler work.
Production MaximumInstructions remains 65536. No other limit or semantics changes.

The next measured stop is missing String.ToLowerInvariant at engine token
0x06001563 IL_0068, the closure within ContinueConstructionContribution requested
by token 0x06001075 IL_054f. Discovery uses 1052133 metadata units/4239 ms.
StateLookup still discovers 15 methods, 11584 units/463 ms. No engine code is emitted
or executed. Evidence and reproducible scripts are in artifacts/
alchemy-instruction-budget-audit and alchemy-instruction-budget-196608.

Review checked source isolation, counters, unchanged frozen inputs and production
limits. No tests are added for this documentation-only slice. Local main 48ecf14
remains an ancestor. Full engine, unchanged fingerprint and saved-game replay
remain unfinished; the next semantic gap is invariant string lowercasing.
