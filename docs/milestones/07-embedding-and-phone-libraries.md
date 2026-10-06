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
| 7c | Values, retained objects and structured exceptions | Bounded desktop checkpoint validated: retained handles/void, UTF-16/Integer64, numeric records and captured type/message details; scripted persistent real-kernel C ABI transcript parity. Reference-bearing records remain outside this checkpoint |
| 7d | iOS library and native adapter | In progress: pinned XCFramework, Swift runtime owner, scoped pauses and UIKit scene host; simulator kernel/concurrency and real foreground/background checks pass. Full application commands, OS suspension and physical-device evidence remain open |
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

## Slice 7c.6 outcome

[ADR 0185](../decisions/0185-persistent-kernel-abi-composition.md) validates the
existing ABI against the unchanged private kernel. A scripted adapter retains a
session between calls, advances one probe command, returns a 13-field numeric
observation and exposes canonical text as a retained string. The .NET adapter's
ten rows match the milestone 6 oracle exactly, apart from its harness entry token.
The copied kernel/primitives image hashes still match the pinned milestone 6
build; the reviewed 6d.23 core/digest is unchanged. The new closure has 363 methods.

Separate C11 consumers link the generated shared library. O0/O2 ASan/UBSan runs,
normal/forced collection and three runtime recreation cycles per mode pass
**10,752 exact comparisons**: 130 numeric fields and 766 UTF-16 units per pass.
Additional assertions cover short-buffer preflight before state changes, cloned
session ownership, retained early snapshots, foreign/stale tokens, independent
runtimes, destruction with live handles and copied text surviving teardown.
Native builds preserve the 2 KiB frame warning gate and disable C++ exceptions/RTTI.
Private evidence, source/output hashes and logs are in
`artifacts/milestone7-kernel/evidence-7c6.json`.

The public `EmbeddingSessionTests` adds two passing O0/O2 cases so this ownership
pattern remains reproducible without the private kernel. Across both builds it
performs 144 successful mutations, using the same retained session after an
escaping managed exception, and later inspects the captured failure and immutable
snapshot. Imported embedded-zero/surrogate text, exact wide totals, numeric
records, list allocations, forced collection and repeated teardown are composed.
Fixture preparation encountered existing unsupported formatting/concatenation
overloads; it now explicitly selects the admitted invariant/two-string APIs.
The private adapter uses scalar locals for conditional record fields, preserving
the existing restriction on locations across block joins. No kernel rule,
compiler/runtime behavior, ABI schema or quota was changed.

Review covered provenance, transcript parity, host buffer bounds, preflight
effects, ownership across allocations, isolation and teardown. No unresolved
defect remains in this checkpoint. Local main is still an ancestor and the remote
advertised no branch heads. Only focused local builds/tests ran; no push, hosted
work or phone execution ran. This is a scripted integration, not a general app
adapter. Reference-bearing records are unnecessary for this measured contract
and remain unsupported; iOS packaging is the next delivery slice.

## Slice 7d.1 outcome

[ADR 0186](../decisions/0186-apple-static-packaging.md) adds a local package builder
for emitted v3 modules. It pins Xcode 26.1.1 (17B100), Apple Clang 17.0.0
(clang-1700.4.4.1), iOS/iOS Simulator SDK 26.1 (23B77) and arm64. It creates separate
static device/simulator archives with a C header, Apple typed-constant bridge,
Clang module map and a build receipt containing commands and hashes. The iOS 17.0
deployment floor is not evidence of runtime compatibility with that OS.

The existing kernel module packages successfully. Both objects have the correct
IOS/IOSSIMULATOR build metadata; the original header and archives are verified
inside the XCFramework. Native O2 builds retain the 2 KiB frame warning gate,
warnings as errors and disabled C++ exceptions/RTTI. Package archives do not link
sanitizers. Initial/final native archives have identical hashes.

A C11 consumer linked against the packaged simulator archive passes **5,376 exact
comparisons** on the already-booted arm64 iPhone 17 Pro simulator running iOS 26.1
(23B86).
The unchanged kernel transcript runs three recreation cycles each with normal and
forced collection. Swift 6.2.1 imports the C module, links both device/simulator
executables and passes six simulator lifecycle cycles, layout/negotiation checks,
unknown-export handling and stale-runtime rejection. The first Swift build exposed
unimportable numeric macros; the Apple header now supplies typed aliases without
changing the core ABI. Explicit SDK selection removed the initial Swift linker's
sysroot warning. Final consumer build logs are clean.

Three focused Python checks pass for schema/artifact mismatches, output protection,
invalid module names and incompatible toolchain preflight. Review covered input
validation, subprocess bounds, slice separation, linkage, C ownership and package
provenance. No unresolved defect remains. Private receipt/log hashes and simulator
evidence are in `artifacts/milestone7-ios/evidence-7d1.json`; public instructions
are in [tools/Apple](../../tools/Apple/README.md) and
[examples/Apple](../../examples/Apple/README.md).

No connected physical device was found. Device builds were cross-compiled/linked
only; no signing, app installation, simulator startup/shutdown, hosted work or push
ran. Local main remains an ancestor; the remote advertised no branch heads. The
Swift consumer is a lifecycle smoke test, not the complete native application
adapter. Physical-device and UI/background lifecycle validation remain open.

## Slice 7d.2 outcome

[ADR 0187](../decisions/0187-swift-runtime-ownership.md) adds a generated Swift
runtime owner. One actor and serial Dispatch worker own each runtime, keeping
invocation, copies, lease management and close free of suspension points. Logical
record shapes come from the manifest. Handles carry runtime identity and explicit
clone/release ownership. Exact UInt16 copies and fault/exception details become
independent Swift values. Explicit close deterministically destroys remaining
leases; final actor destruction provides asynchronous fallback cleanup.

The opt-in public session test passes on the iOS 26.1 simulator and links a device
consumer. It covers 48 concurrent updates across normal/forced collection,
signed/full-width record inputs/results, malformed record IDs, stale/foreign
handles, immutable snapshots after mutation, captured fault recovery and detail
copying, host copy bounds, zero handle quota and close racing with queued work.
The existing kernel transcript also passes **1,792 exact comparisons** through
the Swift adapter, with normal/forced collection. Both target consumers compile
under Swift 6 with warnings as errors; physical-device execution is not implied.

The final focused run passes three tests (Apple integration plus the existing
O0/O2 C session cases). Three generator regressions pass; plain and capture-enabled
no-record variants additionally pass Swift type checking. Review found an unused
mutable capture slot in the plain variant, fixed without changing other variants.
The Apple integration is confirmed skipped unless a simulator is explicitly
selected; normal test runs do not start Apple work. The initial sandbox attempt
could not establish MSBuild IPC; validation used authorized local IPC access.

Review covered worker isolation, lease scope, record mapping, exact bits/text,
failure lifetime, bounded allocations and optional capability paths. No unresolved
implementation defect remains. See [adapter usage](../../adapters/Apple/README.md)
and private `artifacts/milestone7-swift/evidence-7d2.json` for exact evidence.
Local main remains an ancestor; the remote advertised no branch heads. No push or
hosted work ran. Application-specific commands, UIKit/background lifecycle and
physical-device validation remain open.

## Slice 7d.3 outcome

[ADR 0188](../decisions/0188-swift-admission-pauses.md) adds scoped admission
pauses to the Swift runtime owner. Awaiting pause establishes a worker barrier;
up to 64 overlapping holds preserve state and leases until each owner resumes.
Invoke/import/clone reject while paused, before native entry. Text/fault copying,
release and close remain available. Foreign or stale resume tokens cannot undo
another hold, and close permanently invalidates all holds and leases.

The public Swift consumer passes normal/forced-collection checks for nested holds,
token scope/consumption, hold exhaustion, paused copies/cleanup, state preservation,
admission racing concurrent commands and close while paused. Previously queued
tasks have no FIFO guarantee; final state matches exactly the calls admitted.
The real-kernel Swift transcript adds a pause and rejected advance between every
step and still passes **1,792 exact comparisons**, including copying and releasing
snapshots while paused. Both consumers link for arm64 device/simulator under
Swift 6 warnings-as-errors; execution remains limited to the iOS 26.1 simulator.
Three generator checks pass. Existing native packages were reused; native code
and C ABI did not change or require a rebuild.

Review covered actor serialization, token ownership, admission/error order,
bounded metadata and cleanup/close semantics; no unresolved defect remains.
Usage and lifecycle ordering are documented in [the adapter](../../adapters/Apple/README.md).
Private evidence is `artifacts/milestone7-lifecycle/evidence-7d3.json`.
Local main remains an ancestor and the remote advertised no branch heads.
No hosted work or push ran. This is the host admission policy; UIKit event wiring,
real application suspension/resumption, persistence and physical-device execution
remain open.

## Slice 7d.4 outcome

[ADR 0189](../decisions/0189-uikit-scene-host.md) adds an opt-in generated UIKit
scene host. Each host owns one runtime, receives synchronous scene-delegate
forwarding and immediately blocks UI commands on inactivity. A single task
reconciles the latest desired state with its scoped pause. Startup refresh covers
missed callbacks, transition failures stay visible, and disconnect is terminal.
Independent pause holds still apply; the C ABI/native implementation is unchanged.

The public session fixture now has a [UIKit test app and bounded harness](../../tests/Apple/README.md).
Real foreground/background/foreground callbacks pass on the existing arm64 iOS
26.1 simulator, preserving session fields and exact UTF-16 across both normal and
forced collection. Both Apple target consumers link with Swift 6 warnings-as-errors.
Synthetic checks exercise rapid transitions, independent holds, pause-limit
failure, cleanup and disconnect/activation ordering. No-string/no-record variants
with capture off/on also type-check; three existing generator regressions pass.
The harness removed both uniquely identified apps after each run and recorded
cleanup results. Native packages were reused, with no native rebuild.

Review covered startup/teardown races, actor isolation, immediate admission,
failure handling, coalesced work and scene ownership. No unresolved defect remains.
Private evidence is `artifacts/milestone7-uikit/evidence-7d4.json`. Main remains an
ancestor; the remote advertised no branch heads. No hosted work or push ran.
Background callbacks do not establish actual OS suspension, process restoration,
deadline handling, simultaneous windows or physical-device execution.

## Remaining exit gates

- Define the actual mobile application command contract and connect its native
  host adapter, using the validated numeric/handle/string boundary where sufficient.
  Reference-bearing records require a separate design if that contract needs them.
- Keep the Apple pin/package checks current; pin and validate the Android toolchain
  when beginning that separate slice.
- Obtain physical iOS and Android device evidence. Desktop or simulator tests
  alone do not close this milestone.
- Review rooting, resource bounds, C layout, error order and lifecycle races for
  each slice, and keep compatibility claims limited to measured targets.

No hosted jobs, external publication, paid services or additional agents are
authorized by this plan. iOS simulator execution is verified only in the scope
above; no physical-device execution has been verified.
