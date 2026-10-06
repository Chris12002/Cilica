# ADR AC0075: Nominal scalar runtime type tokens

Accepted 2026-10-07 for AC5b.6.

The engine calls Enum.IsDefined(typeof(Heading), boxedHeading). The first
prerequisite is scalar ldtoken support. Admitted integer, Boolean, Char and public
Int32 enum type tokens now use the same validated nominal scalar-box plans as
boxed GetType. Shared stack carriers never merge type identities: Int32, UInt32,
each enum, Int64 and UInt64 retain distinct canonical Type objects.

Logical/scalar validators accept a scalar token only with an admitted scalar plan
and the exact selected RuntimeTypeObject hierarchy. Scalar targets seed value
layout usage; reference catalog usage receives only the runtime implementation.
Emission already assigns boxed descriptors and canonical per-instance type-cache
slots, so the existing allocator, tracing, budget, owner checks and lifetime rules
are reused. A token alone admits the corresponding bounded plan without allocating
a value box. No new library capability or physical runtime layout is introduced.

Six new probes plus the formerly negative Int32/Int64 token probe pass 2,058
repeated O0/O2 native/.NET comparisons, with forced GC, ASan/UBSan and exact cleanup.
They cover all ten admitted primitive scalar types, same-carrier enum distinction,
boxed identity, token-only use, copied handles, generic substitutions, dictionary
keys and null GetType receivers. Three altered-IR checks reject unknown target
identities, missing scalar facts and missing allocation fault edges at both stages.
General structs, Nullable tokens, floating-point and open generic tokens remain
diagnostic. Existing hidden-enum boxing/access boundaries remain unchanged.

All 52 selected checks pass, including runtime cache ownership/quota corruption
checks, scalar boxes, enum shape tests and the public baseline (31 native/16 gaps).
One stale private-helper negative assertion was updated to the earlier lexical
rejection introduced by prior access slices; the unauthorized call stays rejected.
Evidence: artifacts/alchemy-scalar-type-identity/ac5b6a-* (reduction), ac5b6b-*,
ac5b6c-* (final), and engine-and-state.json (unchanged frozen inputs).

Review checked nominal identity, both catalog usage paths, validated scalar plans,
allocation fault edges, cache rooting, existing native descriptor indices and
unchanged bounds. Integer-object CIL advances to v19, object IR to v27, scalar
boxed IR to v10 and native profile to v53; layout schema remains v21.
Frozen engine still requires the Enum.IsDefined declaration; state still exceeds
its catalog bound. This prerequisite does not claim that query or full replay.
Core SHA-256: 69356718f55f03402865ad808087b2e78e0bbe321fed5ff6efca2a8f180a2b31.
Manifest: 3d57664ccf4ab435bc35430109c16ddbc660a43441d03ab97a6118eb8b6e17ae.
