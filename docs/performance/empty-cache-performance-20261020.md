# DiffPlex: trusted empty-array cache lookup

2026-10-20. Base compiler `34f3ce2`, native v101 -> v102. Apple M4 Pro,
macOS ARM64, Apple Clang 17, .NET 10.0.11. Implementation: ADR 0209.

## Result

| Fresh compiler output | Median us/diff | Samples |
| --- | ---: | ---: |
| v101 | 62.065 | 21 |
| v102 | 57.263 | 21 |

Execution time falls 7.74% in this paired series. This is the production compiler
implementation of the earlier cache diagnostic experiment. No heap quota increase
or application-library change. This slice did not rerun .NET or Unity timing;
older comparator measurements are not a same-series gap measurement.

## Correctness and review

All 20 focused tests pass: EmptyArrayLibraryTests, NativeInternalReferenceTests,
NativeTracingTests and ListLibraryTests. Cache tests cover O0/O2 and both internal
reference diagnostic settings with ASan/UBSan. Review checked publication, roots,
identity, failure behavior, checked boundary ordering and foreign/corrupt entries.
GC cache traversal still uses checked membership in both modes.

All 523 DiffPlex observations match the retained oracle in release and two
forced-GC O1 ASan/UBSan runs with internal reference diagnostics off/on and borrow
lifetime diagnostics enabled. Each runs on a requested 512 KiB pthread stack,
checking the actual macOS stack is within 512–576 KiB. Largest individual native
frames are 432 bytes release and 320 bytes sanitized. No sanitizer diagnostics.

## Reproduction

Fresh baseline emission is byte-identical to the previously accepted v101 C++.
Both candidates are emitted by the rebuilt compiler; compiler source hashes are
checked before/after emission. C++20, -O2 -DNDEBUG, no exceptions/RTTI. Timing uses
three alternating processes per variant, seven samples of 256 diffs after warmup.
Every measured checksum is 6146 per diff. No concurrent builds/tests/sampling.
Raw timing outliers are retained rather than filtered.

- Baseline C++ SHA-256: `efa98008bcfd4a89c17622128bef9f5fd7eff10fb5c9e86a440f165b50cf5740`.
- Candidate C++ SHA-256: `5743342e150dbcce28442562477dde7ee34739e1af7b42dde0090752a91c2b13`.
- Owned core SHA-256 (unchanged): `655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1`.
- DiffPlex DLL SHA-256: `652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`.
- DiffPlex PDB SHA-256: `4eac2d8ae6c2e7efaade8bca707bcbbe5673dc317cf141176fd4ce7b6ee285b8`.

Roslyn IL, source-version limitations and .NET/Unity comparison remain those in
[the v101 investigation](v101-profile-20261020.md): the Release IL reads the
canonical empty array, .NET uses a per-T static array and Unity's generated List
constructor loads a static empty array after initialization. Cilica retains its
per-instance cache while removing redundant heap membership work.

Compact evidence and replay scripts: `docs/performance/empty-cache-performance-20261020/`.
Large generated/native outputs: `artifacts/empty-cache-performance-20261020/`.
