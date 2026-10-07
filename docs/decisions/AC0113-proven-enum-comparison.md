# ADR AC0113: Proven constrained Int32 enum comparisons

Recorded 2026-10-07 for AC5b.11.

## Decision

After 0f23f26, frozen engine discovery reaches StatResolver.Compare IL_001a:
a constrained Int32 enum receiver calls Enum.CompareTo(Object), with a directly
boxed matching enum argument. The unchanged diagnostic copy charges 217,890
metadata units; StateLookup remains eight methods/9,919 units. No emission or
execution. Evidence: artifacts/alchemy-ordinal-sort-{refresh,engine}, including
stat-compare-il.txt. A later instruction also requires ordinal String.Compare.

Add the exact owned Enum.CompareTo declaration and EnumCompareTo grant. Enum
implements the IComparable declaration needed to preserve the reference method's
public/final/virtual/new-slot flags; mapping checks remain strict. Discovery
requires an adjacent constrained/callvirt pair and a validated Int32 enum operand.
The normal control-flow analyzer rejects entry between a prefix and its call.
Translation accepts only a directly boxed matching enum or literal null in the
same block. Dynamic targets, aliases, different enum/integer boxes, boxed receivers,
wide enums and Enum generic constraints remain diagnostic.

Preserve original argument evaluation and the target box allocation. Load the
receiver's borrowed Int32 value after evaluating that argument. Matching boxes use
the existing checked unbox operation and signed comparisons, producing -1/0/1
without subtracting potentially overflowing operands. Null returns 1. Literal-null
calls retain scalar identity proof without introducing an unused boxed layout;
actual box instructions continue to register their exact payload layouts.

Integer CIL v39 records this extension. Existing IR/native v64/layout v24 operations
and resource bounds remain unchanged. No general boxed Enum or IComparable dispatch
is claimed. Frozen application code and rules are unchanged.

## Validation and review

Six probes provide 1,764 repeated O0/O2 native/.NET comparisons for signed extrema,
null targets, heap-field receivers, allocation pressure, mutation of the receiver
during argument evaluation, null field owners and throwing argument expressions.
Six negative cases cover differing nominal/scalar types, aliases, wide storage,
boxed receivers and unsupported Enum constraints. Removing the exact grant denies
compilation. Initial runs caught the method-flag mismatch and unused null-case
layout; both were fixed without relaxing validation. The generic-constraint case
was retained as a negative test rather than included in the capability claim.

All 74 distinct selected checks pass: eight focused checks and 66 enum formatting,
IsDefined, scalar boxing, constrained-call and public-baseline checks. ASan/UBSan,
forced GC, 2,048-byte native frames and 30-second subprocess caps remain enabled.
Baseline stays 43 native controls/four gaps. Review covered exact type/grant proof,
prefix control flow, receiver read timing, checked unboxing, heap-owner roots and
signed comparison arithmetic. No outstanding finding; main 48ecf14 is an ancestor.
Full engine compilation and saved-game replay remain unfinished.

Evidence: artifacts/alchemy-enum-compare/tests-{a,b,c,d}.log and hashes.json.
Core: 9b93fdaee53ae443e093c6bf2c27d54b4f78c475cdf97db73355abf4ec5121fd.
Manifest: d11fa1cc75b3dff6cb035a47d2190b3c55bd03fce0bb7b15f6d02ff374012b25.
Fixture: 79d324c154dff45a6390113edb0e889d7e89effb92150726f6d03bb4e18fee77.
