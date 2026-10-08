# Batch011: bounded public vector Array.Copy

Implemented the public three/five-argument Int32 Array.Copy overloads for proven
vectors with the same declared element type. Separate exact grants route calls to
ordinary managed helpers. Copies preserve null/range/type fault order, overlap,
UInt32/Int64/narrow values, runtime covariance with partial writes, inline values,
argument evaluation and precise GC roots. No runtime or quota changes. CIL98,
IR60, native79; see [decision](../decisions/compatibility-013-array-section-copy.md).

**135 distinct checks passed across focused runs:** the final combined checkout
passed 57 Copy/Clear/candidate-probe/callback-contract checks in 101.655s. The 78
adjacent Copy/mutation/UInt32 checks passed in 99.206s before peer callback-contract
integration; that timing distinction is retained, not represented as one full run.
Ten semantic roots, a generic constructor/field probe and the original independent
candidate Copy reductions supply **3,822 new repeated native/.NET comparisons**.
Four additional O0/O2 lifetime checks prove overwritten payloads are reclaimed,
copied payloads survive source clearing, and final roots release exactly.
Native checks use forced GC, ASan/UBSan and existing strict frame checks.

The initial run passed 31/32: an authored widening negative used an inline array
initializer and reached the earlier producer-shape diagnostic. Locals now isolate
the intended same-element restriction. The repaired selection passed 3/3, and the
final combined selection passed all 57. An intervening filter accidentally selected
zero checks; its log remains explicitly non-evidence. Builds succeeded with the
existing CS0108 fixture warning. Full repository tests and Alchemy replay were not run.

[Evidence](batch-011-evidence.json) retains final source/assembly hashes, all build
and test logs and generated C++/native executable hash receipts collected before
successful temporary cleanup. [Combined result](batch-011/combined-tests.log) and
[adjacent result](batch-011/adjacent-tests.log) support the totals. The capture covers
VerifyTranslated and new Copy lifetime harnesses; older custom Clear GC harnesses
are not instrumented. Earlier CRC generated-C++ hashes remain historically missing.

The [manifest](batch-011.json) keeps CircularBuffer mapped-blocked. Only independently
authored reductions ran; no third-party CircularBuffer source was staged, compiled
or executed. Its Int32 constructor/mutation/Clear closure is ready for a separately
approved selected-root trial. ArraySegment, ToArray and enumeration remain unverified.
General Array aliases, joined or unsupported producer shapes, cross-element widening/
boxing and Int64-length overloads remain diagnosed. Verified CRC roots are skipped.

Review milestone 6's callback-contract and measured-audit documentation commits are
preserved. The performance lane owns its experiments; read-only review found no
remaining concrete blocker for the revised experiment, with documented limitations.
No index optimization was adopted. Candidate cleanup was unnecessary; own tests,
receipts and failed attempts remain available for resumption.
