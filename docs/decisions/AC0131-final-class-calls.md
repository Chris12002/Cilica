# ADR AC0131: Exact final class calls

Recorded 2026-10-07 for AC8.35.

A class callvirt to a final method cannot select an override. Retain its exact
binding and lower through the existing direct instance-call path with a receiver
null check after argument evaluation. Keep virtual-slot declaration metadata for
independent validation; only omit the unnecessary class dispatch map. Interface
calls continue using their complete interface maps.

The integer-object profile admits this operation; legacy profiles do not gain a
new direct virtual form. Logical and scalar validators require the Final flag,
ordinary access, receiver type and signature evidence. Abstract methods still
require virtual dispatch and cannot be direct targets. No emitter, allocation,
rooting, resource limit or managed exception changes are necessary.

Reduced probes cover null return/void calls on an abstract owner with no concrete
receiver, argument division faults before null checks, inherited methods hidden
by a derived declaration, and interface calls. Five probes yield 1,470 repeated
O0/O2 differential comparisons under forced GC and sanitizers. Three detached-IR
cases remove final evidence and are rejected at both stages. All 85 selected
dispatch checks pass. Evidence: artifacts/alchemy-final-calls/regressions.log.
Integer CIL v52 and object IR v39 distinguish this admission; native v66 and
layout v24 remain unchanged. Reviewed before commit; no full engine/replay claim.
