# ADR AC0206: Continue diagnostic discovery beyond 8,192 locals

Recorded 2026-10-08 for AC8.64 after AC6.24.

The unchanged frozen EngineStep probe reached 8,194/8,192 aggregate locals and
14,176/16,384 parameters after Int32 Range support. Increase only the isolated probe's
local allowance to 12,288 to discover the next missing reachable semantics. Preserve
all other diagnostic limits: 3,072 types, 8,192 methods, 16,384 parameters, 6,144 fields,
196,608 instructions and 1,572,864 metadata work units. Production remains 4,096 locals;
no compiler, runtime, library, grant or profile source changes are made by this slice.

The resulting scan stops at CIL3010 for Enum.GetValues(Type), member 0x0a0003bb in the
frozen Alchemy.Engine assembly, used by SpellModeChoices.BondsAfter (0x06000403 IL_000a)
and reached through ForDefinition. Source inspection corroborates the immediate
ElementGender[] cast after Enum.GetValues(typeof(ElementGender)). Implementing exact
enum-array creation is separate work; this diagnostic allowance does not admit it.

Engine discovery charges 1,335,497 metadata units in 4,976ms. StateLookup still succeeds
with 15 translated methods and 11,616 units in 458ms. All six frozen assembly hashes
verify; inputs remain data and were not rebuilt. Review confirms the single additional
allowance change in the isolated diagnostic source, preserved production limits and
counter-only instrumentation. No full engine emission or native execution occurred.
The unchanged selected native Range regression evidence belongs to AC6.24; there is
no reason to rerun native tests for this diagnostic-only documentation update.

Evidence: artifacts/alchemy-locals12288-engine; prior stop in artifacts/alchemy-range-engine.
Source-equivalent Range snapshot at e232f8b; diagnostic source hashes and exact command
are retained in source-hashes.json and command.json. Core and manifest remain the
AC6.24 digests. CIL93/IR59/native79 unchanged. Local main 48ecf14 remains an ancestor.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.
