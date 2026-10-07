# ADR AC0097: Preserve composite-format suffix ranges

Recorded 2026-10-07 for AC7b.12.

The engine's boxed-enum format dependency needs the actual format suffix. Change
the exact private BoxedText<T>.Render grant from (Object) to
(Object,String,Int32,Int32), carrying the original format string, start and length.
The shared managed parser computes the range only after validating braces and
format-item structure. An absent/empty suffix has zero length. No substring is
allocated, preserving the existing ordinary-value allocation and callback order.

For the currently admitted ordinary non-IFormattable values, translation consumes
the three extra operands with exact types, then performs the unchanged checked
payload borrow and ToString call. Discovery authenticates the complete new
declaration and intrinsic grant. Existing direct-box flow proof, external caller
rejection and IFormattable rejection remain in force. Enum/numeric formatting is
still diagnostic. The new declaration has a fresh manifest signature; no native
IR, runtime layout, resource bound or application input changes.

Review checked suffix boundaries, malformed-input precedence, no speculative
substring allocation, argument stack order, retained payload roots and exact grant
shape. BoxedFormattingTests and PairFormattingTests pass all eight checks: ten
probes, 49 input pairs each, three repetitions at O0/O2, totaling 2,940 native/.NET
comparisons with forced collection, ASan/UBSan and unchanged 30-second subprocess
caps. They also cover missing grants, hostile callers, aliases and flow joins.
Local main remains an ancestor at 48ecf14.

Evidence: artifacts/alchemy-format-ranges/ac7b-12-{build,tests}.log and the
core-command/core-build outputs. Owned core SHA-256:
7c67b527ff93cc3d3e835433ea540458206a549b7c4822d6a8cf737a76eb8f7c.
Manifest SHA-256:
14b7e92dc8a976fc2171353f130404890a436bd5111e7093d9eb22095f5ad545.
The public baseline and full engine were not rerun for this prerequisite.
