# ADR AC0094: Predicate list search

Accepted 2026-10-07 for AC3.13.

Adapt all three pinned List<T>.FindIndex overloads with ordinary managed bodies.
The short overloads capture current size; the range overload validates startIndex,
then count, then match using pinned parameter names/messages. Capture endIndex
once, read the current backing array on every iteration, and stop at the first
match. Predicate mutation does not use enumerator version checks or List indexer
range checks. Clearing, growth, replacement and removal retain their pinned
observable behavior. No compiler/native profiles, capabilities or quotas change.

Five probes pass all 10 checks and 2,148 repeated native/.NET comparisons at O0/O2
in 29.264 seconds. Explicit grids cover empty/nonempty lists, negative and extreme
indices/counts, validation order with null match, first/late/absent matches,
callback faults, growth, clear, removal, insertion, assignment and capacity
replacement. References and reference-bearing wide values cover managed copy and
root behavior, including null/default slots after clearing and IndexOutOfRange
when a callback replaces storage with an empty array. Every comparison retains
forced collection, ASan/UBSan, exact allocation/frame cleanup, 2,048-byte frame
warnings and 30-second subprocess caps.

Review checked validation subtraction/short-circuit order, captured end bounds,
live array access, delegate copies/roots and exception precedence. All six frozen
application/probe assembly hashes verify. Production engine/state probes still
hit the unchanged type bound; larger diagnostic-only reachability is separate.
No full engine or saved-game replay execution is claimed.

Evidence: artifacts/alchemy-list-find-index/ac3-13a-*, engine-and-state.json and
core/library-build.json.
Core: 38701367e51097b440925a9f76e6b7e16e68d72f87b10c8426aeacbfe73fef59.
Manifest: 30cfde74468993f4ed5c5ba184d4648b009407d3665841b0185acdd079e3060b.
Fixture: bc909628834efccfb21f87049e550590f6d70e0c341a79fc95dd3e3045a0dc94.
