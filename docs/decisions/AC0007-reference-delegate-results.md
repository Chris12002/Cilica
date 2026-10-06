# ADR AC0007: Exact managed reference results from delegates

Accepted 2026-10-06 for AC2.1 in the integer managed-object profile.

## Decision

Extend the existing single-target delegate path to return admitted managed
references: Object, strings, nominal classes/interfaces, exactly closed reference
generics and supported vectors. Keep exact target parameter and return matching.
This does not add return covariance, multicast invocation, open-instance binding,
wide/value/byref results or recursive delegate signature discovery. The narrower
standalone CIL delegate profile retains its Int32/void signature bound.

Store one authoritative IrType ReturnType in ObjectDelegateType. ReturnsVoid is a
computed convenience; the existing bool constructor forwards to Int32/Void for
source compatibility. JSON selects the typed constructor and does not serialize
a second return flag. Catalog validation checks shape and membership, follows
return identities in the closure, and compares exact results between detached
interface and value catalogs. Vector pruning retains signature-only array results.
Capture validation compares the target method result exactly, and invocation
values carry that same type before and after scalar lowering.

Native delegate targets have distinct Int32, void and Object* function pointers.
Use the typed reference pointer without casts between function types. An invocation
writes its reference directly into the caller's existing rooted value slot before
pending-exception handling. Callee frame teardown does not allocate or collect,
so the ordinary reference-call return protocol covers the interval between the
callee root and caller publication. The captured receiver remains traced by the
delegate; argument ownership, null checks and target authentication are unchanged.

Object layout v15, delegate value/scalar IR v2 and native v41 record the change.
The owned core, mapping grants and public embedding ABI are unchanged. Required
Func/Action/Predicate/Comparison declarations and tested signatures belong to
AC2.2; mapping those declarations alone would not supply execution semantics.

## Evidence and review

Thirteen new authored probes and the advanced public RefDelegate probe provide
4,116 repeated native/.NET comparisons on macOS ARM64 at O0/O2, with ASan/UBSan,
collection before allocation, exact/insufficient frame budgets and heap/root
cleanup assertions. Coverage includes fresh results forwarded through a helper,
shared identity, captured receiver cycles, null results, target faults, argument
faults before null invocation, string/array/interface/Object/closed generic
results, reference argument aliases and a Type-to-handler dictionary returning
objects. A signature-only array checks catalog retention without a target body.

Five rejection fixtures cover target covariance, wide/value/byref results and
recursive delegate signatures. Detached mutations cover malformed/unknown result
types, target/result disagreement and mismatched dispatch catalogs. Typed JSON
round-tripping and existing Int32/void/reference-argument tests remain covered.
A byref rejection assertion was corrected to match the earlier signature gate.
The public ledger moves from 37 gaps/10 controls to 36 gaps/11 controls.
201 existing delegate, catalog, dictionary, runtime-type and public-gap checks
pass in two targeted regression runs.

Review checked closure traversal and pruning, exact target and call types, typed
native function pointers, reference publication before exception routing, and
non-allocating callee teardown. No runtime limits were increased. Full AC2,
remaining AC1b work and saved-game replay remain incomplete; private application
roots were not rerun. Validation is local; no hosted work or extra agents.
