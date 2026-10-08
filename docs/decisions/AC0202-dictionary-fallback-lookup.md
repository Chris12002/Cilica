# ADR AC0202: IReadOnlyDictionary fallback lookup

Recorded 2026-10-08 for AC3.35 after AC3.34.

ForcedMovementPlanner.Plan IL_042b calls the three-argument GetValueOrDefault<UnitId,int>.
Adapt both overloads from pinned CollectionExtensions.cs: the two-argument overload
supplies default(TValue), the other checks dictionary for null then calls TryGetValue
exactly once. Return its output only on success; on false, return the supplied fallback
even if a custom dictionary wrote a nondefault output. No concrete-dictionary shortcut,
indexer, Count, ContainsKey, enumeration, intrinsic or compiler change. Argument evaluation,
virtual dispatch, user exceptions and managed out-value rooting retain ordinary IL behavior.
Other CollectionExtensions members remain unimplemented.

Validation: 18 checks pass in 32.515s. Eight probes x 49 pairs x 3 repetitions x O0/O2 yield
2352 native/.NET comparisons covering int/wide values, reference-bearing tuples, null
stored values, explicit fallback identity, string/null keys, null dictionaries and custom
callbacks. A custom IReadOnlyDictionary throws if any operation other than TryGetValue
is used, allocates an output even on false, and can throw during lookup. Two controls
retain diagnostics for extension TryAdd/Remove. ASan/UBSan, forced GC, exact cleanup,
frame exhaustion and bounded native subprocesses remain enabled. Review checked null
validation/argument order, generic default copies, callback fidelity and reference roots.
All 91 upstream hashes and six frozen input hashes verify. Baseline d0b2589 with CIL92/
IR59/native79; no profile, production/diagnostic budget or grant changes. Local main
48ecf14 remains an ancestor. No game changes or frozen input rebuilding.

Evidence: artifacts/alchemy-dictionary-default{,-refresh,-engine}. Frozen engine now
reaches Nullable<T>.ToString's explicit unsupported marker through SettleIntermediateAction
IL_0072 and the owned CompositeFormatting path. Engine charges 1274044 metadata units/
4941ms; StateLookup translates 15 methods/11616 units/462ms. No engine emission/execution.
Core 3bf763ec8fd8e3549aca326a005a94c69ed54f5ed7f969d5ed629c5d19c5f12f.
Manifest 1e89b79d63792fd402b6584cc43d52e94f4ccd74e9df0008cfe0e53b1fb20a8c.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.
