# ADR 0199: Direct native control flow

Status: accepted. Recorded 2026-10-20.

## Decision

Native v93 renders the same validated steps as native labels and branches for
methods with no local exception regions, no dynamic exception terminators and at
most 512 steps. The limit conservatively retains bounded helper frames for very
large methods. It covers every method in the measured DiffPlex closure. Methods
with catch/filter/finally or leave/rethrow/endfilter/endfinally retain the existing
step dispatcher; even a region-free method can legally contain leave.

`CILICA_DIRECT_CONTROL_FLOW` defaults to 1 under native optimization and 0
otherwise. Explicit 0 retains the original driver for diagnosis. Both renderings
share step generation and preserve pending-fault checks at every transition.
Fault materialization occurs with the original frame and roots still live.
Arguments, block input copies, managed arena storage, returned-reference roots,
borrow registrations, depth limits and public ownership checks are unchanged.
Scoped native step bodies prevent branches from bypassing local initialization.
The native compiler may optimize across the resulting direct edges. There is no
C++ text parsing, runtime-helper inlining policy change or IR rewrite.

## Review and evidence

Review found and fixed unused frame_done warnings and the legal region-free
leave case. Its regression executes using the fallback even when direct flow is
forced. No remaining review findings. Two forced-mode differential checks cover
wide heap values, aliases, arithmetic, roots and caught faults (1,176 repeated
native/.NET comparisons), plus the leave regression. Twenty existing focused
checks cover bounded native stack, execution budgets, getters, exceptions and
mutable borrows at O0/O2 with ASan/UBSan and forced GC.

Fresh candidate output is byte-identical after the final eligibility correction.
The baseline is the independently emitted v92 output retained in the immediately
preceding investigation; compiler/core code had not changed since its emission.
Four complete benchmark transcripts match 523 observations and checksum 6146;
two forced-GC operations pass. The verification binary forces the old dispatcher.
Three alternating process pairs, 300 ms warmup and 21 samples of 256 diffs each
measure 365.333 to 243.936 microseconds/diff, 1.4977x. All samples are retained.
This measures direct flow alone, unlike the earlier combined prototypes.
See [performance evidence](../performance/execution-slices-20261020.md).
