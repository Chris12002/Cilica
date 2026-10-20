# Why the measured .NET paths are faster

2026-10-20, investigation from `7cb76aa`; compiler/core remain `142aecd` / native
v115. No production code changed and no new speedup is claimed.

The same approved Roslyn Release IL enters the original .NET and Cilica
comparisons. Roslyn emits calls, branches, array operations and addresses. The
major differences below come from the selected library implementation, runtime
representation and native lowering. This is not evidence that Cilica needs a
different C# compiler.

## Actual code inspected

Captured fresh supported `DOTNET_JitDisasm` output from the original uninstrumented
host, separately for character and line workloads. Both checksums match.
Tiering and ReadyToRun remain disabled, matching the section investigation.
The ARM64 listings explicitly say **FullOpts, optimized code, No PGO data**.
Cold diagnostic execution times in capture logs are not benchmarks.

Also disassembled the freshly emitted, uninstrumented Cilica binary from the
section investigation, inspected the exact input IL, and statically decompiled
the **installed** .NET 10.0.11 CoreLib using ILSpy 9.1.0.7988. This avoids assuming
that the vendored v10.0.11 reference source is the exact installed source build:
the reference commit is `79d0c463...`, while the runtime reports `e2f47b0110`.
The original Roslyn compiler identity and DLL/PDB hashes remain those recorded
in the split-cache investigation; the retained Roslyn source checkout is not an
exact revision match. The actual Release IL is the lowering evidence here.

## 1. Substrings go straight into final storage

The installed CoreLib's InternalSubString:

1. Allocates the result string at its final length.
2. Copies UTF-16 directly from the source into that result using Buffer.Memmove.

Its caller preserves empty/full-length identity shortcuts and range checks.
The actual JIT Substring(int,int) body is 232 bytes and folds the internal helper
into its body. It computes source/destination payload addresses and the two-byte
character count around allocation/copy calls.

Cilica's CopySubstring currently allocates an `int[length]`, fills it through
indexed character reads, then calls FromCodeUnits to allocate and fill the string.
This adds an entire managed temporary, its backing storage, a copy pass and
collection pressure for each nontrivial substring. Cilica's final string payload
also uses its own runtime representation; it is not .NET's inline object layout.

The previous timers locate 42.35 µs in Cilica line chunking versus 4.54 µs in .NET.
They do **not** isolate how much of that gap this temporary causes, but this is
a concrete avoidable mechanism inside the largest measured region.

Recommended first implementation: a private, validated substring construction
operation that allocates final storage and copies directly, preserving caller
roots across allocation, exact UTF-16, range/fault order and existing identity
shortcuts. This should be general string/library support, not DiffPlex recognition.

## 2. Separator scanning works on direct character data

The installed string-array Split overload uses two stack-backed buffers of 128
Int32 entries each for positions and widths: 1,024 payload bytes before any
growth. The actual MakeSeparatorListAny JIT body uses direct `ldrh` character
loads, direct separator-array reads and integer loop state. It retains bounds
and length checks. Multi-character matches call sequence comparison.

Cilica now caches the first 64 pairs in a bounded managed array, so the earlier
complete rescan has already been addressed for this workload. The remaining
question is how cheaply it scans and constructs results, not whether to repeat
that completed optimization.

This observed overload's outer scan is scalar. Do not credit its measured speed
to hypothetical SIMD scanning or propose vectorization as the only explanation.
Stack-backed scratch may remove another allocation, but replacing per-result
temporary arrays is the clearer first target. Any stack storage change still
needs whole-call-chain validation under Cilica's constrained-stack contract.

## 3. Dictionary entries are contiguous values

The installed .NET Dictionary uses an `Entry[]` of structs. For the observed
reference-key/Int32-value instantiation, FindValue's native loop computes an
entry address using a **24-byte stride**. It reads hash/next/key fields there
and returns the address of the value field. There is no separately allocated
entry object to follow for each insertion or probe.

Cilica's DictionaryEntry is a sealed class. Its array holds references to those
objects, and Insert creates a new entry object. This introduces pointer chasing,
additional allocation and additional GC objects. Both implementations retain
bucket chaining and collision handling; this is a representation difference.

.NET also specializes recognized string comparers to its nonrandomized string
comparer. The captured hash body uses paired 32-bit loads and rotates, processing
four UTF-16 units per main iteration. Cilica adapts the same hash algorithm using
checked character indexing. The actual FindValue body uses multiply/shift
FastMod for bucket selection; Cilica's library source uses `%`.
Those are secondary candidates, not separately measured gains.

The .NET generic dictionary body still contains indirect comparer calls.
Likewise, BuildPieceHashes still has interface dispatch. Its advantage does
not require every call to have been devirtualized by profiling.

Compact entries are a promising broader design change, not a safe one-line
class-to-struct edit. Cilica's Find currently returns an entry object and its
mutation/enumeration code relies on that representation. A redesign must preserve
mutable/reentrant key callbacks, resize behavior, free-slot reuse, root release,
version semantics and faults. It should be evaluated separately from direct
substring construction.

## 4. Direct primitive-array loops and inlined field access

The actual .NET recursive BuildModificationData body reports 35 single-block
inlinees. Its common prefix/suffix loops use registers for indices, direct field
loads for HashedPieces and direct integer-array loads followed by comparisons.
Array bounds checks and fault branches remain.

The corresponding uninstrumented Cilica machine code still calls getters,
constructs array locations and uses array-location storage helpers. It also
maintains frame/exception state around these operations. This is visible in the
compiled instructions, not merely in unoptimized emitted C++.

| Selected method | .NET body bytes | Cilica body bytes |
|---|---:|---:|
| BuildPieceHashes | 664 | 3,976 |
| BuildModificationData, outer | 176 | 1,372 |
| BuildModificationData, recursive | 812 | 8,736 |
| CalculateEditLength, worker | 1,280 | 7,520 |
| CharacterChunker.Chunk | 260 | 1,880 |
| LineChunker.Chunk | 56 | 880 |

Cilica sizes run from the first through last disassembled instruction and may
include alignment. These are static body sizes, including cold/fault paths.
They do not include all callees and do not predict proportional speedups.
The dynamic region timing is the stronger prioritization evidence: the diff
algorithm costs 3.70 versus 0.20 µs on characters, 2.33 versus 0.14 µs on lines.

Candidate compiler direction: lower proven ordinary primitive array reads and
writes directly inside these loops, and preserve scalar values in native locals,
while retaining null/bounds/fault ordering and GC safepoints. Do not eliminate
public reference validation or assume all addresses/borrows are equivalent.
First establish which remaining helpers are required versus artifacts of the
general location representation.

## 5. Character conversion is inlined; strings are still fresh

The actual CharacterChunker JIT body inlines Char.ToString. Its fast loop:

- reads one UTF-16 unit directly;
- calls FastAllocateString for a one-character result;
- writes the character directly into the result;
- stores the result through the GC write barrier.

It hoists a destination-length guard out of the fast loop and retains a checked
fallback. There is no cache lookup replacing the character-string allocations.
Cilica already embeds one-character payloads, but its compiled loop still calls
the local-character helper and reference-element store helper. It must preserve
fresh identity; caching/interning characters would change the operation.

This explains concrete overhead opportunities inside the measured 7.63 versus
0.91 µs character-chunking region without claiming that any single change recovers
the full difference.

## Review and next slice

Review checked the hash-pinned input, installed runtime binaries, diagnostic
settings, method tokens, source/IL/native correspondence, actual surviving
bounds checks, absence of PGO, native helper calls and fresh string allocation.
No assembly under inspection was executed by the compiler; the previously
approved benchmark was executed separately by the .NET host for JIT capture.
Original comparator/player inputs and compiler/library source are unchanged.
No new release benchmark, implementation, sanitizer run or compatibility claim
is part of this read-only investigation.

Priority remains **direct substring construction**, followed by investigation
of compact dictionary entries and direct primitive-array lowering. Packed
hash loads/FastMod and stack scratch remain narrower follow-ups. All proposed
gains need isolated uninstrumented before/after measurements plus correctness,
GC and shared-module checks.

Evidence: [capture identity and commands](dotnet-speed-20261020/capture.json),
[character JIT](dotnet-speed-20261020/jit-0.txt),
[line JIT](dotnet-speed-20261020/jit-1.txt),
[original Release IL](dotnet-speed-20261020/input-methods.il),
[installed-runtime and owned-source excerpts](dotnet-speed-20261020/source-excerpts.json),
[Cilica prefix-loop instructions](dotnet-speed-20261020/cilica-prefix-loop.txt),
[native sizes/calls](dotnet-speed-20261020/native-method-summary.json),
and [inspection provenance](dotnet-speed-20261020/inspection-provenance.json).
Raw objdump output and complete decompiled runtime types remain under
`artifacts/dotnet-speed-20261020`.
