# ADR AC0053: Scalar boxes beside typed interface receivers

Accepted 2026-10-07 for bounded AC1b.5.

Nongeneric cursors return boxed scalars while also invoking typed enumeration and
disposal interfaces. The earlier rule required every scalar allocation to be
confined whenever any interface operation existed, rejecting this safe case.

Keep the confinement requirement for an interface cast/test whose input is not
already statically interface-typed. An already-typed interface receiver relies on
IR parameter, call, field, local, edge and assignment checks. Scalar boxes cannot
be assigned to that receiver using incomplete primitive ancestry: the missing
object-to-interface conversion would itself trigger confinement, or an invalid
assignment is rejected. Method dispatch on a typed receiver alone does not require
unrelated scalar allocations to be confined. Known named-box dispatch checks and
class-dispatch checks remain unchanged. This does not implement primitive
interface contracts or boxed payload receiver dispatch.

Inspect operand types within each block: value identities are block-local. An
initial implementation incorrectly built a method-wide dictionary and was caught
by duplicate-ID regressions before native execution. Corrected checks use each
block's validated definitions. Retagging an object parameter as an interface does
not bypass the rule; both logical/scalar validators reject that forged input.

The native matrix now includes returned and nullable boxes, repeated local reads,
array/field storage, recursive forwarding and a parameter-address mutation, all
beside independent typed interface calls. Twenty-one probes, including the
allocation-lifetime control and a mixed unconstrained-interface test, pass 6,174
repeated native/.NET comparisons at O0/O2 with forced collection, ASan/UBSan and
exact cleanup. Object-to-IComparable tests/casts remain diagnostic. Detached tests
still reject escapes from boxed tests/casts when an unconstrained interface
conversion exists. A new forged-parameter check rejects at both stages. Adjacent
box/type-test and public baseline checks pass in the composed integration run.
Evidence, including initial failures, is under artifacts/alchemy-of-type.

Review checked every route into an interface-typed receiver, original box-type
preservation, block-local identities, invocation/return validation, unchanged
class/named-box rules and work bounded by existing IR limits. No runtime allocation
or collection algorithm changes. Boxed value IR advances to v8 and scalar boxed
IR to v9; native remains v49. Full engine and saved-game replay remain unverified.
