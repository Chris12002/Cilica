# ADR AC0192: Diagnostic type-snapshot allowance

Recorded 2026-10-08 for AC8.61.

AC6.21 exhausts exactly 2048 cached snapshots (807 nominal declarations and 1321
closed generic snapshots; categories overlap). Its explicit roots include 184
values, 1453 references and 161 vectors, with 4312/6144 field slots. Reuse the
archived AC6.21 CIL83/IR54 compiler and core, excluding concurrent UInt32 work.
Change only ObjectModelLimits.MaximumTypes from 2048 to 3072 in the diagnostic
copy. Production remains 256; all other limits and semantics are unchanged.

The next missing overload is Guid.TryParse(String, out Guid), called by frozen
TargetOptions.ParsePlayer token 0x06000fbd IL_0003, member 0x0a000720, requested
from TargetOptions.Parse token 0x06000fbf IL_0023. The path reaches movement and
physical damage resolution. Engine discovery charges 1117323 units/4288 ms;
StateLookup discovers 15 methods/11613 units/447 ms. No engine code is emitted
or executed. Evidence: artifacts/alchemy-type-budget-3072, including command,
copied source hashes, audit and repeatable script. Review verifies the exact
one-limit source diff. No tests are needed for this documentation-only slice.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.
