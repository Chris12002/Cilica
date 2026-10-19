# DiffPlex: proven leaf field getters

Recorded 2026-10-20 on Apple M4 Pro, macOS 15.7.3 ARM64. Extends the
[collector membership result](gc-membership-20261020.md).

## Result

| Fresh release build | Median per operation | Sample range | Samples |
| --- | ---: | ---: | ---: |
| Baseline: all getters framed | 372.992 microseconds | 366.653–386.920 microseconds | 21 |
| Candidate: proven nested getters use direct path | 363.076 microseconds | 351.589–376.907 microseconds | 21 |

**1.0273x faster, or 2.66% less execution time in this run.** All 42 timed
checksums match, with no exclusions. Process-pair median speedups are 1.0403x,
1.0317x and 1.0184x. Sample ranges overlap
and the host was not isolated, so this is a modest observed benefit, not a broad
performance guarantee. No repeated timing run was used to select a better result.

The earlier .NET reference remains 5.106 microseconds/op, approximately 71.1x
faster than this candidate. Neither .NET nor Unity was rerun for this slice;
their historical numbers are not current paired comparisons. The earlier .NET
run disabled tiered compilation/ReadyToRun. A future competitive claim needs
warmed normal tiered PGO as well. NativeAOT is a distinct comparator.

The peak execution arena remains 12,664 bytes in every timed row. No managed
object/instance layout or GC scratch change occurs. The native executable grows
from 3,461,112 to 3,463,208 bytes, retaining fallback bodies alongside the new
wrappers. The current whole-workload result supports a narrow improvement; it
does not establish that getters account for the larger performance gap.

## Implementation, identity and IL/native trace

[ADR 0198](../decisions/0198-proven-leaf-field-getters.md) records the structural
proof and resource contract. A method must load its single reference parameter,
read one field and return that value, with no locals, branches, calls, writes,
borrows or handlers. Valid nested calls perform receiver checks and use the
existing field-layout helper, without allocating an execution frame or registering
duplicate roots. Cold entry and failed checks keep the original framed path.

Baseline: `d08c129` (full hash in
[baseline-provenance.json](leaf-getters-20261020/baseline-provenance.json)).
Candidate: `834c06c` (full hash in
[source-review.json](leaf-getters-20261020/source-review.json)). Both descend
from selected latest compiler tip `b8918ac`; local main remains an ancestor.
The core and owned-library tree identities match across the pair. Native emission
advances from v91 to v92; IR/grants and public ABI remain unchanged.

Baseline and candidate C++ were independently emitted with
`ApprovedDiffPlexBenchmarkEmissionTests.Emit_approved_benchmark` on their matching
compiler sources. The baseline matches the previous candidate byte for byte.
The new output contains 19 getter wrappers. Removing only those wrappers and
restoring the renamed noinline fallback headers and profile comment reproduces
the complete baseline. Existing method bodies, steps, roots, descriptors and
runtime fragments are unchanged. Wrapper source and compiler hashes are retained.

Input DLL SHA-256 remains
`652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`, upstream
DiffPlex `f500e73f28e28813f942f1f16dec5a34b40e48e3`. DLL, matching PDB and
retained Release IL hashes were rechecked. The PDB records Roslyn
`5.9.0-1.26379.115+14fbf8d5271c98133561eb55185fdb05b286f578`, Release/AnyCpu,
C# 14.0. That exact Roslyn source commit remains unavailable; the previous public
source comparison is not a matching-revision claim.

`ModificationData.get_HashedPieces`, token 0600004a, remains seven IL bytes:
IL_0000 ldarg.0, IL_0001 ldfld, IL_0006 ret. The validated IR has a parameter
definition, field-load definition and return. `BuildPieceHashes`, token 06000011,
calls it at IL_0087 and IL_0092, emitted as caller steps 74 and 81.

Apple Clang O2 inlines the wrapper into those two caller steps. The retained
[ARM64 disassembly](leaf-getters-20261020/getter-callers-arm64.txt) shows
context/null/type checks, field_address and a load, then publication into the
caller root slot. Guard failures call get_HashedPieces_framed. The normal path
avoids the getter's five-step loop; fallback steps remain in the binary. This
verifies the native effect rather than assuming C++ text implies inlining. The
field accessor and type predicate still incur helper calls. Details and binary
symbols are in [native-inspection.json](leaf-getters-20261020/native-inspection.json).

## Validation and measurement

All 47 distinct targeted checks pass: seven authored getter checks and 40 existing
object/GC/diagnostic/frame/embedding/exception/module checks. The authored managed
probes include 588 repeated O0/O2 native/.NET comparisons with forced collection
and ASan/UBSan, plus exact-budget checks. They cover inherited fields, signed
wide values, returned-reference survival and caught null faults. Private native
checks additionally cover no-spare-arena nested success, depth exhaustion, cold
entry, exact getter IL fault origin, wrong-type/foreign/forged receivers and
pending-state preservation. A separate-source-file getter run passes forced GC,
sanitizers and the 2 KiB native-frame warning. Tests exclude arithmetic/write
methods from the optimized shape. Two fresh emission checks pass separately.

Baseline, normal candidate, candidate with both verification flags enabled and
sanitized candidate each match all 523 observations and checksum 6146. Diagnostic
and sanitized candidates pass separate forced-GC operations. Full result traversal
and input setup are outside timed operations; the fixed operation uses 128 UTF-16
characters with mutations at positions 31 and 95.

Release flags: Apple Clang 17.0.0, C++20, O2, DNDEBUG, fno-exceptions, fno-rtti,
Wall/Wextra/Werror; no LTO or sanitizers. Both diagnostic flags default to zero.
Three processes per build alternate order, each warming at least 300 ms before
seven samples of 64 operations. No task-owned build, validation or inspection ran
during timing. Host load, raw rows, per-process medians, commands, platform and
hashes are retained in [leaf-getters-20261020/](leaf-getters-20261020/).

Full local C++, binaries and logs are in `artifacts/leaf-getter-performance-20261020`.
After fresh emission on each matching compiler revision:

```sh
python3 tools/Performance/leaf_getters.py prepare \
  --source /path/to/baseline.cpp --candidate-source /path/to/candidate.cpp \
  --baseline-revision d08c129 --expected /path/to/expected.json \
  --output /path/to/new-output-directory
python3 tools/Performance/leaf_getters.py validate --output /path/to/new-output-directory
python3 tools/Performance/leaf_getters.py measure --output /path/to/new-output-directory
```

## Review and next target

Review checked proof eligibility, receiver-validation order, unsigned storage bits,
inherited-field C++ provenance, non-allocating reference return, unchanged fallback
faults and cross-unit linkage. Valid nested reads now need no arena capacity; this
is intentional elimination of unused storage, not relaxed rooting. No outstanding
review findings. Initial fixture namespace/name-selection issues were corrected;
the final test runs are recorded without discarding the earlier local logs.

The next investigation should broaden direct native emission to hot library
helpers and address remaining frame/borrow/type-helper costs. This slice supplies
a tested proof/fallback pattern, but neither a general optimizing backend nor a
projected speedup for those future changes.

Follow-up: the [combined runtime-cost investigation](runtime-costs-20261020.md)
measures broader dispatch, borrow, type-lookup and string-construction costs.
Its prototypes are diagnostic experiments, not additional accepted optimizations.
