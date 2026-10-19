# DiffPlex: internal reference membership verification

Recorded 2026-10-19 on macOS 15.7.3 ARM64, Apple M4 Pro. This is the current
performance result on `codex/runtime-performance`, extending the
[allocation-verification result](latest-diffplex-20261019.md).

## Result

| Fresh release build | Median per operation | Sample range | Samples |
| --- | ---: | ---: | ---: |
| Baseline, allocation scans already disabled | 15.480775 ms | 15.142517–16.194790 ms | 21 |
| Candidate, internal membership scans diagnostic | 0.533274 ms | 0.525827–0.561313 ms | 21 |

**29.0297x improvement**, with all 42 timed checksums valid. No samples were
removed. These are paired current-branch measurements; multiplying earlier
speedup ratios from separate runs would not be an additional paired result.
The same-session earlier ordinary .NET reference was 5.106 microseconds/op,
so the candidate remains approximately 104x slower. The historical Unity result
was 24.368 microseconds/op (about 22x faster than this candidate). Neither
comparator was rerun for this slice; NativeAOT is not included in these numbers.

## Identity and method

Baseline compiler revision: `6ede4de47014556045d2dba903f862fd7333b6ad`.
Candidate compiler revision: `479176c` (full hash and source hashes in
[source-review.json](internal-references-20261019/source-review.json)). Both descend
from the latest compiler branch tip selected for this work, `b8918ac`; the
`codex/alchemy-replay-baseline` branch remains there and main is an ancestor.
Both generated files identify native profile 90. The owned library tree is
unchanged and rebuilt by the emission fixture.

The fixed reviewed DiffPlex input assembly SHA-256 is
`652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`, upstream pin
`f500e73f28e28813f942f1f16dec5a34b40e48e3`. The workload uses 128 UTF-16 characters,
substitutions at 31/95, checksum 6146 and 523 result observations. Input setup and
full result traversal are outside the timed operation.

Baseline and candidate C++ were emitted independently before and after the source
change. The baseline re-emission also matches the previous slice's candidate
byte-for-byte. An exact full-file comparison permits only the diagnostic macros,
runtime/borrow helper fragments and 550 emitted ownership guards to change.
Descriptors, managed method operations and owned-library identities remain the
same. An unrelated guard substitution fails that comparison.

Release flags: C++20, O2, DNDEBUG, fno-exceptions, fno-rtti, Wall/Wextra/Werror;
no LTO or sanitizers. Exact compiler version, commands and all binary/source
hashes are in [provenance.json](internal-references-20261019/provenance.json).
Both diagnostic flags default to 0 for timing. Collection on normal heap pressure
remains enabled; forced collection is disabled. Three processes per build alternate
order, each warms for at least 300 ms and produces seven samples of 64 operations.
The larger batch than the previous slice provides about 34 ms per candidate
sample. Raw results and host load are retained; no competing validation/build
process was running during measurement, but the host was not otherwise isolated.

## Implementation and correctness

[ADR 0196](../decisions/0196-internal-reference-verification.md) records the trust
boundary. Generated receiver/argument, field, array, cast and type-test guards,
reference-array stores and managed borrows skip repeated allocation-list scans.
`CILICA_VERIFY_INTERNAL_REFERENCES=1` restores private pointer diagnostics.
Null/type/bounds/covariance checks, readonly storage and borrowed frame lifetimes
remain. Public handle admission/results and GC tracing retain checked membership.
No root publication, heap layout, native frame budget or ABI changes are made.

All **104 targeted tests** pass: 15 focused mode/object/array/borrow checks,
83 adjacent runtime/GC/embedding/module checks and six ordinary boxing/delegate
composition checks. The object and array corpus runs both modes at O0/O2 with
ASan/UBSan. Explicit private forged-pointer harnesses select diagnostic mode.
New boundary tests cover invalid pointers, foreign owners, stale handles,
cross-instance resolution and invalid roots without changing collector behavior.
The malformed macro value 2 is rejected at native preprocessing.

Both explicit emission tests pass. Baseline, normal candidate, diagnostic
candidate (both verification flags enabled) and ASan/UBSan normal candidate
match all 523 observations and checksum 6146. Separate forced-GC operations pass
for diagnostic and sanitized candidates. The peak execution-frame storage reported
by every timed row is 12,664 bytes. Normal collection still makes the sampled
post-operation heap/object counts vary; they are not memory-peak measurements.

A fresh pre-change sample of the profile-90 baseline attributed 2,213 of 2,311
exclusive application samples (95.76%) to source lines containing ownership
scans. This is one optimized source-line attribution capture, not an exact
instruction-cost percentage. Raw sampling output stays in the local artifacts;
its hash and a compact breakdown are retained with this record.

## Reproduction and review

Full C++, binaries and logs are local in
`artifacts/internal-reference-performance-20261019`. Retained report data lives
in [internal-references-20261019/](internal-references-20261019/).

Generate each C++ file with `ApprovedDiffPlexBenchmarkEmissionTests.Emit_approved_benchmark`
on its matching compiler revision using the approved input pin, DLL and current
`tools/CompatibilityCandidates/diffplex-benchmark/native-host.cpp.template`.
Then run:

```sh
python3 tools/Performance/internal_reference_verification.py prepare \
  --source /path/to/baseline.cpp --candidate-source /path/to/candidate.cpp \
  --baseline-revision 6ede4de \
  --expected docs/performance/internal-references-20261019/expected.json \
  --output /path/to/new-output-directory
python3 tools/Performance/internal_reference_verification.py validate \
  --output /path/to/new-output-directory
python3 tools/Performance/internal_reference_verification.py measure \
  --output /path/to/new-output-directory
```

Review checked every shortcut call site, checked handle/result/GC paths,
short-circuit order before header access in diagnostic mode, rooting across
allocation, array store exception order and native borrow lifetimes. The runner
rejects unexpected generated-code changes and changed prepared artifacts. It
requires validation before timing and rejects even an empty GC-stress environment
variable. No hosted work or service changes were needed.

Remaining performance costs must be profiled on this candidate. Exceptions/cache
validation and some boxed/delegate checks still perform membership scans; generated
step dispatch, borrow bookkeeping and collection also remain. This result does
not establish .NET/IL2CPP parity or performance on other workloads/platforms.
