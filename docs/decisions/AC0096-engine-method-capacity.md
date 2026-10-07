# ADR AC0096: Engine method-capacity diagnostic

Recorded 2026-10-07 for diagnostic-only AC8.27.

Use frozen probes/application assemblies and the reviewed owned core with the
committed eb0ef63 compiler source. No application code is executed or modified.
The existing isolated 1,024-type/262,144-metadata compiler copy passes the repaired
internal static-field access and stops at 512/512 methods, 34,113/1,048,576 IL
bytes and 12,778/65,536 instructions in ResolutionKernel.StepReady IL_0016. It
charged 73,620 metadata units; the live production compiler remains unchanged.

A second isolated source copy changes ProgramLimits.MaximumMethods from 512 to
1,024. Keep the existing diagnostic type/metadata limits and all other bounds.
It reaches VictoryEvaluators.Resolve IL_001a through Step/FinalizeAccepted after
91,112 metadata units, rejecting boxed enum formatting. The frozen IL uses a direct
VictoryEvaluatorId box and String.Format(string,object) for its missing-evaluator
message. This is a real dependency, even on an error path. StateLookup still
translates eight methods and charges 9,917 units. No native emission or execution
is attempted. Total engine method/type requirements are still unknown.

The sole additional method-limit change, input commands, base commit, exact
compiler-source hashes and diagnostics are archived. Review checked that all
mutations are under diagnostic-source, the actual working compiler still has
512 methods/256 types/65,536 metadata units, all six frozen inputs match their
pins, and both harnesses omit native emission. This does not establish sufficient
production quotas, general application compatibility or saved-game replay.

Evidence: artifacts/alchemy-static-access-engine-refresh and
artifacts/alchemy-method-capacity-audit, including run.py, audit.json,
source-hashes.json, work262144.json and victory-resolve-il.txt.
Next operation: default boxed enum formatting and its exact metadata/format rules.
