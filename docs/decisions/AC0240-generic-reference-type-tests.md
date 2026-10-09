# ADR AC0240: Instruction-scoped generic reference type tests

Recorded 2026-10-09 for AC8.96 after ccd2ff8.

The Alchemy closure exposes generic iterator casts and array fast-path type tests
whose substituted targets are private. The source names a generic parameter;
checking the substituted type as an explicit private name loses that distinction.
The converted CastIterator.MoveNext in Generics.cpp:6176-6183 resolves its cast
through the bound generic context and retains the resulting current item. File
SHA256: `9bf14842d4fc7346f69df2588e923354e53ca837f5d00092dc8e2395bf14be68`.
This separate Unity capture guides semantics; Cilica owns discovery, validation,
native emission and memory management, and .NET remains the differential oracle.

## Decision and review

Retain original bare-variable or single-vector-variable isinst/castclass/reference
unbox.any sites in each closed method. Evidence identifies the method/owner
specialization, variable scope/index, instruction offset, vector construction and
cast/test operation. Discovery charges its context and uses against the metadata
budget. Both logical and scalar validation bound their scans, reconstruct the
exact admitted reference or vector target, and match the operation at that offset.
Arrays can contain admitted primitive, reference, inline-value or Int32-enum
storage. Existing boxed-value tests keep their separate rules.

Ordinary visible targets retain their existing access rule. Opaque evidence adds
permission only at its validated instruction. Array allocation evidence never
authorizes a cast. Explicit foreign private names, fields, signatures, locals and
catch declarations keep their source-level access checks. No catch/exception
permission, nested-vector support, generic member-access shortcut, runtime cast
algorithm or ownership rule is added. CIL120/object IR73 identify the new retained
facts; native86, value-layout model5, core, grants and production limits stay fixed.
Review checked context hashes, exact nominal kind, operation/offset identity,
logical/scalar preservation, bounded scans and allocation/type-test separation.

## Validation

The new fixture covers inheritance, null, failed casts, exact constructed types,
reference covariance, reference-bearing value arrays, enum arrays, vector generic
arguments, primitive arrays, Cast collection/iterator paths, OfType and Skip.
Malformed/missing evidence is checked on hidden reference, value and enum targets
in both IR stages; public constructed owners may already be visible without this
additional evidence. Foreign explicit-name controls remain negative. The raw
foreign array-and-cast case is now positive and removing only its cast evidence
fails even while its array evidence remains.

Fixture development encountered the separate private generic constructor and
compiler-generated explicit-interface forwarding restrictions. The fixture uses a
public generic owner with a private argument and the existing nongeneric test
sequence to isolate this slice. Those restrictions were not widened. Two stale
OfType projection negatives now translate and are promoted to native/.NET checks.

104 distinct selected checks pass across the final focused/adjacent runs. The 14
new roots, one promoted raw-IL root and two promoted projection roots add 4,998
native/.NET comparisons under O0/O2, ASan/UBSan, forced collection, cleanup and
exact/one-short frame budgets. The final malformed-evidence checks pass in 2.045s;
new roots/initial controls pass in 37.145s; 79 unchanged adjacent checks pass in the
241.644s run and the two promoted projections pass in 24.399s. A bounded check on
the AC8.95 binary confirms the projection negatives were already stale before
this slice. Validation receipts: artifacts/alchemy-generic-type-tests.

The full diagnostic IR audit reduces failing bodies from 32 to nine, clearing 23
cast/test failures. Six remaining tests name ordinary internal types from generic
callers; two constructors branch before initialization and one delegate capture
also remain. All 15,254 bodies translate (61,756 blocks, 294,454 statements), with
3,839,616 metadata units and 70,754ms for the engine probe. StateLookup validates
logical/scalar IR in 702ms/11,759 units. Diagnostic capacity and the frozen input,
core and manifest pins are unchanged from AC0239. The engine returns no validated
program and no native engine/replay execution is claimed. Receipts:
artifacts/alchemy-generic-type-test-ir-inventory.
