# ADR 0181: Host-owned UTF-16 copying

Accepted 2026-10-06; milestone 7c.2. Local macOS ARM64 evidence only.

## Context

The v3 handle boundary can preserve managed strings but a host cannot yet provide
or inspect their contents. The runtime already owns immutable UTF-16 buffers and
traces them through ordinary strong handles. Phone adapters need exact text
transfer, including embedded zero and unpaired surrogate code units.

## Decision

Add an optional UTF-16 capability to v3 without changing existing record layouts
or symbols. `EmbeddingObjectContract.Utf16StringTypeId` defaults to zero (disabled).
A nonzero value must name a declared alias bound to the validated closure's exact
intrinsic String type. The enabled alias participates in the contract hash;
disabled contracts retain their original canonical form and fingerprints.
Capabilities add bit 256 only when enabled. The generated header declares two
additional functions for enabled modules: `string_create` and `string_copy`.

Import copies numeric UInt16 code units into the existing immutable managed string
allocation and returns an ordinary retained handle. It reserves the handle slot
before allocation, publishes without another managed allocation, and cancels the
reservation on failure. Inputs need no encoding conversion. Importing zero code
units creates a nonnull empty string, with no interning or identity promise.

Copy validates runtime ownership and exact String type, then copies into a buffer
owned by the caller. A zero-capacity call queries the required length. Short
buffers receive the required length but no partial text. No terminator is written.
A null reference handle is rejected rather than confused with an empty string.
No raw managed buffer pointer, engine-owned external buffer or buffer-release
function is introduced. The existing admission guard covers both operations and
excludes concurrent release, allocation or destruction on that instance.

The allocator's existing heap/object budgets bound imports. Handle/length limits
return LIMIT; a string allocation that cannot satisfy the heap/object budget or
native allocation returns ALLOCATION_FAILED. These failures leave the output
handle untouched. Copying needs no managed allocation or execution arena.

## Evidence and review

The C11 consumer compares every UTF-16 code unit with an authored .NET sequence,
plus managed length/index/concatenation observations. It checks copied ownership,
empty/null distinctions, no partial writes or terminators, aliases, stale/foreign/
wrong-type handles, quotas, reservation cancellation and collection pressure.
O0/O2 native race tests cover import/copy/release with forced collection and
explicit BUSY rejection. The native harnesses use ASan/UBSan, no C++ exceptions or
RTTI, and the 2 KiB frame warning gate.

Review checked exact descriptor validation before StringObject access, host buffer
lifetimes, allocation/root publication, UInt32 lengths and allocator overflow
checks, cleanup ordering and optional contract negotiation. V1/V2 and existing v3
layouts remain unchanged. Wide values, richer exception copying and phone-device
execution remain separate slices; this does not close milestone 7.
