# ADR AC0243: Compatible reference parameters in method-group bindings

Recorded 2026-10-09 for AC8.99 after 7cdf735.

The final method-level Alchemy validation failure binds
Func<EffectNodeDefinition, Boolean> to SummonComposition.IsConsumingNode(IEffectNode).
The delegate supplies a concrete node that implements the target method's interface.
Discovery admits the declarations, but capture validation required identical
parameter types. Frozen source/IL establishes the precise binding; this method is
not present in the separate stripped Unity capture. Converted receiver/method pair
bindings (Alchemy.Engine__4.cpp:15239-15243, SHA256
`1b9ffafe2c693fc15fe8545fc8e397d1096140e11b18d60e9fbb816aca48debb`)
continue to guide explicit target identity and lifetime, not access authorization.

## Decision and review

Under the integer-object profile, a method-group binding may pass a reference
parameter to an assignable reference parameter of its target. The direction is
from the delegate's supplied type to the method's accepted type. Existing hierarchy
and interface/array assignability facts validate it in both logical and scalar IR.
Return types and nonreference parameters retain exact matching. Captured receiver,
method access, target identity, final-virtual lookup and argument count checks stay
unchanged. No boxing, narrowing, result covariance, byref conversion or conversion
between different delegate object types is added.

Both parameters use the existing Object* packet representation. The binding keeps
its exact delegate type and target method; no adapter thunk, native reinterpretation
or runtime algorithm is needed. Callee argument checks, managed roots, interface
calls and dynamic array-store checks remain active. Review checked assignability
direction, reference-only/profile gates, return invariance, instance-parameter skip,
inline payload flattening and exact native result-function selection.
CIL123/object IR76; native86, layouts, owned core, grants and production budgets
remain unchanged. No outstanding findings within this slice.

## Evidence

49 selected checks pass in 46.382s. Eight new roots add 2,352 native/.NET comparisons
under O0/O2, ASan/UBSan, forced collection, cleanup and frame-budget boundaries.
They cover class-to-interface and derived-to-base method parameters, Boolean and
void results, captured receivers, null arguments, reference-bearing inline payloads
and array covariance with preserved ArrayTypeMismatchException. Forged narrower
targets fail in both IR stages. Existing exact signatures, final captures, null
receivers, result invariance and unsupported return/byref controls still pass.
Receipts: artifacts/alchemy-delegate-reference-parameters.

All 15,254 frozen bodies now pass individual IR validation. The next whole-program
gate is cast-exception message resources: the existing source-type × target-type
table exceeds even the diagnostic 2,097,152 UTF-16-unit allowance. It constructs
complete text for every pair before any native execution. Discovery charges
3,839,616 units; the engine probe takes 92,632ms under shared host load. StateLookup
passes logical/scalar checks in 719ms/11,759 units. Limits and frozen input/core/
manifest pins are unchanged from AC0242. No validated engine program, native engine
or saved-game replay is claimed. Receipts:
artifacts/alchemy-delegate-parameter-ir-inventory.
