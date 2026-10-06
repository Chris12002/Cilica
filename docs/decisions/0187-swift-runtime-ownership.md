# ADR 0187: Swift runtime ownership

Accepted 2026-10-06; milestone 7d.2. Simulator execution and device linking only.

## Context

The Apple package imports into Swift, but raw calls leave every client responsible
for native entry serialization, opaque-token scope, capacity preflight and copied
error lifetime. Those are runtime integration concerns, not application game rules.
The existing manifest supplies parameter/result shapes and optional capabilities.

## Decision

Generate a Swift 6 actor named for the Clang module. Its custom serial executor
uses a dedicated Dispatch queue. Actor operations contain no suspension during
native invocation, query/copy, retain/release or close. Concurrent callers cannot
overlap entry; their submission order is not guaranteed. Sequential application
commands must be explicitly awaited. Swift cancellation does not abort or roll
back synchronous managed execution.

Generate logical shape tables from the manifest. Validate and flatten numeric
records, encode signed Int32 and all-bit Integer64 values, and allocate record
outputs before executing. Expose copied Swift values rather than native buffers.
Only enabled string/capture functions appear in generated source, preserving
modules without optional symbols. The core ABI and native implementation do not
change.

Handles contain a private runtime identity and native lease, with actor-local
tracking. Copying a Swift Handle aliases the same lease; explicit clone creates
another and release removes one. Reject foreign/released handles before native
entry. Close is idempotent and destroys all remaining leases on the same worker.
Final destruction queues fallback cleanup without promising its timing. Explicit
close is required for deterministic cleanup; no per-handle automatic release is
implied. A discarded outcome remains bounded by the native quota until close.

Managed failures return a copied fault and optional retained exception in Outcome;
host admission/copy failures throw adapter errors. Copy type/message details from
the retained exception without reexecution. String APIs return exact UInt16 units
so Swift String decoding cannot silently normalize the transport. Bound each
copy/import to 524,288 units and combined exception text to the same limit, with
caller-lowerable copy budgets. Native heap/handle/execution quotas still apply.

## Evidence and review

An opt-in local Apple integration builds and links both target slices. The Swift
session consumer passes on iOS 26.1 with normal/forced collection: 48 concurrent
mutations, signed/wide record round trips, schema rejection, stale/foreign lease
checks, copies after later mutations, captured failure recovery and inspection,
copy/zero-handle bounds, idempotent close and close racing with queued work.
The unchanged real-kernel adapter adds 1,792 exact numeric/UTF-16 comparisons
through Swift, with both collection modes. Device binaries are linked only.

The final focused run passes three tests: opt-in Apple integration and both
existing C session cases after their fixture/contract helper was shared. Three
generator checks pass. Plain and capture-enabled no-record variants also pass
Swift 6 warnings-as-errors type checking. Review found and fixed an unused mutable
capture slot in the plain variant; its reduced generator regression is retained.
The opt-in test is confirmed skipped without an explicit simulator selection.

Review covered worker isolation/no suspension, explicit lease ownership, cleanup,
bit/UTF-16 preservation, manifest field ordering, optional symbols, copied fault
origins and bounded host buffers. Private evidence is
`artifacts/milestone7-swift/evidence-7d2.json`. No unresolved implementation defect
remains in this slice. No hosted work, simulator startup/shutdown, app installation,
signing or push ran. Full application commands, UI/background lifecycle, Expo and
physical-device execution remain separate gates.

The executor follows [Swift's custom actor executor contract](https://github.com/swiftlang/swift-evolution/blob/main/proposals/0392-custom-actor-executors.md).
