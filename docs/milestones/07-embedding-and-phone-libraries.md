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
| 7c | Values, retained objects and structured exceptions | In progress: retained handles/void, exact UTF-16/Integer64 transfer, numeric records and captured type/message details implemented; reference-bearing records and real-kernel ABI composition remain open |
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

## Slice 7c.2 outcome

[ADR 0181](../decisions/0181-host-owned-utf16-copying.md) adds optional UTF-16
import/copy functions to v3. A declared alias must match the compiled closure's
exact String type. Enabling it changes the contract fingerprint and adds capability
bit 256. Disabled contracts preserve their old fingerprints and C layouts.

Imports copy into managed immutable strings and return retained handles. Copies
query required length or fill caller-owned buffers, preserving every UTF-16 unit,
with no partial writes or terminators. Empty strings have nonzero handles; null
handles are rejected for copying. Allocation/handle budgets and the existing
admission guard cover imports, copies, invocation, release and destruction.

The focused string/handle/module run passed 42 checks. The two subsequently added
O0/O2 string race cases passed separately (44 checked cases total); the combined
suite was not rerun after that test-only addition. C11 consumers exercise all
65,536 code units against an authored .NET sequence, managed concatenation/length/
indexing, source/output ownership, short buffers, aliases, stale/foreign/wrong-type
handles, handle/heap quotas, reservation cancellation and collection pressure.
Native race harnesses cover copy/import/release with forced collection and explicit
BUSY rejection. Native builds use ASan/UBSan, no C++ exceptions/RTTI and the 2 KiB
frame warning gate.

Review covered descriptor checks, host/native ownership, publication without an
intervening managed allocation, length overflow bounds, output/error ordering,
cleanup and optional capability negotiation. No unresolved defect was found in
this slice. The initial sandbox blocked MSBuild IPC; the bounded local tests passed
with approved local execution. Local main remains an ancestor; the successful
read-only remote query advertised no branch heads. No push or hosted work ran.
Wide/value transfer and richer exceptions remain ahead of the iOS adapter.

## Slice 7c.3 outcome

[ADR 0182](../decisions/0182-exact-integer64-embedding.md) adds Integer64 export
shapes to v3. The existing UInt64 payload carries every bit; mixed-width signatures
are checked before lowering. Since the compiler uses one storage type for `long`
and `ulong`, the boundary specifies bit patterns, while managed operations retain
signed/unsigned arithmetic. Wide exports add capability bit 512 and their manifest
signatures affect the hash. Existing records and contracts remain compatible.

The focused wide/string/handle/module run passed 50 checks. Two additional O0/O2
ordinary-object packet checks passed separately, bringing the validated scope to
52; the combined suite was not rerun after that test-only addition. A C11 consumer
supplies 2,704 .NET/native comparisons across eight operations and a 13-by-13 edge
matrix, including values beyond 2^53, signed/unsigned limits, checked/wrapping
addition, division and mixed-width shifts. Additional cases cover object fields,
mixed handle/wide calls, void setters, zero-arity results, malformed tags before
side effects, zero handle quotas and execution-arena failure. Native builds use
ASan/UBSan, disabled C++ exceptions/RTTI and the 2 KiB frame gate.

Review covered signature width before lowering, manifest/capability selection,
full-width packet/result storage, unsigned bit preservation and error cleanup.
No unresolved defect remains in this slice. Local main remains an ancestor; the
read-only remote query advertised no branch heads. No push or hosted work ran.
Structured value and richer exception transfer remain ahead of iOS packaging.

## Slice 7c.4 outcome

[ADR 0183](../decisions/0183-retained-exception-capture.md) adds optional exception
capture and detail copying. A capturing call reserves one handle after validating
inputs, reuses it for a returned object or escaping exception, and cancels it for
scalar/void success. Fatal failures can return no managed exception object while
still copying their original fault code/origin. The ordinary invoke API remains
available with its previous quota behavior.

A captured exception uses existing retain/release ownership. Type/message copying
uses caller-owned UTF-16 buffers and returns declaration identity separately from
throw origin. Length queries and short-buffer retries never reexecute the export.
Lazy message formatting may collect or fail within the runtime's existing budgets;
the handle roots its inputs and the original copied fault remains intact.

The focused exception/wide/string/handle/module run passed 55 checks. Two new O0/O2
copy/release/collection race cases passed separately (57 validated cases total);
the combined suite was not rerun after that test-only addition. C11 consumers
compare explicit/default/parameter-formatted messages, null throws and division
faults with .NET, preserving embedded zero and surrogate units. Other checks cover
caught/successful calls, source origins, aliases after release, later collection,
wrong/stale/foreign handles, one-slot reservation reuse, zero quotas, fatal arena
failure and failure during lazy message allocation. Native builds use ASan/UBSan,
no C++ exceptions/RTTI and the 2 KiB frame warning gate.

Review covered optional schema negotiation, input validation before reservation,
root ownership during capture/formatting, no post-formatting allocation, descriptor
validation, output/error order and cleanup. No unresolved defect remains in this
slice. Local main remains an ancestor; the read-only remote query advertised no
branch heads. No push, hosted work or phone execution ran. Structured application
values remain before iOS packaging; inner traversal and stack traces are not
provided by this bounded capture API.

## Slice 7c.5 outcome

[ADR 0184](../decisions/0184-numeric-record-embedding.md) adds bounded numeric
records with stable record/field IDs. Public nested values containing 1..64
Int32/Integer64 leaves copy through flattened tagged fields; the host never
depends on managed/native struct layout. A record-aware invocation checks output
capacity before running managed code and copies fields only after success. It
reuses existing scalar value lowering and checked internal return locations,
with execution-budget storage and optional retained exception capture.

The combined record/exception/wide/string/handle/module run passed 70 checks and
found one new O0 return-adapter frame exceeding the existing 2 KiB limit. Splitting
return handling into a helper and using loops for field setup fixed the issue
without raising the limit. The final focused record run passed all 16 checks,
including two additional full-64-field regressions. This provides passing evidence
for 73 distinct cases across the runs; the combined suite was not rerun after the
record-only fix. Native O0/O2 ASan/UBSan builds keep C++ exceptions/RTTI disabled.
C11 consumers compare nested copies, reordered field IDs, wide wrapping and
mixed handle/scalar/record calls with .NET. Other checks cover forced collection,
copy ownership, private/reference leaf rejection, short buffers before effects,
malformed tags, exception capture, zero quotas and execution-arena failure.

Review covered logical and lowered signatures, field-order mapping, return
borrow lifetime, publication after success, cleanup, capabilities/fingerprints
and stack/resource bounds. No unresolved defect remains in this slice. Local
main remains an ancestor; the remote advertised no branch heads. No push, hosted
work or phone execution ran. Reference-bearing records, public ref/out and empty
records remain unsupported; kernel composition and iOS packaging are still ahead.

## Remaining exit gates

- Complete structured application value transfer and compose the existing
  ownership/error APIs with the real kernel before connecting a host adapter.
- Pin target toolchains and build/package reproducibly using local tools first.
- Obtain physical iOS and Android device evidence. Desktop or simulator tests
  alone do not close this milestone.
- Review rooting, resource bounds, C layout, error order and lifecycle races for
  each slice, and keep compatibility claims limited to measured targets.

No hosted jobs, external publication, paid services or additional agents are
authorized by this plan. No mobile execution has been verified yet.
