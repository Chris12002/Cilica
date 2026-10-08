# Batch010: vector section clearing and verified CRC32 roots

Implemented bounded Array.Clear(Array,Int32,Int32) using a granted public managed
helper and independent actual stack-type/access checks. Same-block typed vectors
preserve complete validation before writes, argument order, covariance, identity
and reference release. CIL95; IR59/native79, collector, native operations and quotas
unchanged. See [decision](../decisions/compatibility-012-array-section-clear.md).

**97 distinct checks pass across final focused runs.** Eight Clear semantic probes
supply 2,352 repeated native/.NET comparisons; the original independent CircularBuffer
Clear probe adds 294, and a repaired UInt32-copy control adds 294: 2,940 new comparisons.
Four explicit O0/O2 GC tests require exact survivor counts after partial/full/repeated
clearing of rooted reference and inline-value arrays. Native checks use forced GC,
ASan/UBSan, strict frame warnings and root/borrow cleanup. Grants, direct safe managed
helper calls, unproven Array aliases, argument joins and narrow-location restrictions
remain checked. Full repository suite and full Alchemy replay were not run.

The first implementation's internal bridge correctly failed access checks; the public
ordinary managed body fixes that without relaxing access rules. A second run found
Byte/Char/Boolean generic default initialization requires unsupported narrow locations.
Those cases now have explicit rejection controls; this slice does not claim them.
The adjacent run passed 79/80. Its unsigned-copy negative also fails unchanged on
pre-Clear d37c2ad, so it was replaced with actual high-bit/fault-order native coverage.
The repaired copy/candidate run passes 18/18. Baseline setup mistakes, original failure
logs and final evidence are retained rather than counted as compiler regressions.

[Evidence](batch-010-evidence.json) records counts, final source/binary hashes and
baseline provenance. [Focused log](batch-010/tests.log),
[adjacent original result](batch-010/adjacent-tests.log),
[pinned baseline reproduction](batch-010/baseline-tests.log) and
[repaired result](batch-010/repaired-copy-tests.log) support the final tally.
The final build has zero errors and the existing CS0108 State-hiding fixture warning.

Separately, the approved [CRC32 trial](crc32-execution-20261008.md) passes 41 scenarios,
729 full transcript observations and 4,374 repeated O0/O2 comparisons on frozen c64312e
plus exact 6068708 overlays. Its candidate source was unchanged. The report explicitly
records that successful temporary generated-C++ hashes were not captured. Candidate
assembly, source, package, export and overlay hashes plus MIT licence are retained.
CRC32's container, input image and staging were removed; the tooling image remains.

The [current manifest](batch-010.json) marks these CRC32 roots verified and directs
future batches to skip them unless concrete evidence warrants another run. CircularBuffer
remains mapped-blocked: both public Array.Copy overloads and ArraySegment mapping are
still absent. No CircularBuffer source, build or tests ran. Next batch should design
bounded same-type copy with overlap/range/fault tests, then map segments and reassess
the actual candidate closure before requesting its separate execution approval.

Review milestone 6's signed sorting changes are preserved. Performance lane 9527e64
retains its own catalogue and rejected index evidence; this lane supplied read-only
reviews of membership corruption, accounting, stack use and later snapshot designs.
No experimental index was adopted. All baseline source files were verified against
the archive before deleting only the disposable export and generated baseline outputs.
