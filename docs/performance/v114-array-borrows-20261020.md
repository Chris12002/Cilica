# v114: Single-pass array borrow construction

Accepted 2026-10-20, based on `08667362ee9e5c87f08af36d9ed631f03891748f`. The compiler changes
in this commit regenerated the exact approved DiffPlex assembly. Compiler source,
input DLL/PDB, owned core, generated C++ and binary hashes and build settings are
retained in [provenance](v114-array-borrows-20261020/provenance.json). This is current compiler
acceptance, following the generated-code experiments in the [v111 investigation](v111-profile-20261020.md).

For optimized builds, array_location authenticates owner, descriptor, backing
storage, element bounds and exact writable array view once. It then checks only
slot zero for scalar/reference arrays or the existing component-address helper
for value arrays. The previously repeated location_storage round trip is removed.
Every subsequent borrow read/write still authenticates the complete carrier.
There is no allocation, callback, safepoint or mutable descriptor between the
constructor's checks and returning the carrier. Null, malformed storage, bounds,
covariance, slot and readonly behavior are preserved. No new borrow type is admitted.

O0 retains the original constructor tail: the first implementation's extra
aggregate return raised an O0 ASan frame to 2,176 bytes. Keeping its old code path
preserves the historical diagnostic frame gate while optimized and constrained
whole-stack acceptance use the newer measured budget. No limits are raised here.

Extended reference/value-array probes and a new Int32 carrier probe exercise
invalid slots, invalid backing storage, bounds-before-slot errors, empty arrays,
readonly construction, wide-value preservation and marking-busy order. The initial
probe incorrectly used the old array profile without Int32 borrows and treated
component 3 of a five-component value as invalid; these test assumptions were
corrected. The new Int32 catalog also needed its explicit array root, as required
by the detached model's exact closure validation. Initial logs are retained.
The detached flow must actually use each declared array type and retain an Int32
entry boundary; the corrected probe adds an ordinary integer-array allocation to
its authored flow. No production admission rule was relaxed to accept the probe.

All 44 distinct selected checks pass: 42 from the corrected full selection and
the two repaired Int32-probe cases from followup-tests.log. This includes reference
array differential execution, value slots, ordinary arrays, typed dictionary
lookup and independent shared modules. Review confirmed no callback or allocation
between the retained checks, identical returned carriers, and checked resolution
on later accesses. O0 diagnostic stack checks pass without raising their limits.


## Release measurements

Three alternating fresh processes per binary and workload, seven samples each,
1,024 diffs per sample; medians in microseconds per diff. Both binaries match all
523 character and 2,061 line observations before timing. Timing follows tests and
sanitizer builds. These are paired local Apple Clang ARM64 measurements, not
cross-platform guarantees. Full samples and process medians are retained.

| Workload | Before µs | v114 µs | Time reduction |
|---|---:|---:|---:|
| Character | 24.945 | 24.418 | 2.11% |
| Line | 68.688 | 68.351 | 0.49% |

Release binary: 599,432 bytes; largest measured frame:
432 bytes. Separate checked 512–576 KiB thread-stack
acceptance passes both complete observation sets in release and O1 ASan/UBSan
with forced collection, internal and borrow diagnostics both disabled and enabled.
Maximum acceptance frames: release: 544 B, optimized_sanitized: 448 B, sanitized: 480 B. Each run also exercises 32 sequences of
collector stop/limit/invalid/busy/recovery and scratch cleanup.

Focused test output is retained in [tests.log](v114-array-borrows-20261020/tests.log). Review covers
semantic checks, fault ordering, rooting, allocation accounting, optimized/O0
policy, C++ linkage and independently compiled shared modules. No unresolved
review finding. Existing CS0108 fixture warning is unrelated. No hosted CI or push.

Generated C++, executables and full debug symbols remain in `artifacts/v114-array-borrows-20261020`; compact
scripts, logs, observations, stack records and timings are committed beside this
report. See [ADR 0221](../decisions/0221-array-borrow-construction.md).
