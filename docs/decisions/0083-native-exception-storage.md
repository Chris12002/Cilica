# ADR 0083: Private rooted native exception storage

Status: accepted for private helper probes. Recorded 2026-10-04.

Native v17 adds a pending exception reference to Context and an emergency exception
reference to Instance. Precise root visitation authenticates and traces both, counting
them against the existing collector edge budget. Old profiles leave them null.
`EmitExceptionRuntime` emits storage helpers around a validated scalar program; it
does not accept handler IR or execute managed catch/finally/filter bodies.

Intrinsic exception descriptors use a common standard-layout payload with a traced
private message reference. No fabricated managed FieldDef exposes this slot. Creation
checks exact descriptor membership and owned String/null messages before allocation.
Message inspection and pending/saved state operations authenticate ownership before
payload reads. Supplied UTF-16 messages preserve identity/content, including zero and
unpaired surrogate code units. Default/resource message materialization remains a
subsequent execution gate; the private helper can report an absent message as null.

Runtime numeric faults can acquire managed identity before handler search. Status 13
denotes an explicitly thrown object; native ownership/depth/arena statuses remain
outside managed exception handling. Suspend/restore preserves the same object and
fault provenance through caller-published saved roots. Creation failures preserve a
preexisting pending state. Null throw maps to a managed null-reference fault.

Explicit preparation reserves one instance-owned allocation-failure exception within
the normal heap quota. It remains rooted until instance destruction. If preparation
itself cannot fit, it returns bounded bootstrap failure without publishing partial
state. Later fault materialization can fall back to this object without another
allocation, retaining origin while reporting allocation failure. No quota is raised.

Review checked address authentication, typed payload access, tracing, pending/saved
identity, provenance, allocation retry and marking reentrancy. All 28 distinct selected
local checks pass, including O0/O2 ASan/UBSan helper probes and existing tracing,
collection, boxing and delegate regressions. Tests cover exact/short heap capacity,
bootstrap recovery, corrupt/foreign/forged references, message content, collection
pressure and emergency retention. Review also added missing source safepoints for
message/literal allocations. Handler dispatch, default messages and CIL execution
remain next; no public exception ABI or hosted work is introduced.
