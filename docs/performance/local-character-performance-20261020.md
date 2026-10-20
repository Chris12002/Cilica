# DiffPlex: direct values for proven local character reads

2026-10-20. Base compiler `9eeb403`, native v103 -> v104. Apple M4 Pro,
macOS ARM64, Apple Clang 17, .NET 10.0.11. Implementation: ADR 0211.

## Measurement

| Backend | Median us/diff | Samples |
| --- | ---: | ---: |
| Cilica v103 | 48.784 | 21 |
| Cilica v104 | 44.913 | 21 |
| Ordinary .NET, non-tiered | 5.350 | 21 |

The change reduces paired execution time by **7.93%**, leaving a
**8.39x** ratio to the separately rerun .NET comparator. Unity was not
retimed. These are this workload's macOS ARM64 measurements.

The emitted CharacterChunker loop now skips frame_location, location-result/owner
publication, packet copying and runtime borrow resolution for the proven local
read. The private helper forwards the UInt16 value while retaining both real
managed GC frames. Caller frame/borrow registration and allocation behavior stay
unchanged. This is a compiler proof, with no benchmark-name or token matching.

## Roslyn and comparator evidence

The actual pinned Release IL at CharacterChunker token 06000021 stores the
character in local 2 at IL_0019, takes its address at IL_001a and calls the readonly
character instance method at IL_001c. Retained Unity DiffPlex.Benchmark.cpp:2890
passes &V_2. Its mscorlib__1.cpp:20367 wrapper reads UInt16 and calls the scalar
factory. The pinned .NET Char.cs:158 wrapper similarly forwards m_value to its
scalar factory. Roslyn EmitAddress.cs:288–315 emits a real local address when the
local has storage; its source checkout is comparison evidence, not a verified
revision match to the compiler that produced the benchmark DLL.

Cilica's equivalent call now passes the local bits directly because the adjacent
consumer's exact body proves that only a scalar read precedes allocation. General
managed locations retain their existing representation and validation. Exact input
and comparison-source hashes are in reference-sources.json. The prior investigation
retains the broader Roslyn compiler/options and owned-core IL evidence.

## Correctness and review

The final focused set has 45 distinct passing checks: the 41-check run plus four
additional native call-site variants. A four-case post-review run also verifies
marking-time fallback. Classes cover NativeCharacterHelperTests,
NativeTypedBorrowTests, CandidateCharacterTextTests, NativeInlineStringTests and
NativeStringTests. O0/O2 native execution includes ASan/UBSan, forced collection,
UTF-16 units/surrogates, fresh identity, aliases, stale/foreign borrows, managed
faults, allocation failure/recovery and separate translation-unit linkage.

Four valid-IR mutations verify that reuse, escaping into a location local,
intervening work and parameter addresses retain the ordinary path. Four new
private-helper variants exercise diagnostic off/on, real frame counts, unchanged
pending faults, successful depth-64 allocation, depth overflow, marking rejection
and original factory allocation-fault origins. Four actual generated-call-site
variants cover dispatcher/direct execution with diagnostics off/on. The first
private harness omitted predicates for its two reference roots, causing borrow
registration to reject the harness; supplying those predicates repaired it. No
production change was made to weaken registration.

All 523 DiffPlex observations match the retained oracle in release and two
forced-GC O1 ASan/UBSan builds. The optimized sanitizer run sets internal-reference
and borrow-lifetime verification to 0, exercising the direct-local path. The
second sets both to 1, exercising original carriers and full diagnostics. Both
run with CILICA_TYPED_INTERNALS=1 and direct control flow. The requested pthread
stack is 512 KiB; actual size is checked within 512–576 KiB. Largest individual
frames remain 432 bytes release and 320 bytes sanitized. No final sanitizer errors.

Self-review checked validation-before-analysis, every admitted location consumer,
single-use/edge rejection, bounded proof work, adjacency and guard stability,
initialized fallback storage, root/frame lifetimes, partial-failure behavior,
marking/depth guards and native driver splitting. No escaping native pointer or
GC safepoint is introduced between the address and call. Compiler source hashes
still match the measured candidate after validation; no emitter rollback occurred.

## Reproduction

The freshly rebuilt baseline reproduces the committed v103 acceptance C++ byte
for byte. Candidate emission checks compiler hashes before/after generation.
Release uses C++20 -O2 -DNDEBUG, no exceptions/RTTI; three alternating processes
per variant, seven samples of 256 diffs after warmup. Every measured checksum is
6146 per diff. No builds, tests or sampling ran concurrently with timing. Raw
outliers are retained. .NET uses three processes and seven samples of 16,384 diffs,
with DOTNET_TieredCompilation=0 and DOTNET_ReadyToRun=0.

- Baseline C++ SHA-256: `525e915dd56a9c4588b804bc69f3cdbf7e112a1fd396b6ba493530f78d37730f`.
- Candidate C++ SHA-256: `fd0d4229903be9515b7603bc2a6a6c48bd021f9824e34bae6309882395a78d96`.
- Owned core SHA-256, unchanged: `655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1`.
- DiffPlex DLL SHA-256: `652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`.
- DiffPlex PDB SHA-256: `4eac2d8ae6c2e7efaade8bca707bcbbe5673dc317cf141176fd4ce7b6ee285b8`.

Compact evidence/scripts: docs/performance/local-character-performance-20261020/.
Generated sources/native executables: artifacts/local-character-performance-20261020/.

The disk filled before the first source edit. Four reproducible sanitizer binaries
from the completed cache/type-proof runs were removed, preserving their sources,
commands and results. Forty completed C++ artifacts from the v101 investigation,
remaining-gap and character-performance runs were losslessly compressed to .cpp.gz,
freeing 246 MiB; each decompressed SHA-256 was verified before removing its plain
copy. compressed-prior-artifacts.json records exact paths and hashes. Decompress
those files before replaying an older script that expects a plain .cpp path.

General local-borrow transport, writable outputs and inline dictionary-entry
storage remain separate candidates. Reprofile the remaining gap before choosing
the next change; this slice does not claim to remove all borrow overhead.
