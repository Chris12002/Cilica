# ADR 0053: Instance-owned retained handles

Status: Accepted for private native v14 helpers. Recorded 2026-10-04.

## Ownership and identity

Each instance lazily owns a bounded table of 1,024 strong handle slots. Retaining
an owned reference (including null) allocates one slot; duplicate handles are
independent roots. Resolve borrows a pointer; the handle must remain active, or the
borrower must publish another root, across managed allocation. Release clears the
slot immediately. Collection visits every active slot with handle provenance, even
null/duplicate slots, charging each against its edge budget. Teardown deletes the
table and managed heap. Handles do not outlive instance destruction.

A handle contains an opaque unsigned 64-bit token. One relaxed atomic issuer shared
by instances of the compiled closure issues a new token on every retain, including
slot reuse and new instance lifetimes. Never wrap; reserve zero as invalid and
fail permanently at issuer exhaustion. This provides a generation identity without
exposing slot addresses. Foreign-instance lookup searches only the receiving
instance's bounded table; stale, released and zero tokens fail. Cross-closure or
cross-library serialization and a public ABI are not defined here. Tokens are not
security capabilities against deliberate native memory manipulation.

The table is separate bounded native runtime storage: at most
`1024 * sizeof(HandleSlot)` bytes per instance (16 KiB on the tested target),
allocated once on demand and released at teardown. It does not consume managed
heap bytes or collect to create space. Table allocation failure and capacity or
token exhaustion return explicit statuses with no new root. A lower per-instance
handle limit may be configured; the hard capacity cannot be exceeded.

Retain/resolve/release reject execution during marking. They do not enter managed
code or alter pending managed faults. One active execution context and serialized
access per instance remain required; atomic issuance does not make a single
instance concurrently callable. Runtime-owned table extents/counters remain trusted
native metadata; contained reference validation protects tracing from stale values.

## Validation and review

O0/O2 sanitizer tests exercise retaining cycles, duplicate/null handles, root
provenance, stress allocations while a handle is the only cycle root, last-release
reclamation, foreign/stale/zero tokens, exact/excess capacity and tracing edge
budgets, corrupted contained references, same-address instance replacement,
terminal issuer exhaustion, and busy operations from diagnostic callbacks.
Review checks ownership, no token reuse, no managed allocation on retain, failed
operations returning no borrowed pointer, and table cleanup at teardown. The
existing collection stress corpus also runs with the new root source enabled.
No public object handle crosses the existing packed Int32 boundary; milestone 7
still owns versioned embedding and platform lifecycle APIs.
