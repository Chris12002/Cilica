# ADR AC0089: List interface enumeration

Accepted 2026-10-07 for AC4.8, following AC8.25.

Implement the pinned List<T> generic interface factory with its two observable
branches: nonempty lists box their concrete Enumerator; empty lists return the
existing shared SZGenericArrayEnumerator<T> cache. The nongeneric factory forwards
to the generic factory. Empty list and empty generic array enumeration therefore
share identity and mutable cursor state, while each nonempty call returns an
independent box. No substitute class cursor or application-specific path is used.

The concrete struct's nongeneric Current now rejects index <= 0 with the pinned
English resource, then returns its cached element through ordinary boxing.
Int32/inline values get independent boxes; reference elements retain identity.
Existing MoveNext/Reset version checks, generic Current default behavior and
no-op Dispose remain unchanged. Empty interface cursor state/faults intentionally
differ from a boxed concrete empty enumerator, matching the pinned runtime.

This composition exposed an overbroad scalar-confinement trigger. A direct named
box-to-interface conversion was making unrelated returned Int32 boxes require an
escape proof. At both logical and scalar stages, interface conversion now excludes
scalar uncertainty only for validated nominal reference views other than Object,
or a directly identified named non-scalar BoxValue/BoxScalars source. Scalar boxes
in this profile have Object as their only class ancestor. Unknown Object sources,
scalar/enum origins, alias escapes and boxed class-dispatch guards keep their
existing checks. This is an origin/type proof, not invented primitive ancestry or
a general alias analysis. Reduced positive and unknown-object negative probes,
plus detached scalar-confinement regressions, pin the boundary.

Integer CIL v27, boxed value IR v9 and scalar boxed IR v13 record the refinement.
Native v58, interface plan v12, object IR v31, layout v22, grants and resource
limits are unchanged. Collection-constructor fallback, ToList, dictionary/view
interface factories and signature-changing variance remain separate work.

Nine new cursor probes, the advanced legacy Current probe and two reduced
scalar-composition probes pass 3,528 repeated native/.NET comparisons. Coverage
includes mixed interface entry paths, concrete boxed type identity, independent
boxes/value copies, nongeneric element boxing, exact faults, reset, disposal,
mutation before/after exhaustion, nonmutating capacity changes, references,
reference-bearing wide structs, shared empty state, default cursors, null and
finally paths. Six additional O0/O2 native checks prove exact live graphs:
nonempty cursors retain list/backing storage through end/disposal; cached Current
keeps a removed reference alive; empty cached cursors do not retain source lists.
Dropping nonempty cursor roots releases their graph. All use forced GC, ASan/UBSan,
2,048-byte native frame warnings, existing 30-second subprocess caps and cleanup.

All 68 distinct selected checks pass across a/c/d/e logs, including concrete list
and array enumeration, scalar confinement, boxed dispatch and the unchanged
39-control/8-gap public baseline. The a run exposed the scalar-origin guard; b
then found two authored-test issues: an unsupported value ldtoken in a type check
and an uncaught expected legacy Current fault in the .NET oracle. Type identity
now compares with a separately boxed concrete cursor, and the oracle catches the
fault. The d run's six graph checks had all graph counts correct but asserted zero
call depth while their own RootScope was active; e checks depth one inside and
zero after the scope, and passes. No runtime workaround or quota change was made.

Review checked pinned factory branches/cache sharing, element copy identity,
readonly cursor fields, mutation/fault order, retained payload owners, release of
cleared elements, origin proof soundness and unknown-source rejection. Frozen
Alchemy inputs remain unchanged; engine/state roots still hit the production
256-type bound. There is no full engine execution or saved-game replay yet.

Evidence: artifacts/alchemy-list-interface/ac4-8a-* through ac4-8e-*,
engine-and-state.json and core/library-build.json. Public fixture hash:
7aa6ba476b36be40af7049da7437e49ff2ba1e597d668c813386f50657104c53.
Core: 001f5b2ca63c27fa5d849b243015de62dc2f35c7cfe88c5a5841db913e2710c1.
Manifest: ceb421746dd53621eb5f940301bb385ce5ba9cf46059a232fd9d0c71a6b15312.
