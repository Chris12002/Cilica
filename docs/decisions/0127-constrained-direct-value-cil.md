# ADR 0127: Constrained direct value CIL

Status: Accepted

## Decision

In the integer-object profile, recognize one adjacent constrained./callvirt pair
with an exact closed inline value receiver. Use ADR 0126's metadata selector to
find its direct body and carry the prefix offset/receiver identity into value
translation. Pop the exact checked writable receiver borrow and lower through the
existing direct value-call ABI. Do not allocate a box, create a class slot or add
a reference null check to this managed-location receiver.

Keep existing member-access validation. Public interface bodies can execute
directly; a private explicit body across owners requires additional access
evidence and remains rejected. Primitive/reference constrained receivers,
boxing/default fallbacks, combined prefixes, static-interface calls, readonly
receivers and broader generic constraints remain separate capabilities.

The shared control-flow partitioner rejects branches and exception boundaries
inside a prefix sequence. A prefix cannot be bypassed while retaining its
selected call semantics. Other profiles retain explicit constrained-call gates.

## Evidence and review

All 124 distinct focused checks pass. Three new constrained probes supply 882
matching repeated native O0/O2 sanitizer results, covering concrete and generic
Object overrides, receiver mutation, collection, exception ordering and a closed
interface call without boxing. An independent .NET constrained-interface oracle
checks the authored IL receiver mutation. Exact/short frames and ownership cleanup
remain checked by the native harness.

Negative regressions cover duplicate/mixed prefixes, the wrong following opcode,
branches into a call, incompatible receiver stacks, inaccessible private bodies
and boxing fallback. Constructor, instance-value, ordinary control-flow and
exception regressions pass. Review found no remaining actionable issue. Native
kernel equivalence and the required library members remain pending.
