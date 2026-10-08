# Batch 019: polymorphic dictionary text

Completed the owned reduction of the frozen Alchemy netstandard2.1 engine blocker.
Non-sealed ordinary key/value classes now retain runtime ToString dispatch and
actual default class names. Sealed paths and unsupported provider/special-receiver
boundaries remain explicit. No external candidate was downloaded or executed.

- **120 distinct checks pass**, including **2,598 new repeated native/.NET
  comparisons**, two native quota/ownership tests, and the existing enum-text suite.
- The frozen engine scan advances to scalar boxed formatting in
  BoardObjectState.WithHitPoints (0x060000cd, IL0028). StateLookup remains 15 methods;
  neither audit emitted C++ or ran the engine.
- Unity reference code informed dynamic-name, virtual-slot and lifetime tests.
  Its assembly pins differ from the frozen inputs; no implementation was copied.
- No disposable external checkout was created or deleted. Owned source export,
  exact managed images and original receipts remain at
  `/tmp/cilica-polymorphic-text-20261008`; concise evidence is retained here.

[Decision and limits](../decisions/compatibility-021-polymorphic-key-text.md) ·
[Reference locations/hashes](batch-019-reference.json) ·
[Evidence manifest](batch-019-evidence.json) ·
[Focused checks](batch-019/tests-budget.log) ·
[Adjacent checks](batch-019/tests-adjacent.log) ·
[Enum/core checks](batch-019/tests-final.log) ·
[Frozen audit](batch-019/frozen-engine-work12288.json)

Next: reduce the new scalar-formatting failure from its pinned IL, preserving
literal/format/provider proof. Existing screened candidate exclusions and the exact
Albireo Decode proposal remain in batches014–018; Albireo still needs user approval.
No full suite, full Alchemy replay, new platform or milestone-7 completion is claimed.
