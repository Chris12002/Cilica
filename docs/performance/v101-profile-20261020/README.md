# v101 residual-profile evidence

Interpretation and limits: [investigation report](../v101-profile-20261020.md).
Generated-code experiments here are diagnostics, not accepted implementations.

- identity/profile-provenance: exact revision, compiler/input/core hashes and commands.
- profile-aggregate: disjoint exclusive costs and separately overlapping character subtree.
- timing/experiment/heap summaries and raw-timings: all measurements, including outliers.
- counts-summary/method-counts/gc-counts: untimed work counts; method wrapper/kernel
  counters are separate and must not be summed as logical managed calls.
- experiment/heap provenance: transformed source and binary identities.
- rea-selected: fresh metadata/IL anchors from the owned core.
- reference-sources/native-selected/native-summary: inspected comparator sources
  and ARM64 helper bodies; native size is not a time ratio.
- acceptance-results: array/cache/direct-character combined probe on a constrained
  stack with forced collection and sanitizers. Descriptor-removal correctness is
  not established beyond the release workload's output observations.

Large generated files, samples, binaries and full REA output remain locally in
artifacts/v101-profile-20261020. Scripts are local investigation tooling, not CI.
Run from the repository root; copy scripts into a fresh artifact directory and
adapt retained path references before replay. Do not execute in this docs folder.

Build the metadata tests, then run emit.py baseline and profile.py. The latter
requires output identical to the accepted character-performance v101 artifact.
The core-methods map can be reused only while the exact core hash matches.
Run counts.py, experiments.py and heap-experiments.py sequentially, with no
profiling/build/testing during timing. Run acceptance.py for the narrower combined
probe, capture REA/native output, then summarize.py. The summarizer also reads
the previous investigation's shared parser and retained comparator references.
