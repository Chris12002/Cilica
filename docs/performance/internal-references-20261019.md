# DiffPlex: internal reference membership verification

Recorded 2026-10-19 on macOS 15.7.3 ARM64, Apple M4 Pro. This records the
internal-reference result on `codex/runtime-performance`, extending the
[allocation-verification result](latest-diffplex-20261019.md).

The subsequent [collector membership index](gc-membership-20261020.md) records
531.794 to 365.227 microseconds/op (1.4561x faster). The
[leaf-getter follow-up](leaf-getters-20261020.md) has the latest paired measurement.

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

## Follow-up: Roslyn IL and source inspection

The investigation now includes the exact input IL and Roslyn's lowering decisions.
[Inspection identities and mappings](internal-references-20261019/roslyn-inspection.json)
record the DLL/PDB/source/tool hashes. Full IL, REA output and sampling artifacts
are local in `artifacts/roslyn-il-investigation-20261019`.

The portable PDB's GUID/stamp matches the input DLL's CodeView record. Its options
identify release optimization, C# 14, AnyCPU and Roslyn
`5.9.0-1.26379.115+14fbf8d5271c98133561eb55185fdb05b286f578`.
The Unity project plugin DLL has the same SHA-256 as the Cilica/.NET input;
Unity's managed-stripped DLL has a different hash and is recorded separately.
ILSpy 9.1.0 produces the readable IL. Independent System.Reflection.Metadata and
REA inspections agree on the selected methods' tokens, RVAs and IL sizes.

| Input method | Token | IL bytes / instructions | Cilica generated steps | Observation |
| --- | --- | ---: | ---: | --- |
| `CharacterChunker.Chunk` | `06000021` | 49 / 26 | 33 | The input loop calls `Char.ToString` at IL_001c and stores a reference at IL_0021; investigate string creation/library costs separately from added dispatch |
| `Differ.BuildPieceHashes` | `06000011` | 186 / 75 | 114 | The input includes interface calls, repeated getters and a local address for `TryGetValue`; actual implementations and borrow machinery need separate attribution |
| `ModificationData.get_HashedPieces` | `0600004a` | 7 / 3 | 5 | The IL is receiver load, field load, return; Unity emits a force-inline field accessor, while Cilica keeps storage/root setup and a step-table driver |

These are static counts, not relative execution costs. All five Cilica getter
step symbols survive in the O2 diagnostic binary. Cilica already calls this
getter by its direct generated symbol; the finding is avoidable-looking frame/
step machinery, not evidence of incorrect dynamic-dispatch resolution.
Inlining or a simpler accessor path still needs an implementation experiment,
null/fault-order and rooting validation, and paired release measurements.

REA 6.7.0 ran locally through its CLI using Node 24.19.0, without global setup,
MCP registration or an analysis service. `inspect-managed-members` selected
Roslyn's `Microsoft.CodeAnalysis.CSharp.CodeGen.CodeGenerator`: 292 methods,
3,117 call edges and 1,211 field accesses, with complete selected coverage.
The corresponding DiffPlex selection contains 29 methods and also reports
complete selected coverage. These counts are scoped to selected types, not the
entire compiler. Both operations inspect assembly bytes without executing the
inspected program. Reproduce the selections with:

```sh
rea inspect-managed-members /path/to/Microsoft.CodeAnalysis.CSharp.dll \
  --type Microsoft.CodeAnalysis.CSharp.CodeGen.CodeGenerator --json
rea inspect-managed-members /path/to/DiffPlex.Benchmark.dll \
  --type DiffPlex.Differ --type DiffPlex.Chunkers.CharacterChunker \
  --type DiffPlex.Model.ModificationData --json
ilspycmd --disable-updatecheck --ilcode /path/to/DiffPlex.Benchmark.dll
```

For source inspection, GitHub rejected the PDB-recorded commit as unavailable.
The public Roslyn checkout is instead pinned to
`e4e2833be7abbc54776788ca35d5c4d2697b0261`; this is comparative source evidence,
not an exact source match to our input compiler. Its
[property rewriter](https://github.com/dotnet/roslyn/blob/e4e2833be7abbc54776788ca35d5c4d2697b0261/src/Compilers/CSharp/Portable/Lowering/LocalRewriter/LocalRewriter_PropertyAccess.cs#L91)
turns property reads into accessor calls. Its
[call-emission decision](https://github.com/dotnet/roslyn/blob/e4e2833be7abbc54776788ca35d5c4d2697b0261/src/Compilers/CSharp/Portable/CodeGen/EmitExpression.cs#L1921)
uses `callvirt` for reference receivers unless the selected conditions permit
`call`, including the known non-null/nonvirtual case. Thus an IL `callvirt` is
not by itself a demand for a virtual lookup. REA also identifies the installed
compiler's `CanUseCallOnRefTypeReceiver`, `EmitInstanceCallExpression` and generated
receiver-strategy helper, with fully decoded bodies in this selection. Source
navigation itself was direct repository inspection; REA's managed tools operate
on assemblies. Roslyn source does not describe .NET JIT machine-code optimization.

A separate current-candidate sample attributes 956 of 2,117 exclusive application
samples (45.16%) to the checked membership lines in collector `visit_edge` and
`visit_object_references`. The capture uses an O2-g binary and repeated verified
DiffPlex operations; it is a single source-line sampling observation, not a new
release timing. Together with the IL evidence this suggests two distinct next
experiments: reduce collector membership lookup cost while preserving its checked
contract, and reduce proven tiny-method/frame overhead. No further optimization
is implemented or speedup claimed by this inspection.
