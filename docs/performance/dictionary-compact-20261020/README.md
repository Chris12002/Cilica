# Rejected experiment evidence

Production compiler/library remain at implementation baseline `b171fd7`, native
v116. Neither patch is applied. Read the [report](../dictionary-compact-20261020.md)
before reusing measurements.

- `columns-experiment.patch`: final library and test candidate, including the
  six-position resize failure test and shared-module callback checks.
- `inline-entry-experiment.patch`: earlier inline-struct attempt.
- `final-tests-a.log`, `final-tests-b.log`: 155 candidate checks, 141 pass and
  14 fail from two shared fixture setups reaching unsupported UInt64 arrays.
- `summary.json`: preliminary columns timing summary. Its original baseline raw
  samples were overwritten; this summary is not acceptance evidence.
- `stores-summary.json`, `timing-work*-before-*.jsonl`,
  `timing-work*-columns-stores-*.jsonl`: retained-code store-inlining diagnostic.
  These before samples belong to this experiment, not `summary.json`.
- `inline-summary.json`: earlier struct-layout regression.
- `allocation-summary.json`: independent single-operation allocation diagnostic;
  not instrumented release timing.
- `acceptance-results.json`: final candidate's release/sanitizer DiffPlex checks,
  not overall acceptance of the failed library change.
- `experiment-manifest.json`, source hashes, build options and method trace:
  exact final candidate identity and source/IL correspondence.
- `restoration.json`, `restored-wide-tests.log`: restored production sources,
  passing focused checks and fresh core/C++ hashes identical to accepted v116.
- Large generated C++, binaries, complete IL and native disassembly are retained
  under `artifacts/dictionary-compact-20261020/` with hashes in the manifest.

The retained scripts reference that artifact directory's local large inputs.
They document the experiments; do not apply the patch or treat it as a
prerequisite for building the current supported library.
