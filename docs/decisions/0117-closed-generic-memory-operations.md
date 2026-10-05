# ADR 0117: Closed generic memory operations

Status: Accepted

## Decision

The integer-object profile resolves initobj, ldobj, stobj and cpobj operands to
their substituted storage type. Supported integer and reference operands use
existing checked locations; structs retain the same leaf expansion. Named
primitive operands must match the selected runtime identity, not a type name.
Memory operations require exact borrow element types and writable destinations.
Copies snapshot source leaves before writing an overlapping destination. Narrow
memory operands and narrow borrows remain diagnostics.

## Evidence and review

Three C# probes match .NET in 882 repeated O0/O2 sanitizer results, covering
reference clearing, wide integers, aliasing, rooted copies and generic defaults.
Explicit primitive cpobj/initobj probes add 588 expected native results, with a
negative element-type mismatch regression. All 102 distinct focused checks pass.

The first O0 run exposed a generated reference-returning default helper with a
2,080-byte native stack frame. Borrow registration now extracts frame arrays in
a noinline constructor overload, keeping driver instrumentation out of that
stack frame without changing borrow lifetime or the 2 KiB gate. Native borrow
and wide-value regressions pass. Review found no remaining actionable issue.

The private kernel advances to a class constructor with value parameters.
Native kernel equivalence remains pending.
