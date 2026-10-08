# ADR AC0203: Named nullable payload text

Recorded 2026-10-08 for AC1a.2 after AC3.35.

Frozen discovery reaches the owned Nullable<T>.ToString stub through composite
formatting. Implement the pinned presence/payload call for already admitted named
value overrides. Keep the presence field readonly but make the payload mutable: pinned
Nullable explicitly permits ToString to mutate it. Empty values return the empty string;
present values return exactly their override result, including null, and propagate
exceptions after preserving any receiver mutations. No copy/boxing substitution.
Equality/hash remain explicit gaps. No compiler/profile/grant/limit changes.

Initial broad probes exposed separate existing compiler boundaries: primitive constrained
fallbacks, narrow field addresses and nullable composite-box proof. Retain these as five
precise negative controls. A named fixture initially held a managed address across a
conditional initializer; evaluate that fixture's text before construction. A typed string
format fixture likewise uses a separate string-parameter helper to respect the existing
same-block candidate scan. No application source is changed. The original nullable-box
composite fixture remains rejected: discovery currently selects the wrapper as a formatting
receiver although actual nullable boxing yields the underlying payload or null. This
separate compiler issue must be addressed before full engine execution is claimed.

Validation: 27/29 selected nullable checks pass in 67.414s; only the two typed-string
fixture cases fail. After the fixture repair all 14 text checks pass in 33.085s. Thus 29
distinct checks pass, including seven new probes x 49 pairs x 3 repeats x O0/O2 = 2058
new native/.NET comparisons. They cover named present/empty text, mutable/ref-containing
payloads, null output, mutations before throw, generic ref calls, copies, heap fields,
array elements and separately admitted typed-string composition. Existing nullable
storage, boxing, casts, operators and quota controls pass. Native validation uses forced
GC, ASan/UBSan, frame exhaustion, exact cleanup and bounded subprocesses. Review checked
mutable field semantics, same receiver location, exception writeback and reference roots.

All 92 pinned source hashes and six frozen inputs verify. Snapshot 0a6fdd7, CIL92/IR59/
native79. The separately committed earlier-contract helper fix also passes five checks
in 7.027s (AC0202 follow-up); it does not change these compiler results. Local main 48ecf14
remains an ancestor. Evidence: artifacts/alchemy-nullable-text{,-refresh,-engine}.
Frozen discovery now stops at Enumerable.Range from ConstructionContributions.EligiblePlayers
0x06000318 IL_0101. Engine charges 1274261 metadata units/4932ms; StateLookup translates 15
methods/11616 units/501ms. Existing diagnostic allowances unchanged; no engine emission.
Core b7ee24fbb53e5ea23d073ce0f579133cd09889210ad91fef4bee205d0967ad14.
Manifest 28939f088c04896a81dc87c991412619d311b0b74889ea052ffb096de1e96456.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.
