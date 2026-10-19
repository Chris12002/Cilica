# Allocation root verification: first performance slice

Historical retained-code replay. Use the
[fresh latest-compiler result](latest-diffplex-20261019.md) for current performance decisions.

Measured 2026-10-19 on Apple M4 Pro, macOS 15.7.3 ARM64, Apple Clang 17.
The runtime change is [ADR 0195](../decisions/0195-allocation-root-verification.md).

## Result

| Backend | Median per operation | Observed range | Samples |
| --- | ---: | ---: | ---: |
| Retained DiffPlex, original allocation scans | 138.849 ms | 122.202–192.374 ms | 21 |
| Same generated workload, allocation scans diagnostic only | 15.031 ms | 13.533–16.116 ms | 21 |
| Ordinary .NET 10.0.11 JIT, fresh comparator | 5.106 us | 5.029–5.282 us | 21 |

The paired runtime experiment improves median execution by **9.237x**, removing
89.17% of elapsed time. Every timed checksum matches. The candidate remains about
2,944x slower than the fresh .NET comparator, so this is a first improvement, not
competitive execution. The historical Unity IL2CPP median was 24.368 us on this
workload; it was not rerun here and is not a contemporaneous comparison.

The fresh baseline is different from the earlier 184.648 ms recording. The 9.237x
ratio uses only this run's paired baseline and candidate, not the older number.
Host load was nonzero and recorded; ranges and all samples are retained rather
than discarding slower runs. No timing threshold is added to ordinary tests.

## Scope and method

The original `codex/milestone-7-c-abi` compiler/library predates the fuller DiffPlex frontend support in
the retained benchmark. The experiment therefore applies the actual production
macro/helper and five allocation-site replacements to the reviewed generated
C++ from revision `49dd0b1`, SHA-256
`4c5e4f743f67590ba849e3d0b91414b7229a09ee03f5bfab583c08550b5ae1da`.
All other runtime and generated method bodies remain fixed. This is a measured
runtime-change replay, not a new end-to-end DiffPlex compilation by that branch.
Its compiler output was exercised separately by the native tests below. This
record remains historical when carried to the newer compiler branch.

The input is the existing 128-character DiffPlex workload with two substitutions.
Input construction and full output traversal stay outside the timed managed loop.
Each operation contributes checksum 6146. Full validation compares all 523 output
observations against the retained transcript and a fresh .NET execution of the
same pinned workload assembly.

Both native timing binaries use C++20, O2, NDEBUG, no RTTI/exceptions or sanitizers.
There are three fresh processes per variant, alternating baseline/candidate order,
seven samples each, eight operations per sample, and at least 300 ms warmup.
.NET uses three processes, seven samples each, 32,768 operations/sample, with
tiering and ReadyToRun disabled. It is ordinary JIT execution, not NativeAOT.
Diagnostics and tests finished before native timing; .NET ran afterward.

## Correctness and review

- 62 targeted tests pass, including four new O0/O2 ASan/UBSan mode combinations.
- Original, candidate, verification-enabled and sanitized DiffPlex binaries each
  match all 523 observations and checksum 6146.
- Sanitized normal-mode and verification-mode DiffPlex both pass a separate
  forced-collection operation. These stress checks are not timing samples.
- New native tests cover all five allocation paths, observer ordering, unreadable
  root injection, foreign roots under pressure/stress, no reclamation after invalid
  collection and survival of live roots. Existing tests cover public handles,
  arrays/strings, boxing/values, frame limits, static storage and exceptions.
- Review found no new pointer dereference, root removal, ABI/layout change or
  allocation. Collection and boundary validation remain enabled. Private
  corruption detection on every ordinary allocation is deliberately no longer a
  guarantee; the compile flag or private observer restores that check.

## Reproduction and retained evidence

From the repository root, supply the retained source and transcript paths and a
fresh output directory:

```sh
python3 tools/Performance/allocation_verification.py prepare \
  --source /path/to/retained/native-bench.cpp \
  --expected /path/to/retained/verify.jsonl \
  --output /tmp/cilica-allocation-replay
python3 tools/Performance/allocation_verification.py validate \
  --output /tmp/cilica-allocation-replay
python3 tools/Performance/allocation_verification.py measure \
  --output /tmp/cilica-allocation-replay
```

Preparation rejects unrecognized source bytes and existing output directories.
Validation/timing verify prepared artifact hashes. The runner never downloads
inputs or launches services. Native tests run with the filters recorded in this
slice's work; the retained test logs report the exact totals.

Raw paired and .NET samples, environment, commands, source/binary hashes, host
load and test logs are in [the evidence directory](allocation-verification-20261019/).
Full generated C++, binaries and all diagnostic outputs remain in the ignored
local directory `artifacts/allocation-verification-20261019/replay`.

## Next bottleneck

A separate three-second macOS `sample` capture of an O2/g diagnostic binary
contains 1,695 exclusive application-frame samples. Of these, 1,600 (94.4%) map to
generated lines calling `context.instance.owns`. The removed `valid_roots` scan
does not appear. The source-line counts exclude startup/system frames and subtract
children before aggregation; they are not summed inclusive stack counts. This is
one diagnostic recording, and optimized line attribution does not prove the exact
cost of every nearby instruction. It identifies a strong next investigation target.

The next target is the remaining internal reference membership checks. Removing
those checks requires distinguishing compiler-established
references from external inputs; simply making `owns` always return true would
also disable checks needed by the public boundary and collector.
