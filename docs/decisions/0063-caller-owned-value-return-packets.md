# ADR 0063: Caller-owned value return packets

Status: Accepted for authored value IR. Recorded 2026-10-04.

## Context

A struct return cannot expose a pointer into a callee frame that will be popped.
Its embedded references also need rooted destinations before subsequent execution.
The scalar borrow protocol already provides checked, synchronous access to storage
in an active caller, so it can carry return destinations without another native ABI.

## Decision

`value-location-scalars-v2` extends `ValueScalarLowering.LowerLocations` with
struct-return packets. A value-returning method lowers to a void method whose
ordinary expanded parameters are followed by one writable scalar location for each
return leaf. The packed public entry remains Int32-only. The pure-copy `Lower`
entry retains its scalar-return restriction and directs callers to `LowerLocations`
for struct returns.

For each value-returning call site, the caller reserves fresh typed locals in its
budgeted frame, takes their addresses, and lends those destinations to the callee.
Reference locals are part of the caller's precise root array from frame entry.
After a successful invocation, checked loads copy all result leaves into ordinary
SSA snapshots. Later calls cannot overwrite an earlier returned value. A callee
can itself receive a nested struct result, copy it to its caller's packet and then
pop its own frame; no pointer into that finished frame escapes.

All hidden destination parameters are authenticated by the existing scalar method
entry checks before the body executes. A value return copies its already-computed
snapshot to those destinations and then returns void. Stores do not allocate or
call back, and the caller frame remains active. Invalid output capabilities fail
before any output leaf is written. Normal managed faults before return preserve
their original method/IL origin, skip result loads and unwind all registrations,
roots and arena storage. This relies on the same trusted native-buffer and
single-threaded invariants as scalar borrowing, not arbitrary-corruption recovery.

Empty struct results carry zero output locations but retain the invocation and its
fault/depth behavior. Hidden parameters, argument entries, result locals, address
temporaries, loads and stores all count toward existing limits. Per-method local
and parameter checks happen during construction; statement/argument checks and
final scalar root/storage validation cover the complete generated program. Native
byte accounting continues to use the emitted frame/packet sizes in the arena.

## Validation and limits

Local O0/O2 ASan/UBSan probes compare chained struct returns with C#, including
nested returning callees, normal/extreme Int32 values, divide-by-zero and signed
division overflow. Both the private execution path and packed boundary preserve
faults and leave public output unchanged on failure. A private harness returns a
live embedded reference into rooted caller storage, verifies identity and
collection survival, and rejects an invalid final destination before earlier
outputs change. Exact measured execution storage succeeds; one byte less fails
with full cleanup. The 2 KiB stack-warning gate remains enabled.

Detached tests cover empty returns and excess hidden parameter/local/root storage;
existing mutable-borrow and pure-copy regressions remain green. These are authored
IR semantics. CIL translation, allocating value source operations, inline value
fields in heap objects, instance value methods and the broader milestone 5 gates
remain planned. No public struct ABI or additional platform compatibility follows.
