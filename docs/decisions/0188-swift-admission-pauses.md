# ADR 0188: Swift admission pauses

Accepted 2026-10-06; milestone 7d.3. Host policy, simulator execution only.

## Context

The Swift owner serializes native operations and close, but a host cannot yet
stop new commands without destroying retained state. Independent lifecycle
owners can overlap: resuming one screen must not remove an application hold.
A single mutable paused flag would lose that ownership information. Wiring
platform notifications also requires a host ordering policy; actor task creation
alone does not guarantee event order.

## Decision

Add `pause()` and `resume(Pause)` to the existing runtime actor. Pause returns an
opaque runtime-scoped token representing one hold; up to 64 can coexist. Resume
consumes exactly that hold. Foreign, stale or duplicate tokens fail without state
changes. Admission resumes only after the last hold is removed. Tokens have
explicit lifetime: copying aliases a hold; dropping or cancelling does not resume.
Exhaustion reports `pauseLimit` without changing existing holds.

The pause call is a worker barrier and must be awaited. An executing operation
finishes before the pause; queued actor tasks may run on either side of it because
scheduling is not FIFO. No native call or copy suspends internally. Invoke, string
import and handle clone check active admission before validating arguments or
entering native code. Rejected work reports `paused` and is not buffered/replayed.
The host orders lifecycle events and decides whether to resubmit a command.

Copying text, inspecting retained exceptions, releasing handles and closing remain
available while paused. These can run native code; exception formatting may
allocate within existing quotas. Pause therefore gates commands, not all native
execution. State and retained leases remain in memory. It adds no disk checkpoint,
background execution allowance, UIKit observer or OS suspension guarantee.

Expose an actor-isolated active/paused/closed observation. It is not an admission
reservation. Close clears all holds and leases; subsequent calls fail closed,
including resume. All state lives in the Swift owner; C ABI layout, capabilities,
contract fingerprints, runtime quotas and emitted native code stay unchanged.

## Evidence and review

The existing opt-in public Swift consumer now checks the pause policy under normal
and forced collection: overlapping holds, foreign/consumed/stale rejection,
admission before export validation, blocked mutation/import/clone, copied text and
fault details while paused, explicit release, exact state after resume, the
64-hold limit, close while paused and closed error precedence. Concurrent command
submissions race a pause; the final managed state must equal exactly the accepted
operations, proving rejected operations have no effects. Existing concurrency,
ownership, UTF-16, record and close-race checks also pass.

The real-kernel Swift consumer inserts a pause after each transcript step, rejects
an attempted advance, copies/releases the retained snapshot while paused and then
resumes. Its 1,792 numeric/text comparisons pass across normal/forced collection.
Both consumers link for arm64 device and simulator using Swift 6 with warnings as
errors; only the existing iOS 26.1 simulator executes. Existing packaged native
libraries are reused. Three generator regressions pass. Private receipt/source/log
hashes are in `artifacts/milestone7-lifecycle/evidence-7d3.json`.

Review checked actor isolation, absence of suspension during admission/native
entry, error precedence, token scope and consumption, bounded hold storage,
cleanup access and irreversible close. No unresolved defect was found. Actual
UIKit event delivery, application suspension/resumption and physical-device
execution remain open. No hosted work, signing, app installation, simulator
startup/shutdown or push ran.
