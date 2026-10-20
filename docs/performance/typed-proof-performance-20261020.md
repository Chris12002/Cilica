# DiffPlex: preserve reference types inside native kernels

2026-10-20. Base compiler `04a38e3`, native v102 -> v103. Apple M4 Pro,
macOS ARM64, Apple Clang 17, .NET 10.0.11. Implementation: ADR 0210.

## Result

| Backend | Median us/diff | Samples |
| --- | ---: | ---: |
| Cilica v102 (empty-cache fix) | 58.054 | 21 |
| Cilica v103 (kernel type preservation) | 48.767 | 21 |
| Ordinary .NET, non-tiered | 5.544 | 21 |

The compiler change reduces paired execution time by 16.00%, leaving an 8.80x
ratio to the separately rerun .NET comparator. The earlier cache slice reduced
its own paired time by 7.74%; these percentages are separate series. Unity was
not retimed. No native/NativeAOT equivalence or other-platform claim is made.

The typed wrapper still checks incoming types, borrows and array storage. Within
its closed non-allocating call graph, the compiler trusts declared field/array
result types and representation invariants. Null/bounds and exact virtual dispatch
remain. Diagnostic mode restores repeated checks. Default production code is no
longer the earlier generated-code ablation: it has a closed eligibility proof,
checked entries and regression coverage in the compiler.

## Validation and review

The main run contains 67 checks: 66 passed, one stale negative assertion expected
Int64 arrays to remain unsupported. That capability was implemented in AC0104;
the failure is in translation before the changed emitter runs. Remove only that
outdated negative case and include the same fixture in the existing O0/O2 native/
.NET comparison. All seven checks in the repaired class pass. The final selected
suite therefore has 66 distinct passing checks, with the two native array tests
now covering WideElements as well.

Selected classes: NativeTypedLookupTests, NativeTypedHelperTests,
NativeTypedBorrowTests, NativeInternalReferenceTests, ReferenceArrayTranslationTests,
ReferenceArrayBorrowTests, ArrayReceiverTests and ValueArrayInterfaceTests.
Coverage includes loops/locals, field/reference-array results, covariant arrays,
interface dispatch, unknown/allocating targets, staged aliases, writes before
faults, cold/depth/budget fallback, borrow validity and separately linked modules.

Four new O0/O2 diagnostic-off/on tests cover typed entry descriptors, malformed
array backing storage, valid calls with zero arena space, diagnostic rejection of
wrong/forged internal array elements and rooted forced collection. The initial
harness used two incorrect native names; those were repaired. It then exposed an
unsafe fallback for malformed array backing storage. The final implementation
sets invalid-state fault 6 before replay, both at checked entry and at diagnostic
internal array checks. All four corrected tests pass. Earlier failure logs remain
under the artifact directory; the final main-run log retains the stale assertion.

All 523 DiffPlex observations match the retained oracle in release and two
forced-GC O1 ASan/UBSan builds, with internal-reference diagnostics off/on and
borrow lifetime diagnostics enabled. Requested pthread stack: 512 KiB, observed
size required within 512–576 KiB. Largest individual frames: 432 bytes release,
320 bytes sanitized. No sanitizer diagnostics on final runs.

Self-review traced validation-before-emission through ObjectRootAssignment and
ObjectIrValidator; checked type preservation through parameters, default locals,
block edges, typed storage and the acyclic kernel graph; and checked null/bounds,
covariant reads, scalar staging, invalid-state fallback and module linkage.
No allocation, external reference write or unknown call enters that proof.
Public ownership and collector checks remain unchanged. Source hashes still match
the measured compiler after validation; no emitter rollback was observed.

## Provenance and measurement

The base binary is the freshly emitted/measured v102 binary, reproduced byte for
byte from commit `04a38e3`; see the cache report's committed-reproduction record.
The candidate is emitted by the rebuilt changed compiler, with source hashes
checked before/after emission. Release C++20 -O2 -DNDEBUG, no exceptions/RTTI.
Three alternating processes per variant, seven samples of 256 diffs after warmup.
Every measured checksum is 6146 per diff; no concurrent builds/tests/sampling.
.NET uses three processes, seven samples of 16,384 diffs each, with
DOTNET_TieredCompilation=0 and DOTNET_ReadyToRun=0. Raw outliers are retained.

- Candidate C++ SHA-256: `525e915dd56a9c4588b804bc69f3cdbf7e112a1fd396b6ba493530f78d37730f`.
- Owned core SHA-256 (unchanged): `655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1`.
- DiffPlex DLL SHA-256: `652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`.
- DiffPlex PDB SHA-256: `4eac2d8ae6c2e7efaade8bca707bcbbe5673dc317cf141176fd4ce7b6ee285b8`.

Roslyn Release IL, source-version caveat and retained Unity/.NET code comparison:
[the v101 investigation](v101-profile-20261020.md). Dictionary.Find's typed field
reads, array lengths and element accesses remain source/library work; v103 removes
redundant generated checks around them without changing the library algorithm.

Compact evidence/scripts: `docs/performance/typed-proof-performance-20261020/`.
Large generated/native outputs: `artifacts/typed-proof-performance-20261020/`.
Local-borrow carrier elimination and inline dictionary-entry storage remain
unimplemented candidates and require separate measurement and correctness work.
