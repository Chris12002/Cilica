# ADR AC0126: Hidden inline values in generic arguments

Recorded 2026-10-07 for AC5b.14.

AC8.33's isolated copy reaches internal readonly EffectBindingContext in a
Func generic signature at EffectOperationRegistry::.cctor IL_0046 after 262,566
metadata units. The source keeps internal members and reference-bearing fields.

Apply lexical source visibility to named inline values, including generic
arguments. Source declarations, locals and operand tokens are checked before
substitution; foreign explicit names never borrow authority from a bound T.
Generalize the bounded opaque-argument walk used for enums to named/constructed
inline values. Copies, signatures and nominal boxes retain their resolved type
identities and existing layouts; the proof only authorizes carrying an already-
bound declaration. Public fields on a lexically accessible hidden value become
accessible, while private/member checks remain unchanged. Hidden generic value
arrays retain their separate IR access diagnostic.

Integer CIL v49 records admission. Object IR v38/native v65/layout v24, owned
core, grants and all production limits remain unchanged. Shared enum access
continues to use its exact enum checks; the source-name walker needs no separate
argument-vs-direct visibility policy now.

Seven fixture probes cover copy independence, traced reference identity, generic
holders, delegates, private nullable round trips, private boxing/public fields,
and the engine-shaped readonly/internal-accessor mixed Func signature. One raw
foreign helper boxes its opaque T; the caller checks nominal isinst and unboxed
bits. Explicit foreign local/signature/box/token/field uses and private field
access remain rejected; hidden-value generic array creation also stays rejected.
The first raw signature witness had a mismatched MemberRef and was corrected to
reach the intended lexical check. All 82 selected checks pass, and all 23
focused/public-baseline checks pass after adding the readonly mixed-delegate
probe. Eight native probes provide 2,352 repeated O0/O2 comparisons with forced
GC and ASan/UBSan. The 2,048-byte frame and 30-second subprocess bounds remain.
The public baseline remains 43 controls/four gaps.

Review checks pre-substitution source access, bounded opaque traversal, exact
closed storage, member visibility and traced copies/boxes through allocations.
Main 48ecf14 remains an ancestor. Application rules/frozen inputs are unchanged.
Full engine compilation/execution and AC9 saved-game replay remain unfinished.

Evidence: artifacts/alchemy-hidden-value-generics/tests-{b,c}.log and hashes.json.
Core: ab19cfec0d104da213a013f98cfdcd5fcfee0a66fa3ca727d4c00d83f32f7847.
Manifest: 244ed51d51db17d4577cb22dc38e7281caa248be41751bf42dee88292addb858.
Fixture: 47aab463eb6ad77334ba499b028df91a61e7b2d39d8da033b1968d272fa4dfbc.
