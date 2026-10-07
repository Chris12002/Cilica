# ADR AC0087: Diagnostic metadata-work audit

Accepted 2026-10-07 for AC8.24, diagnostic evidence only.

Extend the disposable compiler copy from 9fd8cdc to 1,024 types and 262,144 shared
metadata work units. Production limits are unchanged. Compiler-owned stack-frame
category counters record successful root-budget charges; they never inspect or
execute application objects. All input assemblies and the AC3.10 core manifest
remain frozen. C++ emission and native execution remain disabled. Builds are local
with cached packages and each subprocess is capped at 60 seconds; the category
reprobe is capped at 30 seconds. Counter overhead is not performance evidence.

EngineStep reaches a missing Enumerable.ToList declaration after 67,822 charged
work units, in AdvanceSlots IL_015a via PrepareForfeit/AdvanceAfterActionOpportunity.
The following instruction chain calls List<ActivationSlot>.FindIndex(Predicate<T>).
The ToList source is declared IReadOnlyList<ActivationSlot>; InitiativeSlots.For
constructs and returns a List. This is an observed declaration/call-chain gap,
not proof that earlier translated methods will pass every later stage.

Largest charge categories at this partial boundary: 14,208 method-input work,
8,981 library signature projection, 6,668 closed dispatch validation including
argument traversal, 5,170 method binding, 4,811 closed snapshots and 4,586 library
method matching. StateLookup still translates eight methods and charges 9,917
units, including 8,164 catalog snapshot work. Repeated closed declaration selection
accounts for only 308 engine units; changing its lookup alone would not remove
the observed work-budget deficit. Full engine closure counts remain unknown.

Review checked copied source hashes, bounded counters, unchanged frozen inputs,
exact manifest admission, no emission, and separation from production admission.
Evidence: artifacts/alchemy-metadata-work-audit/run.py, audit.json,
work262144.json, categories.json, advance-slots-il.txt, initiative-slots-il.txt
and build/probe logs. The category instrumentation only skips an intermediate
InspectMember stack frame; its total remains 67,822/9,917. Full replay is unfinished.

Next fill the ToList/FindIndex path and its required enumeration behavior, then
reprobe under the same diagnostic bounds before proposing production capacity.
