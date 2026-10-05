# ADR 0121: Direct calls on interface-bearing values

Status: Accepted

## Decision

The integer-object profile retains exact closed interface declarations on value
layouts and validates them through the bounded reference/interface catalog.
Direct calls to admitted struct method bodies may carry virtual/final/newslot
metadata without creating a class dispatch slot. CIL callvirt/constrained calls
remain separate capabilities. Lowering normalizes validated direct value methods
to private static calls and retains their interface metadata catalog.

The composed scalar wrappers preserve interface usage through that value catalog.
Simpler scalar profiles without retained value metadata reject these layouts.
Boxing/unboxing an interface-bearing value is rejected in CIL, value IR and scalar
IR until boxed interface relationships and dispatch are implemented. No interface
facts are silently discarded. Ref returns and readonly receivers remain outside
this slice.

## Evidence and review

Two independent C# probes match .NET in 588 repeated O0/O2 sanitizer results for
direct equality/closed interface bodies, mutation, copied returns, identity and
allocation. All 131 distinct focused checks pass, including layout, boxed IR,
constructor and detached interface-plan regressions. Review added detached boxed
operation rejection and an explicit retained-metadata profile boundary.

Tests preserve diagnostics for constrained Object calls and the installed
runtime's unsupported generic parameter flags on IEquatable. These are not
implemented by this direct-call slice. The private kernel discovery now first
stops in invariant numeric formatting; subsequent layout/library gates remain.
Native kernel equivalence is still pending.
