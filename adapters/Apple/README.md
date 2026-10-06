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

The host must order its lifecycle transitions explicitly. This adapter does not
observe UIKit/scene notifications or acquire background execution time. The scoped
policy has simulator regression evidence; real application suspension/resumption
and device execution remain separate validation gates.

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
Expo module or public mobile SDK. Physical-device and background/resume behavior
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
