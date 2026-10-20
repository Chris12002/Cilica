# v104 investigation evidence

Parent report: [allocator, fields and local outputs](../v104-profile-20261020.md).
All edits in experiments.py/followup.py are diagnostic modifications of freshly
emitted C++; they do not modify production source or establish general proofs.

Run from the repository root. Use a new artifact directory, copy these scripts
there, and copy core-methods.json from the prior v101 artifact only after checking
the captured core hash. Build the metadata tests, then run emit.py baseline.
profile.py asserts byte identity with committed-local.cpp, compiles release and
sampling binaries, verifies outputs, times native/.NET, then samples twice.
Run aggregate.py and counts.py. Run experiments.py, then followup.py, sequentially.
Run acceptance.py and metrics.py after timing. Every release experiment validates
the 523-value oracle before timing; all timings check the per-diff checksum.

Capture native code with `xcrun llvm-objdump --disassemble --demangle` for baseline
and inline_fields into native-arm64.txt and inline-native-arm64.txt respectively.
Then summarize.py regenerates sample/counter/native summaries. package.py records
identity and copies compact evidence into this directory.

The source methods map is in artifacts/roslyn-il-investigation-20261019/methods.json.
The original input DLL/PDB/host and Unity output paths are in identity.json and
reference-sources.json. Full sample trees and generated sources are intentionally
kept in artifacts rather than committed.

- timing-summary.json: fresh compiler versus .NET.
- experiment-summary.json / followup-summary.json: two paired experiment batches.
- raw-timings.json: every release sample in both batches and the comparator run.
- profile-aggregate.json: exclusive buckets and explicitly non-additive subtree data.
- counts-summary.json / metrics-summary.json: untimed work and memory measurements.
- experiment-provenance.json / followup-provenance.json: exact C++ hashes and flags.
- acceptance-results.json: bounded-stack release and two forced-GC sanitizer modes.
- native-summary.json / native-selected.txt: ARM64 field call evidence.
- rea-selected.json: prior inspection of the identical owned-core binary.
- pdb-module-info.txt / reference-sources.json: input compiler and comparator identity.
- identity.json: source revision, fresh output identity and retained raw evidence hashes.

The raw field-offset experiment still requires a C++ object/layout/provenance
proof before production. The local-output experiment explicitly selects the
benchmark target; production must use structural analysis. The pool's fixed
16-slab budget is experimental and is not a new public runtime limit.
