# ADR 0197: Index validated allocation addresses during collection

Status: accepted. Recorded 2026-10-20.

## Motivation

After ADR 0196, the current DiffPlex sample attributes 956 of 2,117 exclusive
application samples to collector membership validation. Unlike trusted internal
access, collector roots and edges must still reject invalid pointers before
reading headers. Repeated linear allocation-list searches add avoidable work.

## Decision

Keep the allocation list as the authoritative inventory. After its complete
owner/descriptor/count/byte validation, build an open-addressed address index
with at most half its slots occupied. `Instance::owns` uses it only while the
inventory is immutable during marking. Hashing and equality inspect addresses,
never the candidate header. `traceable_reference` still checks owner and exact
descriptor identity after successful membership validation.

The table is disabled before diagnostic classification, sweeping and every
return from the marking scope. Busy/reentry rules remain unchanged. Roots,
fields, arrays, handles, descriptors, fault origins, accounting and the rule that
incomplete marking cannot sweep are unchanged. Public boundary checks outside
marking still search the authoritative allocation list. No membership check is
gated by either diagnostic macro.

The table uses a lazy, reusable native `nothrow` allocation. Failure or capacity
overflow leaves the existing checked linear search in use, without a new managed
fault or a reduced collection capability. An unsuccessful growth preserves the
old buffer for later reuse. Rebuilds clear the active portion, so stale addresses
from prior collections cannot become roots. Buffer contents are never traced.

At the default 4,096-object marking bound, retained scratch is at most 8,192
pointers (64 KiB on the measured 64-bit host), plus three private instance fields
(24 bytes). Growth temporarily holds old and new buffers; the last default-bound
growth can use 96 KiB until the old buffer is freed. The buffer retains its high
water capacity until instance destruction. It is native overhead outside the
managed byte quota, like execution storage; it does not consume managed object
headroom. Custom larger marking limits can require larger scratch. No large
native stack array is introduced.

Native emission advances to v91 for the changed private instance layout.
Managed object headers, IR profiles, grants, owned core/library and public ABI
remain unchanged. Historical allocation-free marking describes the older
checkpoint; current marking optionally allocates native scratch and always has
the allocation-free fallback. Standalone precise visitors remain allocation-free.

## Review and validation

Review checked table sizing overflow, a terminating empty slot at half load,
address-only rejection before dereference, enablement only after full inventory
validation, invalid/limit/stopped cleanup, deactivation before destruction and
native buffer release. No managed allocation or callback occurs during index
construction. The single-thread instance/no-topology-mutation contract is retained.

Four new O0/O2 ASan/UBSan cases, with internal verification both enabled and
disabled, cover initial/growth allocation failure, cache reuse, foreign/forged/
interior pointers, invalid roots and child edges, budget/visitor stops, reclamation,
empty heaps and subsequent allocations. Existing tracing tests assert inactive
lookup state after every pass and before classification callbacks. Twelve focused
and 42 adjacent checks pass, along with seven separate-module checks (61 total).
Fresh paired DiffPlex medians improve from 531.794 to 365.227 microseconds/op,
1.4561x faster. Four full transcripts and two forced-GC operations pass. The
[performance report](../performance/gc-membership-20261020.md) retains exact
revisions, inputs, raw timings, source comparison and remaining costs.
