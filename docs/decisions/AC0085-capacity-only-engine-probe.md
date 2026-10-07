# ADR AC0085: Isolated capacity probe of frozen engine roots

Accepted 2026-10-07 for AC8.23, diagnostic evidence only.

The unchanged frozen roots repeatedly stop at the production 256-type limit.
Copy compiler/metadata source from e591c34 into ignored diagnostic artifacts and
change only ObjectModelLimits.MaximumTypes to 1,024 in that copy. Keep all other
budgets, input assemblies, owned-core hashes and semantic checks unchanged. The
harness explicitly disables C++ emission; it neither executes application code
in the compiler nor runs native code. Each local subprocess has a 60-second cap
(the final metadata reprobe uses 30 seconds). This is a bounded experiment, not
production capacity admission or evidence of full-engine compatibility.

Results from type1024-counts.json:

- EngineStep reaches GameState constructor token 0x06000108 IL_0270, where
  List<SpellSourceGrantState>.Sort(Comparison<T>) lacks a mapped implementation.
  The call path is Step -> PrepareForfeit -> GameState.With -> constructor.
  Three later constructor sites use the same overload for other state records.
  Full engine closure size is still unknown.
- StateLookup completes translation to eight methods: 36 value layouts,
  92 reference layouts, 166 interfaces, two reference arrays, no value arrays,
  delegates or static fields. These actual catalog counts explain why the
  production 256-type limit is insufficient. No emission or execution was done.

The first isolated build failed while trying to query online NuGet vulnerability
data. The same cached packages built with NuGetAudit disabled for this disposable
experiment; no repository setting or dependency changed. Builds and probes then
completed locally. Review checked that the source-copy change is exactly the type
limit, that native emission is removed, and that the harness consumes the existing
frozen inputs and reviewed core manifest. Production diagnostics remain unchanged.

Next implement the observed Comparison<T> sort overload with pinned callback order,
partial mutation and exception behavior. Separately design capacity admission using
measured catalog, metadata-work and native-size evidence; do not promote the
experimental 1,024 setting without that work. Full saved-game replay is unfinished.
Evidence: artifacts/alchemy-capacity-audit/run.py, audit.json, command.json,
type1024-counts.json, gamestate-il.json and local build/probe logs.
