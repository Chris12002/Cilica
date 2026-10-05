# ADR 0180: Public retained object handles

Accepted 2026-10-06; milestone 7c.1. Desktop evidence only.

## Context

The v2 module shares persistent state, but exports only Int32. Hosts need to keep
managed objects alive across calls without exposing native addresses. The runtime
already traces private strong handles; adding another object ownership system
would duplicate collector rules.

## Decision

Add `embedding-object-handles-v3` through `EmbeddingObjectModuleEmitter`, preserving
v1/v2. An explicit contract selects public ordinary static methods in one validated
closure. Logical signatures admit Int32, named reference aliases and void results;
flattened structs, borrows and wide values cannot silently cross this boundary.
Canonical reference aliases/signatures participate in the interface fingerprint;
exact image/type/method bindings remain separate implementation metadata.

Values use a fixed tagged C record. Object tokens are independent strong leases
owned by one runtime in one emitted module; zero means null. Retain creates another
lease, release removes one root, and destroy invalidates every lease. Nonreused
tokens reject stale and foreign-instance use. Token equality is not object identity.
The same admission guard serializes invoke/retain/release/destroy. Inputs remain
rooted by their leases while native execution packets and managed objects allocate.
The adapter validates tags, ownership and runtime assignability before execution.

A reference result reserves a null strong-handle slot before managed execution.
Once the method returns, its pointer is checked and published to that slot without
a managed allocation or collector safepoint. Reservation failure therefore cannot
follow application effects; null returns and faults cancel the slot. This costs a
temporary slot even when the eventual result would be null or the call would throw.
It is preferable to executing first and failing to transfer a successful result.
Handle capacity is independently lowerable from 1,024, including to zero.

V2/V3 share lifecycle generation so allocation, registry admission and cleanup keep
one implementation. Per-export helpers retain the established native frame bound.

## Evidence and limits

Local macOS ARM64 C11 consumers and C++ O0/O2 ASan/UBSan harnesses cover clone/alias
identity against .NET, type and owner rejection, nulls, release, collection and
cycles, quota preflight, managed faults, fresh instances and admission races.
Both ordinary object and composed exception emitters are exercised. Generated
code uses no C++ exceptions/RTTI and passes the 2 KiB frame warning gate.

Review checked rooting at the result handoff, input lifetime during allocation,
lease cancellation, unsigned token exhaustion, race/teardown ordering and schema
validation. No unresolved defect remains in the admitted scope. Rich exception
type/message transfer, exact string/wide/value copying, independent export-root
discovery and phone packaging/device execution remain separate slices. This is
not a complete embedding SDK or an iOS compatibility claim.
