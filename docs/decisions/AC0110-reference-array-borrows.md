# ADR AC0110: Checked reference-array element borrows

Recorded 2026-10-07 for AC7a.4.

## Decision

Complete the second prerequisite exposed by frozen BoardTopology.Blocking:
reference-array element addresses compose with AC5b.10 scoped ref locals. The
logical IR carries the exact array and invariant reference element type. Scalar
lowering uses the existing array-slot carrier, with slot zero for references.
Native carriers retain the array owner and exact descriptor; owner roots survive
allocating calls and replacement of the source array variable.

Writable element addresses check null, bounds, then exact array type, matching
.NET covariance fault order. Loads/stores authenticate ownership, backing storage,
slot/index, readonly state and payload compatibility. Existing inline-value slot
validation remains unchanged. Primitive element borrows, ref returns, ref-local
exception regions and general readonly-prefix lowering remain unsupported.

Integer CIL v36, value-array IR v3, scalar-location IR v4, object IR v36 and native
v63 record the extension. Carrier layout, core library and production bounds are
unchanged. The array component address helper is kept separate and noinline to
preserve the existing 2,048-byte O0 sanitizer stack-frame limit.

## Validation and review

Six probes provide 1,764 repeated O0/O2 native/.NET comparisons: cached null
coalescing, source-array replacement, reborrowing through an allocating callee,
strings, closed generic references, and null/bounds/covariance faults. Four IR
mutation checks reject element width, missing fault edges, wrong array identity
and invalid scalar slots. Two native checks exercise forged, readonly, foreign,
stale and incompatible carriers, forced collection and complete cleanup.

The initial O0 sanitizer build exposed a 2,528-byte helper frame; splitting the
helper fixed it without raising the limit. The final nine positive/value-array
checks and 45 safety/regression checks all pass. ASan/UBSan, forced GC and
30-second native subprocess caps remain enabled. Public baseline remains 43
native controls/four gaps. Review covered exact token-selected element types,
covariance fault order, owner rooting, descriptor authentication, value-array
regressions, bounds and pointer arithmetic. No outstanding finding remains.

Full engine compilation and AC9 replay remain unfinished. Frozen application
inputs are unchanged. Local main 48ecf14 remains an ancestor.

Evidence: artifacts/alchemy-reference-array-borrows/tests-{a,b,c}.log and hashes.json.
Core: d77f711997696db4e72d687ba835aed4eccd83fbd606ddfbae60911e3447b569.
Manifest: 032931b75ac0e7b2770b861ecb1d2163ab88769a414931bb42cfc10c5f47ecc4.
Fixture: c4954931d9a7cd5907dcb6bda9fdcd76d28320e5f6942b3f8728bc801442f5e0.
