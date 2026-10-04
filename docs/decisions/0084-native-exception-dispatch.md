# ADR 0084: Rooted native exception dispatch

Status: accepted for the bounded authored IR profile. Recorded 2026-10-04.

`LowerExceptions` and `EmitExceptions` compose scalar values, locations, boxes and
delegates with managed exception handlers. Per-frame active block state selects
clauses from innermost try outward, retaining metadata order for sibling catches.
Cleanup/filter continuations keep both saved identity roots and original fault
provenance. A completed continuation propagates without redispatching its cleanup
block. Throw-null materializes NullReferenceException; rethrow retains identity.
Only the verified pure, acyclic type-test filter contract is admitted. General
search-before-unwind filters remain unsupported.

The normal leave path runs finally clauses only when it exits their try scope.
Fault clauses run only during propagation. A leave inside a cleanup may remain
inside that cleanup, but cannot escape it. Every activation retains its existing
arena and root guards, with continuation storage charged to its frame allocation.

Messages cache a lazily allocated UTF-16 string or preserve the supplied identity.
Invariant selected runtime fault resources match the local .NET test oracle;
explicit null uses the framework default for selected runtime exception classes
and the nominal type-name fallback for other authored shells. Resource sizes are
bounded. Allocation failure leaves the message slot and existing origin unchanged.
The instance emergency exception remains charged to normal heap capacity.

Nineteen selected checks cover contracts, O0/O2 ASan/UBSan handler/filter/rethrow
probes and message/ownership/heap failures. Native builds retain the 2 KiB stack
gate and disable C++ exceptions/RTTI. CIL handling, a public exception ABI, localized
resources and broad framework exception constructors remain separate gates.
