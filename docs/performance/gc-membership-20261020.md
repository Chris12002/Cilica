# DiffPlex: checked collector membership index

Recorded 2026-10-20 on macOS 15.7.3 ARM64, Apple M4 Pro. Extends the
[internal-reference result](internal-references-20261019.md).

## Result

| Fresh release build | Median per operation | Sample range | Samples |
| --- | ---: | ---: | ---: |
| Baseline: collector list searches | 531.794 microseconds | 525.286–567.785 microseconds | 21 |
| Candidate: scoped collector address index | 365.227 microseconds | 353.873–370.154 microseconds | 21 |

**1.4561x faster, or 31.32% less execution time.** All 42 timed checksums match;
no samples were removed. These are paired before/after measurements, independent
of earlier runs. The earlier ordinary .NET result was 5.106 microseconds/op,
approximately 71.5x faster than this candidate; the historical Unity result was
24.368 microseconds/op, approximately 15x faster. Neither comparator was rerun
here. The earlier .NET configuration disabled tiered compilation and ReadyToRun;
a future claim of outperforming .NET also needs a warmed normal tiered-PGO run.
NativeAOT is separate and is not represented by these figures.

## Change and trade-off

[ADR 0197](../decisions/0197-scoped-collector-membership-index.md) describes the
index. The collector validates its complete allocation inventory, then builds
a hash lookup of those addresses. Every root/edge still passes membership,
owner and canonical descriptor validation before its header is trusted. Failed
or incomplete marking still cannot sweep. The lookup is disabled before
reporting, sweeping and all exits; ordinary public boundaries retain list checks.

The native scratch buffer is reused. Allocation failure falls back to the same
checked linear search, without a managed fault. No managed object header, root
publication, allocation quota, library body or public ABI changes. Private
instance scratch fields advance the native emission profile from 90 to 91.

A separate diagnostic run measured **65,536 bytes** of retained lookup storage
and a 168-byte private Instance, 24 bytes larger than its previous field layout.
Scratch is outside the managed byte quota; at the default marking limit its
largest growth can temporarily hold 96 KiB across old and new buffers. Capacity
is retained until instance teardown. Larger custom marking limits can use more.
Every timed row still reports a 12,664-byte peak managed execution arena. That
arena metric and sampled post-operation heap sizes are not total process peaks.

## Identity and reproduction

Baseline: `6910eccaccec8efb4e96b0f788d7719b3fb63553`.
Candidate: `c29cb36f0b403ae12038e08d282ef42d6a8dde4f`.
Both descend from selected latest compiler tip `b8918ac`; local main remains
an ancestor. Core and library tree identities match across the pair.

Both C++ files were independently emitted from the corresponding compiler
sources using `ApprovedDiffPlexBenchmarkEmissionTests.Emit_approved_benchmark`.
The new baseline is byte-identical to the previous candidate. Full-file
comparison admits only ObjectCppRuntime.Text, ObjectCppMarking.Text and the
native profile comment. Every generated managed method and descriptor is
unchanged. Compiler/source hashes and tree identities are retained in
[source-review.json](gc-membership-20261020/source-review.json) and
[baseline-provenance.json](gc-membership-20261020/baseline-provenance.json).

Input remains the approved DiffPlex DLL, SHA-256
`652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`, upstream
`f500e73f28e28813f942f1f16dec5a34b40e48e3`. Workload: 128 UTF-16 characters,
mutations at 31/95, checksum 6146 and 523 observations. Input setup and full
result inspection remain outside timed operations.

The input DLL/PDB and retained Roslyn Release IL hashes were rechecked against
the [prior IL investigation](internal-references-20261019.md#follow-up-roslyn-il-and-source-inspection).
The PDB records Roslyn `5.9.0-1.26379.115+14fbf8d5271c98133561eb55185fdb05b286f578`,
Release, AnyCpu, C# 14.0. Its exact Roslyn source commit remains unavailable;
the earlier public source inspection was comparative, not a revision match.
BuildPieceHashes token 06000011 and getter token 0600004a retain the same IL
offsets and generated bodies. This change affects collector runtime work reached
at allocation pressure, not Roslyn lowering or source-library behavior.

Native compilation uses Apple Clang 17.0.0, C++20, O2, DNDEBUG, fno-exceptions,
fno-rtti and Wall/Wextra/Werror; no LTO or sanitizers for release timing. Both
diagnostic macros default to zero. Three processes per build alternate order;
each warms for at least 300 ms and produces seven samples of 64 operations.
No build, test or profiling process from this task ran during timing. The host
was not otherwise isolated; load is retained alongside all raw rows.

Exact commands, binary/source hashes and platform details are in
[provenance.json](gc-membership-20261020/provenance.json). Full generated sources,
binaries and logs remain in `artifacts/gc-membership-performance-20261020`.
After emitting both inputs on their respective revisions:

```sh
python3 tools/Performance/gc_membership.py prepare \
  --source /path/to/baseline.cpp --candidate-source /path/to/candidate.cpp \
  --baseline-revision 6910ecc --expected /path/to/expected.json \
  --output /path/to/new-output-directory
python3 tools/Performance/gc_membership.py validate --output /path/to/new-output-directory
python3 tools/Performance/gc_membership.py measure --output /path/to/new-output-directory
```

## Correctness, review and remaining cost

All **61 targeted checks** pass: 12 focused index/GC/internal-reference checks,
42 adjacent allocation/handle/borrow/array/exception/boxing checks and seven
module checks. Two fresh emission checks pass separately. Native probes include
O0/O2 ASan/UBSan, forced collection, a 4,096-node cycle, invalid inventory,
forged/foreign/interior pointers, stops/budgets/reentry, buffer reuse, failed
initial allocation and growth, empty heaps, reclamation and later allocation.

Baseline, candidate, diagnostic candidate with both verification flags enabled,
and sanitized candidate each match all 523 observations and checksum 6146.
Diagnostic and sanitized candidates pass separate forced-GC operations.
[Validation](gc-membership-20261020/validation.json) and
[test summaries](gc-membership-20261020/test-summary.json) are retained.

Review verified bounded sizing, a terminating empty hash slot, comparison before
dereference, activation after inventory validation, failure fallback, cleanup
before reporting/destruction and buffer teardown. No outstanding review findings.
The timing harness rejects unrelated generated-code changes and checksums; its
measurement loop is shared with the preceding experiments.

A separate O2-g candidate capture sampled for three seconds at 1 ms after one
second of an 8,192-operation-per-sample run. All seven diagnostic-run checksums
match. It reports 1,934 exclusive application samples; visit_edge and
visit_object_references now account for three combined, versus 956 of 2,117 in
the previous capture. This is source/symbol attribution from separate samples,
not an exact collector cost reduction: inlining and folded symbols can attribute
descriptor checks elsewhere. Index construction accounts for 15 samples.

Remaining visible costs include descriptor acceptance scans, generated owned-core
methods and borrowed/root-frame lookup. DiffPlex's seven-byte getter still has
five generated steps and accounts for 43 samples. The next architectural target
is direct native execution for proven simple methods, avoiding unnecessary
frame/step machinery while preserving null/fault order and roots across allocations.
No speedup from that future change is claimed. Compact profile counts, memory
observations and raw-source/binary/sample hashes are in
[profile-summary.json](gc-membership-20261020/profile-summary.json).
