# DiffPlex execution improvements, 2026-10-20

Apple M4 Pro, macOS ARM64, Apple Clang 17. Each slice uses freshly emitted
candidate C++, O2/NDEBUG, C++20, no native exceptions/RTTI. Sanitizers and forced
collection are separate correctness runs. No hosted work or services are used.

Each timing comparison alternates three process pairs with 300 ms warmup and
seven samples of 256 diffs per process. All 21 samples per binary are retained.
The complete 523-value transcript must match before timing; checksum is 6146.
`tools/Performance/execution_slices.py` retains exact sources, compiler commands,
binary/source hashes, validation, raw samples and host load. Candidate source
hashes identify uncommitted implementation at measurement; final commits follow
review. No generated-method substitutions are used.

| Slice | Before µs/diff | After µs/diff | Speedup |
| --- | ---: | ---: | ---: |
| Direct native control flow, v92 → v93 | 365.333 | 243.936 | 1.4977x |
| Borrow lookup, v93 → v94 | 246.020 | 231.181 | 1.0642x |

## Direct flow

Baseline compiler df37feb (compiler implementation unchanged from b44dbc6),
independently emitted during the preceding investigation. The current branch's
v93 candidate is regenerated, including after the review correction; the two
candidate files match byte for byte. No local exception regions occur in the
144-method DiffPlex closure. All are eligible for direct flow.

[ADR 0199](../decisions/0199-direct-native-control-flow.md) records the design,
review and correctness checks. [Summary](execution-slices-20261020/direct/summary.json),
[provenance](execution-slices-20261020/direct/provenance.json),
[validation](execution-slices-20261020/direct/validation.json) and raw samples are
retained beside this report. Raw C++/binaries remain under
`artifacts/direct-control-flow-20261020`. Review checks pass 23 distinct tests.

## IL and comparator continuity

The unchanged DiffPlex DLL is SHA-256
`652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`;
its PDB is `4eac2d8ae6c2e7efaade8bca707bcbbe5673dc317cf141176fd4ce7b6ee285b8`.
The [preceding investigation](runtime-costs-20261020.md) retains Roslyn Release IL,
compiler options/version and method-token/offset tracing. BuildPieceHashes
06000011 IL_007d/007f remains a managed borrow passed to TryGetValue; Chunk
06000021 IL_001c remains Char.ToString. These source operations are unchanged by
control-flow rendering. IL2CPP emits ordinary native branches and typed byrefs;
its generated Char.ToString uses direct string allocation. Roslyn source is a
comparative revision, not an exact match to the unavailable compiler commit.
The preceding .NET non-tiered measurement is 5.303 µs/diff; Unity's 24.368 µs
measurement is historical (October 8). Neither is presented as a fresh per-slice
comparator. Current native changes still leave a substantial gap to .NET.

## Borrow lookup

Baseline de4b886, fresh v94 candidate. Paired medians are 246.020 to 231.181
microseconds/diff (1.0642x). Four full transcripts and two forced-GC
operations pass; diagnostic verification rechecks both lifetime chains.
[Summary](execution-slices-20261020/borrow/summary.json) and
[provenance](execution-slices-20261020/borrow/provenance.json) retain the details.
Raw output is in `artifacts/borrow-lookup-20261020`. Forty-five distinct focused
checks pass across the final corrected runs; retained logs show the initial host
stack-harness failure and its separate passing rerun. The 2 KiB bound is unchanged.
See [ADR 0200](../decisions/0200-cached-borrow-registration.md).
