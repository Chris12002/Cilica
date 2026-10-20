# Retained investigation evidence

See [the report](../remaining-gap-20261020.md) for interpretation and limitations.
These are diagnostic experiments, not compiler/runtime changes.

## Files

- `timing-summary.json`: fresh committed baseline and .NET measurements.
- `variant-summary.json`: descriptor, inlining and freshly emitted hash experiments.
- `frame-summary.json`: separate native-frame experiment with paired controls.
- `borrow-ablation-summary.json`: negative/noisy borrow experiments; semantics weakened.
- `raw-timings.json`: every warmup record and timed sample, including outliers.
- `profile-aggregate.json`: two independent statistical samples, exclusive groups
  and callers of the broad object predicate. Inclusive caller counts are not
  interchangeable with exclusive time attribution.
- `counts.txt`, `method-counts.json`: one cold diff including initialization.
- `rea-hash-il.json`: REA metadata, hashes, opcode counts and IL anchors for the
  exact freshly built core's StringKeyOperations.
- `*-provenance.json`, `reference-sources.json`, `core-*.json`: revisions, commands,
  source/binary hashes, library compiler options and source comparisons.
- `diagnostic-check-result.json`: separate forced-GC ASan/UBSan result for the
  combined native-frame probe. This is not general stack/semantics acceptance.
- Python scripts and `hash_*.cs`: the transformations and measurement supervisors.

## Local replay

The retained local workspace is `artifacts/remaining-gap-20261020`. Generated C++,
binaries, dSYM, raw `sample-*.txt`, metadata reader output, DLLs and complete logs
remain there and are ignored by Git. The scripts assume repository-root cwd and
that layout, plus the retained input host and DLL paths shown in `profile.py` and
`hash-variants.py`. They are investigation scripts, not a portable benchmark product.
Copy the evidence scripts to that directory before replay; do not run them directly
from this documentation folder. `variants.py` in this bundle loads the identical
saved descriptor transformation from its sibling `descriptor-transform.py`;
the original run loaded that function from the preceding investigation's script.

To reconstruct the baseline, archive commit `8b552e4` into the isolated `source`
subdirectory, build `tests/Cilica.Metadata.Tests/Cilica.Metadata.Tests.csproj`,
and invoke the explicit `ApprovedDiffPlexBenchmarkEmissionTests.Emit_approved_benchmark`
test. Use the environment in `hash-variants.py`, with `CILICA_BENCH_CPP` set to
`baseline.cpp` and `CILICA_BENCH_CORE_CAPTURE` set to `core`. Retain the core metadata
reader output as `core-methods.json`. The exact input transcript and application
method metadata dependencies are identified in `profile.py`; those earlier local
artifacts are required, or regenerate them from the hash-identified input DLL.

Run sequentially: `profile.py`, `variants.py`, `hash-variants.py`,
`measure-variants.py`, `counts.py`, `borrow-ablation.py`, `frame-experiment.py`,
then `check-experiment.py`. Do not run builds or sampling concurrently with timing.
The source hash variants restore the original isolated source in a finally block.
The scripts never need to edit the working tree's compiler emitter.

`profile.py` computes exclusive samples as a node's count minus its immediate
children, groups managed symbols by DLL hash and method token, and keeps external
system-library samples separate. Raw profile transcripts are retained locally;
`local-artifact-hashes.json` identifies them and the current generated/binary inputs.
No sample filtering or best-run selection was applied. The noisy borrow series is
kept as negative evidence rather than rerun until favorable.
