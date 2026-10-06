# Swift runtime owner

Generate a Swift 6 adapter for one [packaged v3 module](../../tools/Apple/README.md):

```bash
python3 -B adapters/Apple/generate.py \
  --package out/apple-engine --output out/CilicaEngineRuntime.swift
```

Add that source and the matching XCFramework to your native target. The generator
checks the manifest against the package receipt, supports the current numeric
record shapes and refuses to overwrite a source file. It generates
`<Module>Runtime`; the examples below use a module named `CilicaEngine`.

Each runtime is an actor with its own serial Dispatch queue. Native calls and
buffer copies finish without suspension points, so concurrent Swift callers never
overlap native entry on that runtime. Work does not run on the UI actor. Ordering
between concurrent submissions is unspecified; await each command in sequence when
application ordering matters. This queues synchronous work, not managed async/Task
execution. Cancelling a Swift task does not cancel or roll back an admitted call.

```swift
let runtime = try await CilicaEngineRuntime.open()
let outcome = try await runtime.invoke(1, arguments: [.int32(42)])
if let fault = outcome.fault {
    // Preserve this copied code, method, offset and image hash.
    print(fault.code)
}
try await runtime.close()
```

`invoke` takes logical parameters. Use `.int32` for signed Int32 values,
`.integer64` for exact UInt64 bit patterns, `.handle(handle)` or `.handle(nil)` for
references, and `.record(id, fields)` for numeric values. Record fields follow the
manifest's ascending field-ID order. The adapter checks record IDs, counts and
scalar widths and flattens fields before entering C. Output capacity comes from
the manifest, so it never reexecutes an export to discover result size. Outcomes
contain a value, optional copied fault and optional captured exception lease.

Managed failures return an `Outcome` with `fault`; admission, ownership, argument
and copy failures throw `Failure`. Native status codes remain available as
`Failure.native(code)`. Managed effects before a fault are not rolled back.
`captureException: true` requests a lease when the module supports capture and
may exhaust the handle quota before executing. Fatal failures can have a fault
without an exception object. A module without that capability rejects the request
with `Failure.unsupported` and emits no reference to missing optional C functions.

A `Handle` is a Sendable opaque lease carrying its runtime identity. Copying the
Swift value copies the same lease, not ownership in the managed runtime. Use
`clone` to obtain another independent lease; use `release` once per lease. The
adapter rejects foreign and released handles locally. Null references have no
lease and consume no quota. Close invalidates all leases, including discarded
results or retained exception handles. Do not mix raw native calls with adapter
ownership for the same runtime.

Call `close()` explicitly for deterministic cleanup; it is idempotent and shares
the worker with invocation/release. Submitted work either runs before close or
receives `Failure.closed`. The final actor destructor queues fallback destruction
on its worker, but its timing is not promised and is not a substitute for close.

## Pausing command admission

Use an awaited `pause()` when a host needs to stop admitting new work while
preserving its runtime and handles. It returns an opaque runtime-scoped `Pause`:

```swift
let hold = try await runtime.pause()
// Admission is now paused. Retained results can still be copied or released.
try await runtime.resume(hold)
```

This is a worker barrier: a native operation already executing finishes before
the pause takes effect. Tasks queued before `pause()` can execute on either side
of it because actor scheduling is not FIFO. Do not treat starting a Task that
calls `pause()` as an immediate admission stop; await completion. While any hold
exists, `invoke` (including read exports), `importUTF16` and `clone` throw
`Failure.paused` before argument checks or native entry. Rejected commands are
not queued for replay. The host decides whether to submit them after resuming.

Each caller owns its hold and must explicitly pass it to `resume`. Multiple holds
compose: admission resumes only after all are removed. Copying a Pause value
aliases the same hold; consuming it twice, using a foreign hold or reusing an old
hold throws `Failure.invalidPause` without changing admission. At most 64 holds
are allowed; exhaustion throws `Failure.pauseLimit`. Dropping a token or cancelling
a task does not release it. Keep each hold until resume or close.

The actor's `lifecycle` reports `.active`, `.paused` or `.closed`; it is an
observation, not a reservation for a later call. Copying text, inspecting retained
exceptions, releasing handles and closing remain available while paused. These
may still run native code; exception formatting may allocate within native quotas.
Close clears all holds and leases. Calls after close report `Failure.closed` and
resume cannot reopen the runtime. Pausing preserves memory only: it provides no
disk checkpoint or guarantee that the OS will keep a background process alive.

The host must order its lifecycle transitions explicitly. The optional scene host
below supplies that ordering for forwarded UIKit callbacks. Neither owner acquires
background execution time or guarantees OS suspension/resumption behavior.

## UIKit scene host

Add `--scene-host` when generating to include `<Module>SceneHost`. The default
runtime source remains independent of UIKit. Each scene host creates and owns one
runtime; use a new host when a scene reconnects after disconnection. It holds a
weak scene reference and its own scoped pause token. It does not take over the
app's scene delegate or register global notification observers.

In your `@MainActor` scene delegate, open the host from `willConnectTo` and keep
errors visible to your application. Store the returned host, then immediately call
`refreshSceneState()` **before any further await**; this samples events that may
have occurred while opening/storing it. An unattached scene closes the host.

```swift
let opened = try await CilicaEngineSceneHost.open(for: scene)
self.host = opened
opened.refreshSceneState()
try await opened.synchronize()
```

Forward these delegate methods synchronously to the stored host, passing the same
scene: `sceneDidBecomeActive`, `sceneWillResignActive`, `sceneDidEnterBackground`
and `sceneDidDisconnect`. Do not wrap forwarding in separate Tasks. Foreign-scene
callbacks are ignored. The [test app](../../tests/Apple/SceneApp.swift.in) shows
the wiring and error handling in a minimal UIKit app.

Call `host.invoke`, `host.importUTF16` (when supported) and `host.clone` for UI
commands. Resign/background closes this main-thread admission gate immediately,
then one reconciliation task pauses the worker. Rapid events update the desired
state without accumulating per-event tasks or holds. A stale resume completion
cannot reopen UI admission. The active callback requests resume; `isActive`
becomes true only after reconciliation. Commands during transition throw `paused`
and are not replayed. Already-admitted operations can finish or encounter the
runtime pause; native calls are never forcibly interrupted.

`host.runtime` is available for copies, fault inspection, release and independent
pause holds. Do not bypass host admission by invoking/importing/cloning through
that property or close the owned runtime directly. A separate runtime pause still
rejects commands even when the scene host's own `isActive` is true. Observe
`transitionFailure` or await `synchronize()` to surface a transition failure;
failure keeps UI admission closed. Explicit `host.close()` can clean up after a
failed transition. Disconnect immediately blocks commands and requests runtime
destruction; `synchronize()`/`close()` can await completion. Subsequent active
callbacks cannot reopen it. Final destruction remains fallback cleanup only.

The test app has real iOS 26.1 simulator foreground/background/foreground callback
evidence in normal/forced collection modes. Event-storm, pause-failure and forwarded
disconnect checks are synthetic within that app. Physical devices, multiple
simultaneous windows, actual OS suspension/process eviction, background deadlines
and disk restoration remain unverified. This host does not supply game commands,
an Expo module or a complete application SDK.

## Copied text and failures

When the manifest enables UTF-16, `importUTF16` and `copyUTF16` transfer `[UInt16]`.
This preserves embedded zeros and unpaired surrogates; no implicit Swift String
normalization occurs. `String(decoding: units, as: UTF16.self)` is a deliberate
lossy presentation choice for malformed UTF-16. Copies are independent of runtime
lifetime. Each import/copy is limited to 524,288 units; callers can lower copy
limits, and native heap quotas still apply. Query/copy remain within one actor
operation with no interleaving.

When capture is enabled, `exceptionDetails` copies type/message units and source
declaration identity from a retained exception. The combined text limit is
524,288 units. A detail-copy failure does not replace the original invocation
fault. Release the exception lease when finished. Inner exceptions and stack
traces remain unsupported by the underlying ABI.

This is a bounded runtime adapter, not a complete application command API, UI,
Expo module or public mobile SDK. Physical-device and OS suspension/resume behavior
remain unverified. Simulator evidence covers iOS 26.1; the package's iOS 17 build
floor remains a setting rather than an earlier-OS execution guarantee.

## Local validation

Fast generator regressions:

```bash
python3 -B -m unittest discover -s adapters/Apple -p 'test_*.py' -v
```

The opt-in integration uses the public session fixture, builds both Apple slices,
links a Swift consumer and runs on an explicitly selected already-booted simulator:

```bash
CILICA_APPLE_SIMULATOR=YOUR-BOOTED-SIMULATOR-UUID \
  dotnet test tests/Cilica.Metadata.Tests/Cilica.Metadata.Tests.csproj \
  --no-restore -m:1 --filter FullyQualifiedName~AppleAdapterTests
```

Without that variable the integration test is skipped. Set `CILICA_APPLE_OUTPUT`
to an absolute new directory to retain package/source/log evidence; otherwise
its disposable test directory is cleaned up. Native subprocesses are bounded.
No simulator is started/stopped, no app is installed/signed and no hosted work runs.
The consumer checks concurrent mutation, exact UTF-16/wide record copies, malformed
record rejection, stale/foreign leases, exception recovery, copy/handle quotas and
close racing with queued operations. It also checks overlapping pauses, stale and
foreign holds, the 64-hold bound, copies/cleanup while paused, state preservation,
admission races and close while paused under normal/forced collection.
The private kernel separately passes 1,792
exact comparisons through this Swift adapter, with normal/forced collection.

For another consumer, pass `--adapter --consumer path/to/Consumer.swift.in` to
[check_package.py](../../examples/Apple/check_package.py); module/prefix placeholders
are replaced as in the lifecycle template. Native libraries can be reused when
only the Swift adapter/consumer changes.

The separate opt-in [UIKit check](../../tests/Apple/README.md) temporarily installs
two uniquely identified apps, switches their foreground status and uninstalls
both. It is never run by the ordinary CLI consumer or default .NET tests.
Its shared app shell accepts contract-specific scenarios and repeats numbered
background/foreground cycles. The public scenario retains fault details and text
across mutations; a private real-kernel scenario compares each resumed state with
its existing .NET transcript. This remains a scripted integration check rather
than a complete application command adapter.
