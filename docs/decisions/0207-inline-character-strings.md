# ADR 0207: Single-allocation character strings

Status: accepted. Native v100. Recorded 2026-10-20.

The v99 DiffPlex profile attributed 38% of sampled stacks to the character-string
construction subtree. Both retained .NET native code and Unity IL2CPP source place
character data in the string allocation. Cilica allocated a header and a separate
UTF-16 buffer even for one code unit.

Store exactly one code unit in a real UInt16 member after StringObject.length.
Its position can use existing alignment padding; correctness and accounting use
native sizeof rather than assuming a particular target layout. Empty strings
keep a null buffer; longer strings retain their separately allocated buffer.
Destruction compares the buffer pointer with the embedded member before delete[].
No flexible-array arithmetic, interning or character cache is introduced.

All string construction uses the same allocator, including literals, concatenation
and character-array copying. One-unit strings charge sizeof(StringObject), which
already includes their payload; longer strings additionally charge their buffer.
Each uncached construction still creates a fresh identity. Allocation safepoints,
retry/collection, checked lengths and publication after filling are preserved.

Review covered standard-layout provenance, const exposure, disposal, partial
allocation failure, actual byte charging and source roots across collection.
Sixteen focused checks pass: native string semantics, character-array construction,
public string embedding and new O0/O2 ASan/UBSan storage probes. The latter exhaust
all 65,536 UTF-16 code units, verify exactly one allocation per fresh character,
exercise each allocator input position, force collection and inject header/buffer
allocation failures. Exact heap limits and instance-local literals remain checked.

Fresh compiler emission passes all 523 DiffPlex observations. Three alternating
process runs with seven samples each measure v99 at 72.635 us/diff and v100 at
65.023 us/diff, 10.48% less time on Apple M4 Pro, Clang 17, C++20 -O2 -DNDEBUG.
The largest release native frame remains 432 bytes. This is one workload/target.
See artifacts/character-performance-20261020 for local raw evidence; compact
provenance is retained in docs/performance/character-performance-20261020.
