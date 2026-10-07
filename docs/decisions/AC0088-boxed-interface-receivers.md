# ADR AC0088: Boxed named-value interface receivers

Accepted 2026-10-07 for AC8.25.

General list materialization needs a mutable List<T>.Enumerator called through
IEnumerator<T>. Adapt the existing named-value receiver lowering rather than
replace the value with a class cursor, which would change identity, copying and
GetType behavior. This slice supplies the compiler prerequisite; owned collection
interface factories remain separate library work.

Closed interface discovery now includes compatible named non-scalar value
layouts. Each target must be a declared nongeneric instance body on that exact
value owner. Both ordinary public and explicit private implementations are
admitted, including closed generic owners and inline parameters/results.
Inherited Object bodies, scalar/enum boxed dispatch, generic interface methods on
boxes and signature-changing variance adapters remain unsupported. Existing
scalar confinement and boxed class-dispatch guards remain active.

Detached method facts retain the logical ValueInstance binding while scalar
lowering expands the receiver into checked managed locations. A separate retained
value catalog authenticates the lowered packet without confusing the existing
array-helper logical/scalar distinction. Validators require complete receiver
maps, exact layout/parameter order, writable receiver slots, original binding
provenance, matching executable bodies and shared catalogs. Catalog equality now
also compares scalar/enum classification and enum constants.

Native maps select an exact box descriptor and payload width with the method
pointer. The caller sizes its ordinary budgeted heap frame for the largest target
packet, copies declaration arguments, then adapts that same mutable packet before
calling. A checked helper authenticates the box, shifts the argument/result
suffix, and substitutes payload locations. Zero-field values simply remove the
receiver slot. Incoming const packets are never mutated, and no extra allocation,
native stack array or quota increase is needed. Caller receiver roots survive the
call and each payload location keeps its heap owner. Exact descriptors and bounds
are checked before payload access.

Integer CIL v26, interface plan v12, scalar boxed IR v12, object IR v31 and native
v58 record this extension. Layout v22, core contents and production resource
limits are unchanged.

Ten dispatch probes pass 2,940 repeated native/.NET comparisons: mixed class/value
maps, escaped boxes, mutable aliases versus independent boxes, reference-bearing
nested/wide payloads, inline arguments/results, mutation before faults, null calls,
allocation and self-copy, explicit generic owners, reference results and empty
values. Existing four ancestry/storage probes also pass. Both O0/O2 builds use
forced GC, ASan/UBSan, exact frame-budget recovery/cleanup, a 2,048-byte native
frame warning and 30-second subprocess limits. Four additional native tests check
null/forged/copied descriptors, forged references, capacities/counts, zero-width
receivers and suffix preservation without allocation. Detached mutation checks
reject missing/duplicate/wrong receiver maps, forged bindings, readonly or wrong
payload slots and mismatched catalogs.

All 113 distinct selected checks pass across f/h logs, including scalar confinement,
concrete list enumeration, ordinary/closed/generic class interface dispatch and
the unchanged 39-control/8-gap public baseline. The f run had one stale negative
assertion: legacy List.Enumerator.Current now reaches its missing owned body.
The h run pins that actual location and passes. The g run selected zero tests due
to intersected class/method filters; h uses only method filters. No timeout or
production bound was raised. Core build hash is unchanged; frozen EngineStep and
StateLookup still stop at the 256-type production bound. Full replay is unfinished.

Review checked metadata body identity, detached receiver/catalog authentication,
argument/result ordering, empty payloads, frame capacity, native pointer provenance,
rooting across allocation, and preserved scalar/class rejection paths. No known
issue remains within this bounded slice.

Evidence: artifacts/alchemy-boxed-interface/ac8-25c-* through ac8-25h-*,
engine-and-state.json, command.json and core/library-build.json. Public fixture
hash: 6e74ba743b7b31e3f637550b5d4fbfc08c519c7c6bf80df6a5c9e3c09a7a7ec7.
Core: 71f2ecc38cfc28f9c12678228ec59ec3d547d7dc0768c8cde5575db84230dabd.
Manifest: f7182b3c47ceb2d53759bdfd58af10b715e43f428d3cace1dab0bfd69ff98ca9.
