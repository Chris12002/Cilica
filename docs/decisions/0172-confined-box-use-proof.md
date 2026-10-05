# ADR 0172: Prove confined box uses before rejecting interface overlap

Accepted 2026-10-06; milestone 6c.57.

The kernel boxes ResolutionHandlerId only for the existing String.Format path,
yet discovery rejected it because an interface implemented by that value also
appeared elsewhere in the closure. Interface ancestry alone does not establish
that the particular box can become a dispatch receiver.

Replace that discovery-wide rejection with a narrow detached IR use proof. A box
whose interfaces overlap dispatch declarations may be compared, unboxed, or passed
to ordinary method parameters proven to have the same restricted uses. Scalar
CheckBox aliases are followed to component borrows. Every other reference use,
including local/heap/static storage, return/edge aliases, casts and virtual calls,
fails closed. Parameter-address escapes and recursive proofs are rejected.

The proof re-runs after scalar lowering; no caller-authored exemption flag is
trusted. It charges statements, operands and parameter scans against the existing
1,048,576 type-relation work bound and caps recursion at 64. Completed parameter
summaries are keyed by exact closed method identity and parameter index. Payload
borrows still retain owners and cannot expose the box's object identity.

Boxed value IR v3/scalar boxed IR v4 record the added admission. General boxed
interface dispatch remains unsupported. Existing unrelated interface casts/storage
are unaffected when their interfaces have no reachable dispatch declarations.

Validation includes O0/O2 .NET/native comparisons for a direct unbox helper and
ported formatting alongside independent interface calls (588 new repeated
outcomes), sanitizers, forced collection and frame-budget checks. Direct/mixed and
helper-escaped dispatch remain diagnostic. A detached scalar mutation that stores
a previously confined parameter is rejected. Review covered argument aliases,
all operand/edge uses, payload identity, closed summary keys and bounded work.

The real gate now reaches PlayerId[] to IReadOnlyList<PlayerId> assignment at
Adapter.Prepare IL_00b4 (`gate-mapped-6c57.json`). It has not reached successful
complete-program IR validation or native transcript comparison.
