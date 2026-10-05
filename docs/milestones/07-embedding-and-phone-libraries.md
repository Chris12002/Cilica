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
| 7b | Export manifest and shared instance dispatch | Implemented for exports from one validated closure: stable application IDs, signature/schema checks, capabilities and lowerable per-instance quotas; independent multi-root discovery remains planned |
| 7c | Values, retained objects and structured exceptions | In progress: 7c.1 typed retained handles and void results implemented; UTF-16/64-bit/value copying and detailed exception transfer remain planned |
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

## Slice 7b outcome

[ADR 0179](../decisions/0179-shared-export-module.md) implements
`embedding-module-int32-v2`. `EmbeddingModuleEmitter` emits a C header/source and
canonical export manifest for 1..64 explicitly selected methods from a single
validated closure. IDs/names and schema version are application-owned; the host's
expected contract fingerprint is checked before instance creation. All exports
share the same managed heap, static roots and initialization state.

Composed export admission checks logical signatures before scalar lowering, so
flattened structs cannot accidentally cross the Int32 boundary. Explicit capability
bits and copied configuration describe heap/object/execution limits, which can only
be reduced from the established defaults. Shared admission covers every export,
and per-export packet helpers preserve the 2 KiB native-stack gate at full capacity.
V1's interface remains available and uses the same private registry implementation.

A separate C11 consumer compares 800 step/read outcomes with the authored .NET
oracle across O0/O2 and ordinary/forced collection. It also verifies reset/recovery,
instance isolation, manifest negotiation, error order, copied fault state and each
quota. Native 64-export checks exercise all IDs, concurrent entry and registry
capacity. Contract tests cover canonical ordering, stable interface hashes when
bindings change, wrong/private/missing methods and inline-value signature rejection.

No independent-root merger or compiler discovery API is introduced: the selected
methods must already be reachable through one input root, for example a facade.
The root is never executed at create time. This keeps one validated metadata
closure and its existing aggregate limits rather than weakening validation.
Object/value transfer and rich exception copying remain the next milestone slice.

The final 25-check scope has passing evidence: the combined v1/v2 run passed 23
checks, then all 16 negative-contract cases passed after adding reserved-identifier
validation and its two cases. Native code was unchanged by that final name check;
the combined suite was not rerun. Builds reported no warnings/errors. Review
covered original versus lowered signatures, manifest stability, output/error order,
heap and execution quotas, token/slot lifetime, GC roots and guarded cleanup. No
unresolved defect remains in the admitted scope. Local main remains an ancestor;
the read-only remote query still advertised no branch heads. No push or hosted
work ran.

## Slice 7c.1 outcome

[ADR 0180](../decisions/0180-public-retained-object-handles.md) implements
`embedding-object-handles-v3` through `EmbeddingObjectModuleEmitter`. Explicit
contracts declare reference aliases and Int32/reference/void signatures in one
validated closure. The C header exposes tagged values and opaque object leases,
with retain/release sharing the existing runtime admission guard. No managed
address crosses the boundary. V1/V2 remain available.

Each reference result reserves one strong-handle slot before executing managed
code, then publishes without an intervening managed allocation. This preserves
returned roots and rejects quota exhaustion before application effects. Faults
and null returns cancel reservations. Inputs are validated for kind, runtime
ownership and assignability before execution. Alias/clone tokens retain the same
object independently; releasing a token removes only that root. Destroy invalidates
all leases from the instance. Handle quotas may be lowered from 1,024 to zero.

The focused v1/v2/v3/private-tracing run passed 43 tests. Two additional ordinary
object-emitter cases passed at O0/O2, bringing the checked scope to 45; the combined
suite was not rerun after that test-only addition. Native C11 consumers and C++
race harnesses use ASan/UBSan, disabled C++ exceptions/RTTI and the 2 KiB frame gate.
Evidence covers .NET alias/mutation equivalence, base-type admission, stale/foreign/
wrong-type handles, nulls, malformed tagged values, collection under small quotas,
released cycles, reserved-slot fault cleanup and concurrent invoke/retain/release.

Review covered generated native ownership, allocation safepoints, handle token
exhaustion, admission/cleanup ordering, contract validation and the shared v2/v3
lifecycle extraction. No unresolved defect was found in the admitted scope. Local
main remains an ancestor and the read-only remote query advertised no branch heads.
No push, hosted work or phone execution ran. String/value copying and richer
exception transfer remain open before the iOS adapter.

## Remaining exit gates

- Complete data/handle ownership and rich exception copying
  before connecting the real kernel to a host adapter.
- Pin target toolchains and build/package reproducibly using local tools first.
- Obtain physical iOS and Android device evidence. Desktop or simulator tests
  alone do not close this milestone.
- Review rooting, resource bounds, C layout, error order and lifecycle races for
  each slice, and keep compatibility claims limited to measured targets.

No hosted jobs, external publication, paid services or additional agents are
authorized by this plan. No mobile execution has been verified yet.
