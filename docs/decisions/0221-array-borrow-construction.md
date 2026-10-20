# ADR 0221: Single-pass array borrow construction

Accepted 2026-10-20. Native v114.

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


Correctness, shared-module, constrained-stack and release timing evidence is in
[the acceptance report](../performance/v114-array-borrows-20261020.md).
