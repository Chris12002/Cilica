# ADR AC0117: Internal exception declaration and constructor access

Recorded 2026-10-07 for AC8.29.

## Decision

After f1c0fc1, frozen discovery reaches EffectOperationRegistry.Require IL_000b
through ShieldResponseCapabilities.Allows, after 229,411 metadata units. Its
UnsupportedOperationException is an internal fieldless top-level class with an
internal string constructor. StateLookup remains eight methods/9,921 units.
No engine native emission/execution. Evidence:
artifacts/alchemy-enum-keys-{refresh,engine}, require-il.txt and
unsupported-operation-il.txt.

The integer profile can register internal top-level nongeneric fieldless exception
shells. Registration authenticates shape; explicit type uses (including catch
clauses) check the actual caller's assembly/scope after its binding exists.
Public and assembly-access constructors are admitted only when accessible to that
caller. Declaration and validated-constructor caches never cache caller access:
access checks run again for each call, including recursive forwarding validation.

Constructor behavior remains unchanged: only exact no-argument/string forwarding
bodies are admitted. Additional statements, message construction, fields,
initializers, nesting, generics and virtual overrides remain diagnostic. In
particular the real UnsupportedOperationException message-building constructor
still needs a later slice. Integer CIL v43 records admission; native v64/layout v24,
exception IR/payload, resource mapping, grants and production bounds are unchanged.

## Validation and review

Five new native probes provide 1,470 O0/O2 native/.NET comparisons for internal
messages/default text, derived/base catches, internal constructors on public
owners and preserved exception identity through rethrow. Four raw foreign-call
checks cover public/internal owners both directly and after a local constructor
populates metadata caches. A message-building fixture stays negative. Together
with nine existing private generic source-use boundaries, 18 focused checks pass.
All 44 selected exception translation/native/validation and public-baseline
regressions pass: 62 checks total. Forced GC, ASan/UBSan, 2,048-byte frame warnings
and 30-second subprocess limits remain enabled. Public baseline: 43 controls/four
gaps. Main 48ecf14 remains an ancestor; review has no outstanding finding.

Review checks shape/access separation, catch-clause staging, caller-specific
cache checks and unchanged constructor-body validation. Full engine compilation
and saved-game replay remain unfinished.

Evidence: artifacts/alchemy-internal-exceptions/tests-{b,c}.log and hashes.json.
Core: fbf06d7abd311dbc38fa5c8bb5939533165ef08bbd1c6581165dbddeb0ff1a6c.
Manifest: 8958e38b2ae113d297f34b3549078af1baefea2d078aaba4c78d7ac6d5e67214.
Fixture: 38aee513f992daa70fdf1f6a6ae031174c6db3963cacf946acda54cb710fc848.
