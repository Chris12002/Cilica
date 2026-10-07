# ADR AC0152: Retain delegate identities for vector helpers

Recorded 2026-10-07 for AC8.45.

## Evidence and decision

After ec492b9, the frozen engine clears the hidden enum packet context and reaches
a missing closed vector element identity after 780,053 metadata units. A copied
diagnostic names AlchemyGameEngine/CommandHandler (0x020002d1), a private delegate,
while preparing nongeneric collection Count through Enumerable.Count. StateLookup
still admits 15 methods after 11,511 units. No native engine emission/execution.

Delegates bypass the ordinary class-layout observer which populates the known
constructed-type table. Retain their exact constructed identity immediately after
successful delegate shape/signature validation and bounded fact insertion. This
lets existing selected array helpers bind their generic element without guessing
from names or treating delegate instances as ordinary class layouts.

No shape, lexical visibility, generic binding, intrinsic grant, array assignment,
dispatch recipe, packet validation or runtime ownership rule is relaxed. The
same catalog/parameter bounds still apply. Integer CIL v68 records admission;
core, interface plan v15, object IR v43, layout v26 and native v69 are unchanged.
Production and diagnostic resource limits remain unchanged.

## Validation and review

The reduced nongeneric Count test reproduces the missing metadata diagnostic
before the change. Four authored probes then pass at O0/O2 with ASan/UBSan and
forced collection, providing 1,176 repeated native/.NET comparisons. They cover
private nongeneric delegate arrays and closed Func arrays; counting, indexed
reads, CopyTo and nongeneric Clear; changed captured state, null elements and a
saved delegate remaining live after its array slot is cleared. Exact zero-root
cleanup is checked by the shared harness.

All 64 selected delegate, hidden-delegate, generic, array-interface and hidden
packet checks pass. Evidence: artifacts/alchemy-vector-metadata-audit/{red,tests}.log.
Review checked insertion after validation, exact closed identity, immutable
metadata ownership, helper selection, captured-target retention and unchanged
reference-array copying. No frozen input or application rule was modified.
Full frozen engine, unchanged fingerprint execution and saved-game replay remain
unverified.

All four public-baseline checks also pass: 68 distinct checks overall, 46 native
controls and one retained gap. All six frozen input hashes were reverified.
Local main remains ancestor 48ecf14. Evidence: baseline.log and
frozen-inputs-verified.json.
