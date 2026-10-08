# ADR AC0229: Same-assembly instance field access

Recorded 2026-10-09 for AC8.85 after 2eb4a66 (AC8.84).

Frozen ActiveResolution 0x0600001b IL_03fa loads field 0x040008c6, Invocation.
Metadata confirms Assembly access on nested-private PhysicalDamagePayload
0x020002e3 inside AlchemyGameEngine 0x02000009. This legal access previously failed
because the instance-field gate only admitted same-owner or public fields.

Share ObjectFieldAccess (renamed from ObjectStaticFieldAccess) between static and
reference-instance fields in discovery and logical/scalar IR validation. Existing
same-owner and public rules remain. The integer-object profile admits Assembly and
FamORAssem only with equal content hashes and valid, nonempty lexical chains,
and an assembly-visible or lexically visible declaring type. Proven nongeneric
nested callers can access private members of their containing type. Reverse,
sibling, foreign-image and protected-only permissions are not inferred. Existing
inline-value field admission and readonly-constructor checks remain unchanged.
Closed generic assembly-visible owners retain their exact identities.

CIL109, object IR65 and native81 identify the newly admitted access semantics.
Physical layout28, interface20, grants, runtime algorithms and production budgets
are unchanged. The diagnostic literal allowance below is separate from production
runtime scratch/resource accounting and any concurrent performance work.

## Converted reference and review

Converted Alchemy.Engine.cpp lines 8157-8162 reads PhysicalDamagePayload.Invocation
after a null check; lines 8196-8198 reads Reactions. SHA256:
`f4762054c04bf5a9f7a2dc45b837171b4b7bd2ea82073975d56bd07b1215b10f`.
This is the existing alchemy-reference-20261008T2312Z capture, whose DLLs differ
from the frozen audit. Frozen metadata remains authoritative for member flags.
No application rules, assemblies or Unity runtime implementation changed.

Review checked image identity, exact closed owners, lexical visibility, the
separate readonly check, borrowed storage and unchanged physical rooting/emission.
The shared access predicate prevents metadata admission and detached IR from
using inconsistent permissions. No remaining findings for this bounded slice.

## Validation

Snapshot artifacts/alchemy-instance-fields starts at 2eb4a66. All 81 distinct
selected checks pass across the initial and focused runs: 64 adjacent AssemblyStatic,
NestedStaticField and HeapValueField checks, plus 17 new checks. Initial eight
raw-metadata failures were fixtures lacking System.Object base metadata; correcting
the fixtures exposes the intended access checks. The final new-test run passes all
17 in 6.765s; the prior adjacent successes remain valid because their source and
shared access predicate did not change. Profile labels were advanced before the
corrected native run.

Seven new native roots x 49 input pairs x three repeats x O0/O2 = 2,058 new
native/.NET comparisons. Cover scalar/wide/reference/reference-bearing value
fields, closed generic owners, nested private-type ownership, private containing
members, readonly reads, borrowed updates and RHS-before-null-store exception order.
Raw metadata checks read/write/borrow flags, foreign assembly rejection and legacy
profile boundaries. Both logical and scalar IR reject disallowed flags, readonly
stores and missing caller/target lexical facts. ASan/UBSan, forced collection,
exact cleanup/borrow counts and frame-budget checks remain enabled. No full suite
or hosted tests ran.

## Diagnostic capacity measurement

Before the field fix, artifacts/alchemy-guid-exact-capacity measures 1,810 default
runtime type names totaling 135,162 UTF-16 units (longest 273), in addition to
217,130 already charged units: 352,292 total exceeds the production 262,144 allowance.
Only the copied diagnostic MaximumTotalLiteralCodeUnits becomes 2,097,152. With
that room, the scan reaches the Invocation field at 3,818,341 metadata units in
59,172ms. StateLookup validates 15 methods/11,745 units in 450ms. Both emissions
are null. A missing System.Linq import in the temporary counter was repaired before
this scan. This is a capacity measurement, not a production-limit change.

After the fix, all method discovery and field-access validation complete; body
translation now reaches the aggregate value-IR resource limit in ArrayInterface<T>.Count
0x0600007a. Engine work remains 3,818,341 units in 60,448ms. StateLookup validates
15 methods/11,745 units in 429ms. Both emission fields remain null. The body-stage
capacity and subsequent full IR/native validation still need measurement.
Core SHA256: `92758a62b697681f4c99eedc222698bc14693eef2b1f6a9275e9dc1b9a14d751`.
Manifest SHA256: `a66a016b1b370ca085f69cf7ecb4aa2f0311467c97cd2930bb9ebfd8ad8179b2`.
Receipts: artifacts/alchemy-instance-fields-refresh and -engine; all six frozen
inputs match. Full native engine closure and saved-game replay remain incomplete.
