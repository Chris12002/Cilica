# ADR AC0141: Ordinary reference-key equality and hashing

Recorded 2026-10-07 for AC3.25.

## Evidence and decision

The committed ecc141b frozen diagnostic probe reaches Dictionary.Add for
EffectiveEffectNode/EffectTransaction in EffectSchedule.Compile IL_00d7 after
356,495 metadata units. Discovery stops on reference-key text. Reviewing that
path also exposes the missing reference equality/hash prerequisite. StateLookup
remains eight methods/9,927 units. No engine emission/execution occurred.
Evidence: artifacts/alchemy-constrained-reference-{refresh,engine}.

Keep selection in the already exactly granted KeyOperations<T> boundary. Walk
the closed static key's interfaces and base types within existing metadata/type
budgets. Choose typed equality only for exact IEquatable<T>; a contract present
only on a dynamic derived instance must not change a base-typed collection.
Ordinary managed helpers implement null guards and call the existing virtual
Equals/GetHashCode or interface Equals paths. Existing string and Type special
cases are unchanged. No reflection, generated user code, or native helper is added.

An ordinary-class equality call can coexist with unrelated boxed enumerators.
The old safety analysis used the virtual declaration owner Object and rejected
all such boxes. Use the validated receiver type at each actual class call site.
Object aliases still retain Object and are rejected; the ordinary IR validator
also independently rejects special/runtime receivers without a complete map.
Retagged managed-location loads must fail before and after scalar lowering.

Integer CIL v59; object IR v41, native v67, scalar boxing v13, layout v25 and all
production limits remain unchanged. Reference-key duplicate/missing text,
Object aliases, vector/special receivers and nullable value equality remain
separate boundaries. This slice does not close the frozen engine blocker.

## Validation and review

Focused tests cover identity keys, colliding overridden equality/hash callbacks,
IEquatable preference, static versus dynamic receiver contracts, inherited closed
generic contracts, nulls, hash/equality faults and allocation during callbacks.
The List construction probe composes a real boxed enumerator with Object-slot
dispatch. Four former reference-negative fixtures are promoted only with native
execution. Exact equality/hash grants and the original boxed-dispatch boundaries
are checked. Detached type-forgery checks cover both IR stages.

Review checked callback receiver/argument order, copied reference search values,
managed-location lifetime, exact interface matching, inherited metadata limits,
complete ordinary dispatch maps and the independent alias/box safety checks.
Local main remains the ancestor 48ecf14. All 97 distinct selected checks pass:
93 reference/search/dictionary/boxed-confinement/dispatch checks, followed by the
isolated boundary and four public-baseline checks. The first broad run had one
negative fixture reaching a nested-vector gate too early; removing that unrelated
operation confirms the intended special-receiver rejection. Twelve new/promoted
probes supply 3,528 repeated O0/O2 native/.NET comparisons with forced GC,
ASan/UBSan, exact cleanup, 2,048-byte frames and unchanged process timeouts.
Public baseline remains 46 controls and one diagnostic gap. Evidence:
artifacts/alchemy-reference-keys/{regressions,boundaries-baseline}.log.
