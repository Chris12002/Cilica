# ADR 0183: Retained exception capture

Accepted 2026-10-06; milestone 7c.4. Desktop evidence only.

## Context

V3 already copies fault codes and source origins, but hosts cannot inspect an
escaping managed exception's type or message. Reinvoking a failed method to retry
an undersized text buffer could repeat application effects. A mutable per-runtime
last-error slot would also race with later calls. The existing strong-handle and
caller-owned UTF-16 copying contracts provide a bounded alternative.

## Decision

Add optional `EmbeddingObjectContract.CaptureExceptions`, disabled by default.
It requires `EmitExceptions` and a validated closure with intrinsic String support.
Enabling it adds `captureExceptions: true` to the canonical schema and capability
bit 1024. Existing records, symbols and disabled contract hashes remain unchanged.

`invoke_capture` returns the existing result plus an independently retained
exception handle. After argument validation, it reserves one handle before managed
execution. Reference-returning methods reuse that reservation for either their
successful result or their escaping exception; scalar/void success cancels it.
Caught exceptions do not produce handles. Host admission/validation failures leave
both outputs untouched. Ordinary `invoke` retains its existing behavior and quota
requirements even when the module supports capture.

Only an existing, valid escaping managed exception is published. The adapter does
not allocate or collect during capture, materialize a new exception, or replace the
original fault. Fatal runtime failures (and failures where no managed exception
could be materialized) may return an execution failure with exception handle zero.
The copied fault code and origin remain authoritative and available independently.

`exception_copy` validates the lease and exception type, then uses the runtime's
existing message formatter. The incoming handle roots the exception and payloads
across lazy formatting allocations and collection. No allocation occurs between
receiving the formatted string and copying it to the host. Type names come from
bounded validated metadata. Returned information includes both text lengths and
the exception declaration's image hash/TypeDef token, separate from throw origin.

The host supplies two UTF-16 buffers. If either is too small, only lengths/type
metadata are written and neither text is partially copied. Query and copy never
rerun the export. Formatting may allocate within existing budgets; failure leaves
all copy outputs untouched and does not change the host's original fault report.
Clone/release/destroy follow normal handle rules, and the same instance admission
guard covers capture/copy and all other calls. No external buffer allocator or
per-runtime last-error state is introduced.

## Evidence and review

Local C11/shared-library consumers compare type names and full UTF-16 messages
with authored .NET exceptions: explicit messages, embedded zero/surrogates, default
messages, parameter formatting, divide-by-zero and null throws. Tests cover copied
origins, caught/successful calls, querying without reexecution, short buffers,
clones after release, later invocation/collection, wrong/stale/foreign handles,
one-slot capture, zero quotas, fatal arena exhaustion and message-allocation
failure. O0/O2 ASan/UBSan race harnesses exercise copy/release/collection and
explicit BUSY responses. Native frames remain under the 2 KiB warning gate with
C++ exceptions/RTTI disabled.

Review covered reservation ownership, logical versus lowered signatures, root
handoff without allocation, formatter rooting, descriptor checks, output/error
ordering and capability negotiation. Inner-exception traversal, stack traces,
structured application values and phone-device evidence remain separate work.
