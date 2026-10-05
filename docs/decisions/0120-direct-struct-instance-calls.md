# ADR 0120: Direct struct instance calls

Status: Accepted

## Decision

The integer-object profile admits ordinary nonvirtual struct instance methods
and accessors. A distinct value-instance binding requires a writable checked
location of the exact declaring value at parameter zero. Its body uses existing
copies, locations, scalar expansion and rooted return destinations. Lowering
normalizes the validated declaration to a private static call over scalar borrows.

Mutation reaches the original value; by-value arguments and returns still copy.
The direct receiver may cross a control-flow join by restoring parameter zero,
as with value constructors, without admitting arbitrary escaping borrows. An
empty receiver retains calls and side effects. Ordinary methods receive no
constructor privilege to write readonly fields. Ref returns, readonly receiver
admission and virtual/interface value dispatch remain separate gates.

## Evidence and review

Four C# probes match .NET in 1,176 repeated O0/O2 sanitizer results for mutation,
this replacement, aliases, nested calls, copied returns, generic values, empty
receivers and caught faults. Detached receiver/kind mutations are rejected. All
44 focused checks pass, including value/class constructor and location regressions.
Review found no remaining actionable issue.

The private kernel's next gate is the interface implementation metadata on its
direct PlayerId equality call; native equivalence remains pending.
