# Milestone 7: C ABI and phone libraries

Started 2026-10-06. In progress; device exit gates remain open.

Milestone 6 proved the bounded compiled kernel on macOS ARM64. This milestone
makes compiled closures embeddable through a versioned C interface, then packages
and tests them on phones. iOS comes first, as requested. Expo/TypeScript bindings
and the offline app flow belong to milestone 8; browser parity is milestone 9.

## Delivery slices

| Slice | Deliverable | Evidence / status |
| --- | --- | --- |
| 7a | Persistent scalar C boundary | Implemented through `EmbeddingCppEmitter`; C11 consumer, shared-library lifecycle and concurrency tests on macOS ARM64 |
| 7b | Export manifest and shared instance dispatch | Planned: stable application export IDs, signature/schema validation, capabilities and configurable bounded quotas; all exports in one engine share its heap |
| 7c | Values, retained objects and structured exceptions | Planned: exact UTF-16/64-bit transfer, caller/engine buffer ownership, retained-handle release, exception type/message/origin and stale/foreign-handle tests |
| 7d | iOS library and native adapter | Planned: pin local Apple SDK/targets, package a library/header, exercise repeated calls and create/destroy on a physical iOS device |
| 7e | Android library and native adapter | Planned: pin NDK/targets, package library/header, exercise repeated calls and create/destroy on a physical Android device |
| 7f | Composed lifecycle review | Planned: native adapters run equivalent kernel transcripts, repeated teardown/recreation, fault recovery and retention/collection stress; record exact devices, OS and toolchains |

## Slice 7a boundary

[ADR 0178](../decisions/0178-persistent-scalar-c-abi.md) and the
[ABI contract](../embedding-abi.md) define the admitted scope. A compiler-library
API emits a C header plus C++ for one validated Int32 export. Persistent instances
reuse the existing collector, static roots and initialization state. Each call
gets fresh execution frames and pending-fault state. Opaque nonreused runtime
tokens replace native heap addresses; overlapping entry or destruction is rejected.

One export closure owns each registry. Tokens must only be used with the matching
generated functions. This is the initial versioned embedding profile, not a
finished application SDK, a multi-export engine or a stable application export
manifest. The old experimental per-call boundary remains unchanged.

Local tests compile a separate C11 consumer and link it to an emitted C++ shared
library with hidden implementation symbols. O0/O2 use ASan/UBSan with native
exceptions/RTTI disabled. Repeated native calls compare with an authored .NET
stateful/throwing probe, normally and with collection before every allocation.
Coverage includes separate instance state, recovery after faults, copied origin
lifetime, caught-fault success, stale tokens, slot reuse, invalid arguments/version/
flags, registry exhaustion and concurrent invoke/destroy races.

The 16-check targeted scope covers `EmbeddingAbiTests`, `StaticStorageTests` and
`NativePackedBoundaryTests`. Fourteen passed in the combined run; the two new
zero-arity/concurrency cases initially rejected an overinclusive test catalog.
After removing the unused fixture types, both passed in a focused rerun. All final
cases have passing evidence; the combined scope was not rerun after that test-only
correction. The C consumer supplies 1,200 stateful .NET/native call outcomes across
O0/O2, ordinary/forced collection and three fresh-instance cycles. The C++ harness
also exercises zero arity, independent host threads and token exhaustion.

Slice review covered C linkage/layout, unsigned token exhaustion, fresh call state,
root/arena cleanup before admission release, copied origin ownership, and registry
ordering during invoke/destroy/reuse. No unresolved defect was found in this scope.
The local `main` commit `48ecf14` remains an ancestor of the working branch; the
read-only remote query advertised no branch heads, so no newer main was available
to integrate. No push or hosted work ran.

## Remaining exit gates

- Complete multi-export dispatch, data/handle ownership and rich exception copying
  before connecting the real kernel to a host adapter.
- Pin target toolchains and build/package reproducibly using local tools first.
- Obtain physical iOS and Android device evidence. Desktop or simulator tests
  alone do not close this milestone.
- Review rooting, resource bounds, C layout, error order and lifecycle races for
  each slice, and keep compatibility claims limited to measured targets.

No hosted jobs, external publication, paid services or additional agents are
authorized by this plan. No mobile execution has been verified yet.
