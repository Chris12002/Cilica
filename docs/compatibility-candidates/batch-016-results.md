# Batch 016: exact Char delegate parameters

Local owned slice, 2026-10-08, on `6e76e33` with peer documentation commit
`97ae9c4` preserved. No candidate source downloaded or executed.

## Outcome

Exact Char parameters now compose with existing single-target delegates, including
Func<Char,Boolean>, Predicate<Char>, Action<Char> and multiple Char arguments.
The compiler authenticates exact Char target parameter identity before reducing it
to the existing Int32 carrier. Existing method argument normalization, receiver
roots, exception paths and native function pointers remain unchanged. Integer CIL
advances from v102 to v103; object IR 62/native 79, quotas and grants stay unchanged.

All **51 distinct focused checks pass** (57.332s), including **14,808 new repeated
native/.NET comparisons**: six callback probes  × 70 inputs  × 3 repetitions  × O0/O2,
and 2,048 groups covering all 65,536 UTF-16 units  × 3 repetitions  × O0/O2. Forced GC,
ASan/UBSan, exact root/borrow cleanup, exact/one-short managed frames and strict
2 KiB native frames stay enabled. Captured cycles, sole-receiver rooting, live
callback mutation, null targets, argument-before-null invocation and callback
faults are covered. Five forged Char aliases fail before lowering. Older delegate
profile and other narrow signature gates remain checked.

The initial run passed 41/51; all ten failures came from proposed Char-array Any
composition reaching a separate narrow-vector interface rejection. That source
and both minimal Any probes remain; their expectations now name the exact CIL2001
gate. Independent live-loop callback tests do not claim Any compatibility.

A final negative-only refinement makes Byte/UInt16 callbacks return Boolean,
isolating parameter admission from result rejection. All three targeted negatives
pass (1.836s), overlapping the 51 distinct checks. Positive bodies and production
were unchanged; their tested fixture source is retained. The positive-run managed
DLL hash was not captured before this rebuild; final managed DLL hashes apply only
to the negative rerun. All 30 final native source/binary hash pairs are retained,
plus 22 from the initial attempt.

## Candidate status and next action

The existing pinned online screen is carried forward in [batch 016](batch-016.json).
Albireo.Base32 remains **mapped-blocked**: its owned minimal Char predicate advances
from delegate admission to generic Char-vector interface operations in
Enumerable.Any. The current narrow-vector gate is unchanged. Review bounded
Char-vector enumeration/indexed reads independently next; full decoder closure and
encoder Decimal.Ceiling remain unverified. Wiry.Base32 unsafe roots, LruCacheNet
locking/event paths and current SimpleBase framework targets retain prior skip
reasons. No new screening or candidate compatibility result is claimed here.

No upstream candidate execution, full suite or Alchemy replay was run. No disposable
candidate copies were created, so none required cleanup. Build artifacts remain
under `artifacts/compatibility-batch-016`; durable evidence is retained below.

## Review and evidence

Review checked admission is restricted to the integer-object profile, scalar
identity is checked before carrier erasure, UInt16 normalization uses existing
method logic, and native dispatch/rooting/storage have no changes. Existing wide
and framework callback regressions pass. Peer docs-only changes were preserved;
no outstanding production finding. Initial warnings include the pre-existing
HashAlgorithm.State fixture hiding warning.

- [Decision](../decisions/compatibility-018-character-delegate-parameters.md)
- [Hash manifest](batch-016-evidence.json)
- [Final semantic run](batch-016/refined-tests.log)
- [Parameter-only rejection checks](batch-016/parameter-negatives-tests.log)
- [Initial failure transcript](batch-016/initial-tests.log)
