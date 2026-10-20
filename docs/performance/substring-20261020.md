# Direct substring construction: native v116

2026-10-20, `codex/profile-v107`, implementation based on `b5ff3a5`.
The four approved targets are saved in
[the performance roadmap](next-targets-20261020.md). This completes its first
slice; compact dictionary entries, primitive-array/native-local loops and
character construction loops remain queued.

## Measured result

Whole-workload, uninstrumented release medians in microseconds per diff:

| Workload | Before | v116 | .NET | Unity IL2CPP |
|---|---:|---:|---:|---:|
| Character | 23.808 | 23.569 | 5.471 | 25.670 |
| Line | 54.518 | 39.502 | 6.987 | 25.394 |

Line execution takes **27.54% less time**. The first independent paired batch
measured 54.167 → 39.957 µs, a 26.23% reduction. Character timings overlap and
are treated as unchanged. The final line result is 5.65× .NET and 1.56× Unity;
character is 4.31× .NET and about 8.2% less time than Unity.

Each cell contains 21 samples from three fresh processes, seven samples per
process. Native batches use 1,024 operations per sample, .NET 8,192, Unity 4,096;
warmup is at least 300 ms. Process order rotates/reverses. No test, build,
profiler or instrumented timing overlaps release measurement.

Apple M4 Pro/macOS ARM64, Apple Clang 17, C++20 `-O2 -DNDEBUG -fno-exceptions
-fno-rtti`. .NET 10.0.11 runs with tiering and ReadyToRun disabled, as in the
prior investigation. Unity 6000.3.2f1 uses the retained uninstrumented Control
player from the section comparison, with its ARM64 slice. Unity was retimed,
not rebuilt. Its controls contain no section scopes. No NativeAOT result is
included, and no claim is made about other hardware, libraries or workloads.

## Change and semantic boundary

CopySubstring now invokes one exact private three-argument construction
intrinsic. It allocates final string storage and copies UTF-16 directly from
the rooted source. It removes the temporary Int32 vector, its separate backing
allocation, per-element managed accesses and second copy. Public validation,
range/error ordering, identity shortcuts and exception text remain unchanged.

Source and result retain ordinary frame roots. Native range checks precede
pointer arithmetic; zero-length copies use the canonical empty string.
The non-moving collector permits a rooted source payload address to survive
allocation/retry. Quotas, allocation observers, diagnostic switches and public
ownership checks remain active. This reduces memory use as well as work rather
than spending a larger stack or heap budget.

Native v116, object IR v80, integer-object CIL v127; object layout and public
ABI remain unchanged. See [ADR 0224](../decisions/0224-direct-substring-construction.md).

## Roslyn, .NET and Unity trace

The application input is unchanged:

- DLL SHA-256 `652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`.
- PDB SHA-256 `4eac2d8ae6c2e7efaade8bca707bcbbe5673dc317cf141176fd4ce7b6ee285b8`.
- Its retained PDB records C# 14 Release/AnyCPU and Roslyn
  `5.9.0-1.26379.115+14fbf8d5271c98133561eb55185fdb05b286f578`.
- Fresh owned core SHA-256
  `05c15eedddf2ed6e1a54d2b081a47b93d1308a7ccbc27578ec231765327a01fe`.
  Its retained build options specify optimized deterministic C# 9, no standard
  library, no unsafe code and no debug symbols; compiler inputs are pinned in
  its build manifest.

The fresh core's CopySubstring is token `060002d9`, nine IL bytes:
`ldarg.0`, `ldarg.1`, `ldarg.2`, `call FromSlice` at IL_0003, `ret`.
FromSlice is token `06000235`. The two public overloads are `060002d5` and
`060002d6`. The new operation maps through both IR stages into
`string_from_slice` with that method/IL fault origin.

The actual release native body contains direct UTF-16 loads/stores and Clang's
vectorized copy path with a scalar remainder. It no longer calls a managed
temporary-array construction/copy loop. This does not imply the surrounding
Split separator scan is vectorized.

The installed .NET CoreLib inspection already showed FastAllocateString followed
by Buffer.Memmove; the actual JIT folds it into Substring. The retained Unity
Control player's generated `mscorlib.cpp:28952` likewise calls FastAllocateString,
then wstrcpy directly into the final string. The excerpt and file hash are
retained here. Cilica adopts the direct-copy strategy with its own allocator,
rooting, quota and fault machinery. The Roslyn reference source checkout is not
the exact compiler revision; actual emitted Release IL supplies the evidence.
See [the previous actual-JIT investigation](dotnet-speed-20261020.md).

## Correctness and review

Forty-eight distinct focused checks pass:

| Test class | Checks |
|---|---:|
| SubstringConstructionTests | 13 |
| SubstringTests | 9 |
| SplitCacheTests | 5 |
| CharacterConstructionTests | 8 |
| StringBuilderLibraryTests | 8 |
| EmbeddingStringTests | 5 |

Existing substring differential checks retain 4,356 repeated O0/O2 comparisons
over boundary/overflow ranges, NUL and surrogates, identity, null receivers and
composed copies, plus explicit direct-call null semantics. New native tests
cover every nonempty range of a six-unit boundary string, source survival with
no external root, exactly one allocation, object/byte exhaustion, exact fault
origin, catch/recovery, private invalid ranges and canonical empty output.
The separator-cache suite checks cross-unit execution with forced GC and
sanitizers. Eight malformed packet variants fail both detached validators.

The final native harness class passes after correcting two harness assumptions:
exception materialization can allocate, and roots are Context-specific.
Initial failures and their subsequent passing run remain in local artifacts.
All other selected checks passed on their recorded runs.

Both complete benchmark observations (523 character, 2,061 line values) match
the .NET oracle in release and O1 ASan/UBSan modes with internal/borrow diagnostics
off and on. Sanitized modes force GC before allocation. Each uses a requested
512 KiB thread stack, checks the actual 512–576 KiB size and repeats collector
cleanup/failure invariants. Maximum recorded frames are 544/448/480 bytes for
release/optimized-sanitized/diagnostic-sanitized. The timing binary's size is
599,432 bytes. Comparator outputs were verified again before their timing.

Review found no outstanding implementation issues. Checks covered declaration
access, operand ordering/types, fault edges, allocation classification, roots,
unsigned overflow, empty pointers, unchanged quotas, shared linkage and fallback
paths. Local main is an ancestor; no integration or hosted work was needed.

## Retained evidence

The adjacent `substring-20261020/` directory contains raw timings, summaries,
source hashes, compiler options, selected IL/metadata, Unity excerpt, validation
logs and reproduction scripts. Full generated C++, binaries, core build
manifest, full IL and native disassembly remain under
`artifacts/substring-20261020/`. Baseline is the byte-identical uninstrumented
section-control emission of committed `142aecd`. Compiler-source hashes were
checked during emission and again before acceptance commit.

Compact dictionary entries are next. The roadmap also retains closed-world
lifetime, storage-specialization and operation-fusion hypotheses. These may
offer advantages beyond the observed comparator paths; none is yet an
implemented improvement or a demonstrated novel result.
