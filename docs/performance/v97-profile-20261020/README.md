# v97 residual-profile evidence

Interpretation and limitations: [the report](../v97-profile-20261020.md).
These are investigation artifacts, not compiler/runtime implementation changes.

- `identity.json`, `baseline-provenance.json`, `profile-provenance.json`: exact
  revision, freshly emitted C++/core/input hashes, compiler source hashes and flags.
- `timing-summary.json`: fresh uninstrumented baseline and non-tiered .NET.
- `profile-aggregate.json`: two sample trees, disjoint exclusive buckets and
  inclusive callers (the latter must not be added to exclusive samples).
- `experiment-summary.json`, `experiment-provenance.json`, `raw-timings.json`:
  all 21 samples per variant, per-process warmups, commands and stack-frame sizes.
- `counts-summary.json`, `method-counts.json`: separate instrumentation of 256
  diffs after eight warmup diffs. Instrumented elapsed time is not benchmark evidence.
- `rea-selected.json`: current-core metadata/IL anchors extracted using REA 6.7.0.
- `native-selected.txt`, `native-summary.json`: `otool -tvV` ARM64 output from the
  sampled v97 binary, selected dictionary wrapper and membership symbols. Sizes
  are instruction counts times four, including cold code; not timing ratios.
- `reference-sources.json`: verified hashes for retained Unity/Roslyn/.NET sources.
- `diagnostic-check-result.json`: complete output equality with forced GC,
  ASan/UBSan and both reference/lifetime diagnostics for the combined experiment.
  This does not establish general maximum-depth stack or semantics acceptance.

Large sources, binaries, full REA JSON, profile trees and logs remain locally in
`artifacts/v97-profile-20261020`; compact records identify their hashes.

## Replay

Use the source tree at `74b5d79` (or its identical compiler/core implementation),
repository-root cwd and the retained approved DiffPlex DLL/PDB/host identified in
`identity.json`. Scripts assume the existing local artifact paths; they are not
portable tooling. Build `tests/Cilica.Metadata.Tests/Cilica.Metadata.Tests.csproj`
first. Copy the scripts to `artifacts/v97-profile-20261020` before running them;
do not run them directly in this documentation directory. Reuse this directory
only when intentionally replacing the retained experiment; otherwise adjust all
artifact references to a fresh output directory.

Run sequentially:

1. `python3 artifacts/v97-profile-20261020/emit.py baseline`
2. `python3 artifacts/v97-profile-20261020/profile.py`
3. `python3 artifacts/v97-profile-20261020/experiments.py`
4. `python3 artifacts/v97-profile-20261020/counts.py`
5. `python3 artifacts/v97-profile-20261020/check-experiment.py`
6. Capture REA and native output, then `python3 artifacts/v97-profile-20261020/summarize.py`.

The profile script expects `core-methods.json`, copied from the earlier
`artifacts/remaining-gap-20261020/core-methods.json`. It is valid only for the
matching core SHA-256 `655cc451...`; regenerate metadata if that changes. Its
application map is the retained `roslyn-il-investigation-20261019/methods.json`.
The experiment script uses the checked-in descriptor transformation from
`docs/performance/remaining-gap-20261020/descriptor-transform.py`; an identical
copy is included here. The fresh baseline must match accepted v97 `final.cpp`.

REA capture used:

```sh
node /tmp/cilica-rea-tools-20261019/node_modules/rea-agents/scripts/rea.mjs inspect-managed-members artifacts/v97-profile-20261020/baseline-core/Cilica.Core.dll --type 'System.Collections.Generic.Dictionary`2' --type 'Cilica.Runtime.StringKeyOperations' --type 'System.OrdinalCaseSensitiveComparer' --type 'System.Char' --format json > artifacts/v97-profile-20261020/rea-core.json
```

Native capture used `otool -tvV artifacts/v97-profile-20261020/profile`.
`native-summary.json` retains the exact selected symbols. .NET comparison is
ordinary .NET with TieredCompilation and ReadyToRun disabled; Unity code is a
retained structural comparator, not a newly timed execution.
