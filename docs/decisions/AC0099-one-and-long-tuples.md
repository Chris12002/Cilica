# ADR AC0099: One-field and long-tuple storage and equality

Recorded 2026-10-07 for AC5b.9.

## Engine evidence

The committed ba3bd43 compiler with unchanged diagnostic-only 1,024-type,
1,024-method and 262,144-metadata bounds clears enum formatting and reaches
unmapped netstandard ValueTuple`8 in AlchemyGameEngine.EndParticipation, through
CreateHandlers callback b__36_10. It charges 103,353 metadata units. Frozen IL
shows seven fields plus ValueTuple<IReadOnlyList<ParticipantState>> Rest.
StateLookup still translates eight methods/9,917 units. No native emission or
execution was attempted. Production bounds remain unchanged and still reject
the engine/state closures. Evidence: artifacts/alchemy-enum-format-{refresh,engine},
including exact diagnostic source hashes and handler/end-participation IL.

## Decision

Add selected ValueTuple<T1> and ValueTuple<T1,...,T7,TRest> declarations, fields,
constructors and typed equality, retaining six public nominal interfaces and the
struct constraint on TRest. Existing generic value lowering supplies storage,
copies, returns, heap/vector fields, boxing and nullable composition. Keep
unimplemented members as explicit diagnostic bodies. No raw external tuple
layout compatibility is implied by the managed sequential storage.

The long constructor must reject a non-tuple Rest before storing any field. A
private granted TupleShape<T>.IsTuple declaration proves the exact closed owned
type and lowers to a Boolean constant. This replaces the nonescaping private
IValueTupleInternal marker test: the currently admitted tuple identities are
1, 2, 5 and 8, matched by selected assembly/type identity. Public ITuple alone is
insufficient. This is deliberately not recursive validation: a default long tuple
is still a tuple when used as Rest even if its own Rest is invalid. Future tuple
arities must extend the predicate alongside their implemented declarations.

Typed equality copies each field immediately before its callback, preserving
short-circuit order and shared-reference effects. Hashing outside the pair,
comparison, formatting, object equality, structural APIs and tuple indexing remain
diagnostic. Integer CIL v31; native v60/object IR v33/layout v22 unchanged. No
production bounds, hosted workflows or application rules change.

## Validation and review

Twelve probes, 70 input pairs each, three repetitions at O0/O2: 5,040 native/.NET
comparisons with forced GC, ASan/UBSan, 2,048-byte frame warnings and unchanged
30-second subprocess caps. Tests cover every field, Int64 bits, reference aliasing,
copies, returned values, heap/vector/box/nullable storage, invalid Rest and argument
side effects, public-ITuple rejection, default nested invalid Rest acceptance,
equality short-circuit/mutation/exceptions and original receiver preservation.
Exact shape grant denial and hostile private calls are rejected. Reference metadata
checks both new declarations, public contracts, field bindings and member signatures.

The first 21 checks had 13 passes and eight lazy-fixture failures at the existing
direct struct-return-to-heap restriction. The storage probe now uses an explicit
materialized copy; a separate reduced negative pins the original restriction.
The next 19 checks all pass, covering long tuples and the full public baseline.
Together, all 31 distinct checks pass. Review checked nominal identity versus
structural imitation, Rest constraints, validation/store order, copied callbacks,
reference roots and unchanged unsupported-member boundaries. Local main remains
an ancestor at 48ecf14.

Evidence: artifacts/alchemy-long-tuples/ac5b-9{a,b}-tests.log, build logs and
hashes.json. Core: ff46baa58f6405314e47c69786cdb53650df78f888a729a638de634866693ce7.
Manifest: 02e384e4cb374cc5376de039645c8289b04bf46cd45c51f4d6c9b0f8cd43bf50.
Fixture: 450450e8e6983e01f6573e75ad5260baf9072ae6ab12a92b7eac8a4f4da54eb0.
The engine/replay closure still requires a fresh probe after this commit.
