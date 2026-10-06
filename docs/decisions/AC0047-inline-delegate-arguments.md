# ADR AC0047: Inline-value delegate arguments

Accepted 2026-10-07 for bounded AC2.3.

Extend single-target delegates with admitted named struct parameters in the integer
object CIL profile. Preserve exact nominal parameter types in logical IR. Expand
these parameters through the same validated field plan already used for ordinary
calls: Int32, Int64 and reference leaves. Empty values expand to zero slots. Native
calls still pass compiler-owned location argument packets; no boxing, native pointer
capture, wrapper object or struct ABI is introduced. Return types retain their
existing Int32/Boolean/reference/void contract.

The shared scalar catalog transformation expands delegate declarations alongside
heap/static fields. Scalar boxed validation reconstructs that catalog from retained
logical layouts and rejects disagreement. Logical layout validation requires every
value argument to identify an included nonscalar-box layout, and reference catalog
walks distinguish value identity from reference identity. Exact target signature
validation occurs before nominal identities are erased. Expanded individual and
aggregate signature quotas remain bounded by the existing 4,096 parameter budget;
the receiver slot remains reserved. Existing root, scalar-slot, frame and call-work
budgets remain unchanged. No new library grant is needed.

Profiles advance to delegate-values-ir-v3, scalar-delegates-ir-v3, integer-managed-
objects-cil-v9, object-operations-ir-v19, object-layout-v18 and native v46. The layout
profile change records the widened catalog signature contract; physical delegate
object storage is unchanged. Exception IR v5 is unchanged. Direct wide/narrow/enum
parameters, value results, byref signatures, boxed value receivers, variance and
multicast remain outside the CIL profile.

Fifteen authored probes pass 4,410 repeated O0/O2 native/.NET comparisons on macOS
ARM64 with ASan/UBSan, forced collection and frame bounds. They cover static and
captured targets, closures, multiple/nested/empty values, all Int64 bits, copied
scalar mutations, shared reference identity, reference and void results, null and
throwing calls, and value-predicate Count/FirstOrDefault. The latter two advance
previous negative fixtures into native controls. One compiler-generated lambda
retains its static singleton/cache; its group uses the existing stable persistent-
root harness. Other groups require exact cleanup. Initial strict-cleanup failure
for that legitimate static cache and an authored private-helper access failure are
preserved; no game inputs or compiler access gates were changed to mask either.

Detached tests reject missing/wrong/future arguments and missing fault edges at
both stages, unknown/reference/byref value signatures, equal-layout nominal type
substitution and altered scalar signatures. A 4,100-leaf logical argument validates
as a layout but fails the expanded delegate signature budget. All 76 focused
existing-delegate, boundary, expanded-limit and public-baseline checks pass. Public
counts remain 31 controls/16 gaps.

Review checked argument-copy ordering, reference roots across allocation, Int64
packet members, empty argument omission, null invocation checks, exact preserved
catalog comparison, target identity and bounded expansion before allocation.

Frozen engine discovery now reaches CIL3008 while resolving internal
GameStateScope.set_CurrentRound at PrepareForfeit IL_0474. Its explicit MethodImpl
for internal IEffectScope triggers the metadata interface-visibility gate. State
still stops at 257/256 discovered identities. Evidence is retained in
artifacts/alchemy-value-delegates. This is discovery progress, not engine execution
or completed saved-game replay.
