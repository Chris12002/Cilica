# Compact dictionary investigation

2026-10-20, based on committed `b171fd73669fc3de4a5f99ffc0137f5ce1a86106`
in `codex/profile-v107`. **No implementation from this experiment is accepted.**
The working compiler/core retain native v116 and the reference-entry dictionary.
The next implementation target is array/native-local lowering; compact storage
will be revisited after its prerequisites. See [ADR 0225](../decisions/0225-compact-dictionary-columns.md).

## Results and decision

Preliminary paired release medians, microseconds per complete diff:

| Experiment | Character before → candidate | Line before → candidate |
|---|---:|---:|
| Inline Entry struct array | 23.514 → 118.902 | 39.254 → 89.548 |
| Separate hash/link/key/value columns | 23.658 → 23.573 | 39.487 → 38.730 |
| Columns plus generated-code store inlining | 23.578 → 23.366 | 39.660 → 38.845 |

The inline layout is a clear regression. Scalarized field reads become
AddressArraySlot/LoadLocation operations, introducing generic managed-borrow
work and excluding the lookup from the existing typed-kernel proof. Merely
copying .NET's physical layout does not copy its machine-code efficiency.

Columns restore typed lookup execution, but the small timing differences overlap
noise and do not demonstrate a substantial gain. Store inlining was a retained-C++
diagnostic experiment only; the compiler was not modified. The first column
batch's baseline raw timing files were overwritten by the store experiment;
its summary remains preliminary. This is not release acceptance evidence.

A separate warmed allocation diagnostic, with collection before and after one
operation and enough budget to avoid collection during it, measured:

| Workload | Objects before → columns | Allocated bytes before → columns |
|---|---:|---:|
| Character | 312 → 300 | 34,216 → 34,280 |
| Line | 188 → 175 | 25,192 → 25,176 |

Retained live bytes/objects after collection were identical. The extra fixed
storage owner and arrays nearly cancel the byte savings for these dictionaries.
Fewer allocations alone are insufficient to justify the representation change.

Most decisively, the broader tests reject `Dictionary<UInt64, …>` after the
change: direct UInt64 arrays are not admitted by ValueProgramDiscovery.Vector,
although UInt64 fields inside the old entry objects are supported. Existing
DictionaryProbe.WideKeys and DictionaryAddReadProbe.UInt64 expose this regression.
Adding array semantics or silently weakening type identity is outside this
representation experiment. The candidate was restored out of production code.

## Roslyn, .NET and Unity comparison

The application DLL/PDB and compiler options are unchanged from the
[substring slice](substring-20261020.md). Roslyn's actual optimized owned-core IL
was captured for each candidate. The final column core SHA-256 is
`3558afedffdfd7e28a2d1126ea97242ea044a1a6c0414e0ec30eab0a37978832`;
the timed preliminary column core is
`e1d8db03c6c0c99176b652281a3696738a27a0109d78d4cb6ac4d64f4917cfc2`.
Both use the pinned deterministic optimized C# 9 invocation, no unsafe code
and no debug symbols. Application DLL SHA-256 remains
`652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`.
The retained Roslyn source checkout is not the exact compiler revision; the
emitted IL is the direct evidence.

In the final candidate, Find is token `060004a1`, 139 IL bytes.
It captures storage at IL_0030, reads hash/key/link arrays at IL_0041,
IL_004d and IL_0066, and returns owner plus index. TryGetValue, token
`060004a2`, reads the value at IL_0015. Fresh generated C++ contains typed
Find/TryGetValue/Hash/Equal kernels for the benchmark's closed specialization.
The actual ARM64 disassembly and both typed/framed Find bodies are retained.

Installed .NET 10.0.11's FindValue returns a reference into its captured Entry
array. TryInsert captures entries before hashing, while FindValue captures them
after hashing. Equality callbacks can resize, overwrite, remove or clear entries;
subsequent reads must still use the appropriate captured storage. Removed
scalar-only values remain in freed slots; reference-containing values are cleared.
The candidate's probes preserve these observable behaviors.

Unity's retained Control output uses a contiguous Entry struct array and direct
indexed fields in `Dictionary_2_FindEntry_m68D71E2…`, at
`Generics__3.cpp:5608`. Its older library returns an index and captures arrays
earlier than .NET 10; it is representation guidance, not the semantic oracle
for callback ordering. Neither comparator was rebuilt or retimed for this
rejected experiment.

## Validation and review

Of 155 final candidate checks, 141 pass and 14 fail. The failures fan out from
the UInt64 rejection in two shared fixture setups; they are not 14 independent
semantic defects. Forty-nine selected checks pass across the new callback/allocation/shared
module probes, dictionary enumeration, view enumeration, copying and view
interfaces. They include O0/O2 ASan/UBSan, forced collection, 456 repeated new
callback comparisons and 72 additional shared-module callback comparisons.
All six growth-allocation failure positions preserve the original dictionary
and recover after the quota is restored. Inserting into preallocated capacity
needs no allocation. Copy/enumeration review removed unnecessary whole-entry
snapshots and adjusted physical GC counts without weakening root-release checks.

Both complete DiffPlex outputs (523 character and 2,061 line values) match in
release and two O1 sanitizer configurations, diagnostics off/on, forced GC,
and 512–576 KiB native stacks. Maximum reported native frames are 544/480/512
bytes for those three builds. These successes do not supersede the UInt64
compatibility failures in the broader checks.

After restoration, both O0/O2 dictionary growth/collision/value-copy/mutating-key
checks pass. Compiler, library and test sources match `b171fd7`. Fresh core
SHA-256 is again `05c15eedddf2ed6e1a54d2b081a47b93d1308a7ccbc27578ec231765327a01fe`,
and regenerated benchmark C++ is byte-identical to accepted v116
(`2d5bb7c06e4e2941fa4f25ca5d88221aef6465c7614bc6724f9b7c6c9062822a`).
Review verified that only the report, decision, roadmap and retained evidence
remain changed. No new performance or compatibility claim is made.

Measurements use Apple M4 Pro, macOS ARM64, Apple Clang 17, C++20
`-O2 -DNDEBUG -fno-exceptions -fno-rtti`, 1,024 operations per sample,
300 ms warmup, three fresh processes and seven samples per process.
No validation/build activity overlaps the preliminary timing batches.

## Retained evidence

Small evidence and the unapplied implementation/test patch are committed under
`docs/performance/dictionary-compact-20261020/`. Large emitted C++, binaries,
core IL and disassembly remain in
`artifacts/dictionary-compact-20261020/`.
`columns-experiment.patch` applies to the implementation baseline above; its
tests are intentionally not active after restoring the production library.
The inline candidate and store-inlining diagnostic remain separately identified.
The final candidate was freshly emitted after enumeration review but was not
retimed after broader correctness checks rejected it.
