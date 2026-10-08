# Performance catalogue

These are workload-specific measurements, not compatibility or general speed claims.
Timing is informational; correctness remains mandatory. No noisy ratio gates CI.

| Priority | Finding | Evidence and confidence | Smallest proposed action |
| --- | --- | --- | --- |
| 1 | Pre-allocation root validation repeatedly performs linear heap membership searches | DiffPlex: 89.70% of instrumented elapsed time; 125,296 queries per warmed diff. High confidence. | Preserve exact membership checks; the scoped lazy-prefix experiment addresses this without a persistent ownership authority. |
| 2 | Ordinary array/field/reference checks also search the same allocation list | 22,750 queries per warmed diff; 16.78% of all measured list-node visits. Independent wall-time share not isolated. High confidence in work count. | Profile the scoped revision before proposing further membership reuse; do not remove checks. |
| 3 | Uncollected allocations amplify both costs until the 4,096-object quota collects | 824 objects / 85,928 charged bytes per diff; eight stable survivors after explicit collection. High confidence. | Fix lookup scaling first; investigate allocation representation and root liveness only after new profiling. |

[Full fingerprint, experiment and caveats](diffplex-20261008.md) ·
[Machine-readable results](diffplex-20261008/analysis.json) ·
[Reusable suite](../../tools/Performance/diffplex/README.md) ·
[Correctness-first lazy-prefix revision](lazy-prefix-20261008.md)

Collection itself consumed 0.114% of the original instrumented run. On the rejected persistent-index
experiment, [residual sampling and adoption review](ownership-index-review.md)
identify generated execution/dispatch as the largest remaining region (46.65% of
CPU samples), followed by root validation (16.20%), locations/borrows (11.08%) and
descriptor checks (9.79%). These are diagnostic regions, not promised removable costs.
Compilation and bridge/serialization work are absent from the steady compute loop.

The ranking above fingerprints the original baseline. The rejected index’s residual
percentages must not be attributed to the narrower lazy-prefix implementation.

The revised lazy-prefix experiment measured 261.225 → 38.041 ms per diff (**6.867×**)
after corruption/frame/budget checks and independent review. It remains experimental.
A separate check also confirmed that launch-inclusive `wait4` RSS can inherit the
Python launcher's high-water floor; see the revision's RSS correction before
interpreting the historical .NET/native memory comparison.

[NativeAOT / IL2CPP architectural comparison](runtime-comparison-20261008.md)
records a read-only source review and ranked proposals, with unexecuted owned
kernels for future comparisons. It adds no runtime adoption or performance claim.

[Local Unity pipeline setup](../../tools/Performance/unity/README.md): Mac IL2CPP
support installed, pinned .NET oracle verified; Editor build awaits license activation.
