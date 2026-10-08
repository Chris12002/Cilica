# Exact same-owner constructor initialization

The approved CircularBuffer trial stopped at a capacity constructor delegating to
another constructor on the same closed generic class. Existing BaseCall permitted
only the immediate base owner. Admit an exact same-owner initialization call in the
integer-object profile through ConstructorInitializationCall. Keep normal access,
argument types and constructor flags, a direct Parameter0 receiver, no null/virtual
dispatch, one entry-prefix initialization, and no uninitialized receiver alias,
read or escape. Existing prohibition on prefix-local handlers and reentry remains.
This does not admit arbitrary constructor invocation or different closed owners.

Each successfully validated same-owner initialization records one edge. After all
methods validate, both value and lowered compositions check the graph iteratively.
Completed paths are memoized; a visiting-set repetition diagnoses a cycle. Existing
method limits bound the graph (512), without recursive validator stack growth.
Every accepted same-owner path ends at a separately validated immediate-base call.
Unconditional cyclic initializer graphs remain outside this bounded profile even
if represented in IL. Calls that allocate another object remain ordinary allocations.

Struct-valued array arguments exposed a related composition gap: a valid logical
StoreElement lowers to AddressArraySlot/StoreLocation before initialization. Admit
only the already typed, bounds-checked scalar slot address whose array/index are not
the direct uninitialized receiver. Normal slot/type validation still applies, and
StoreLocation still forbids receiver escape. No generic address/box shortcut or
runtime operation is introduced. Unsupported prefix diagnostics name the operation.

CIL101 and object IR61 record the newly accepted initialization relation; native79,
allocation/GC limits, frame machinery, private ownership checks and emitter stay
unchanged. The current peer baseline already includes CIL100 boxed-interface work.

Six authored roots plus the original generic candidate reduction cover argument and
field-initializer order, private/multistep calls, base/inner/argument exceptions,
readonly stores after branches, closed owners, reference identity and inline values.
They provide 2,058 repeated O0/O2 native/.NET comparisons. Allocation-limit tests at 1..6
prove failure cleanup, same-context recovery and precise retained/released roots.
Nine forged mutations in each IR representation reject cycles, missing/repeated
initialization, aliases, flags, later-body calls and disabled profiles. Existing
hierarchy, array escape, value/borrow and segment controls also pass: 111/111 total.
The first cycle probes hit the independent dead-method guard; retained corrected
probes keep displaced targets reachable to specifically exercise the cycle guard.

The first candidate failure remains historical evidence. No upstream source was
executed again during this feature work. A retry must pin the new compiler commit
and retain the same source, adapter, 48 scenarios, isolation limits and stop-on-new-gate
policy. Full candidate or Alchemy replay compatibility is not claimed.
