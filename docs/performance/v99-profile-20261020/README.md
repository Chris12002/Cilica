# v99 residual-profile evidence

Interpretation: [the investigation report](../v99-profile-20261020.md).
These files document diagnostic experiments, not production changes.

- `identity.json`, `baseline-provenance.json`, `profile-provenance.json`: revision,
  fresh C++/core/input identities, compiler hashes, commands and comparator host.
- `profile-aggregate.json`: exclusive costs and the separately overlapping
  character call-chain attribution; never add inclusive and exclusive totals.
- `timing-summary.json`: fresh release and ordinary non-tiered .NET measurements.
- `experiment-summary.json`, `experiment-provenance.json`, `raw-timings.json`:
  all release samples, generated variant hashes, flags and native frame sizes.
- `counts-summary.json`, `method-counts.json`: separate untimed instrumentation;
  small-set and broad hash-set descriptor counters are distinct.
- `rea-selected.json`: fresh owned-core PE/CLI method identities and IL anchors.
- `native-selected.txt`, `native-summary.json`: ARM64 validation and Char.ToString
  bodies from the sampled binary. Sizes include cold paths and are not time ratios.
- `reference-sources.json`: re-read Unity, Roslyn and pinned .NET source hashes.
- `acceptance-results.json`: strongest combined probe, full output equality,
  constrained stack and forced-GC sanitizers with array diagnostics both off/on.
  This does not constitute general semantic/capacity acceptance for implementation.

Large generated C++, binaries, captured core, full REA JSON, raw sample trees and
failure logs remain locally in `artifacts/v99-profile-20261020`.

## Replay

Use compiler implementation `8f951a5` / baseline tree `2906904` and the exact local
inputs listed in identity.json. Run from the repository root. These scripts assume
retained local comparator files; they are investigation tooling, not portable CI.
Copy scripts into a fresh artifact directory and adjust references before reuse;
do not run them inside this documentation folder or overwrite retained evidence.

Build `tests/Cilica.Metadata.Tests/Cilica.Metadata.Tests.csproj` first. The emitter
must match committed v99; the accepted generated C++ is retained in
`artifacts/descriptor-typed-20261020/committed.cpp`. Copy core-methods.json from
`artifacts/v97-profile-20261020` only while its checked core hash still matches.

Run sequentially, with no unrelated builds/tests/profilers during timing:

1. `python3 artifacts/v99-profile-20261020/emit.py baseline`
2. `python3 artifacts/v99-profile-20261020/profile.py`
3. `python3 artifacts/v99-profile-20261020/experiments.py`
4. `python3 artifacts/v99-profile-20261020/counts.py`
5. `python3 artifacts/v99-profile-20261020/acceptance.py`
6. Capture REA/native references, then run `summarize.py` in that artifact folder.

REA capture:

```sh
node /tmp/cilica-rea-tools-20261019/node_modules/rea-agents/scripts/rea.mjs inspect-managed-members artifacts/v99-profile-20261020/baseline-core/Cilica.Core.dll --type 'System.Char' --type 'System.Collections.Generic.Dictionary`2' --type 'System.Collections.Generic.List`1' --type 'Cilica.Runtime.ArrayInterface`1' --format json
```

Save stdout as rea-core.json in the artifact folder. Native capture uses
`otool -tvV artifacts/v99-profile-20261020/profile > artifacts/v99-profile-20261020/native-arm64.txt`.
The report records why the first allocation-capable wrapper probe failed and how
its root scopes were corrected before measurement. Original failure logs remain
local. No compiler or runtime source changes are needed to replay the probes.
