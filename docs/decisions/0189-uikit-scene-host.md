# ADR 0189: UIKit scene host

Accepted 2026-10-06; milestone 7d.4. Simulator application evidence; device linking only.

## Context

The worker pause is an awaited barrier, so launching a Task from a UIKit callback
does not immediately stop UI submissions. Separate callback tasks can also reach
the worker in a different order. Scene lifecycle belongs to the UI host, while
native serialization and retained ownership belong to the existing runtime actor.

## Decision

Generate an optional `<Module>SceneHost` with `--scene-host`; the default source
does not import UIKit. Each MainActor host creates and owns one runtime, holds a
weak reference to its scene and starts with a scoped pause. The app forwards
active, resign-active, background and disconnect delegate callbacks synchronously.
Callbacks for another scene are ignored. No global notification observers or
automatic scene-delegate replacement are introduced.

Resign/background immediately closes UI admission. Invoke/import/clone check the
UI gate before awaiting the runtime actor. One reconciliation task manages the
host's pause hold and rechecks desired state after each worker await. New events
update desired state rather than enqueue another task or hold. A stale resume
completion cannot reopen the UI gate; brief intermediate states can coalesce.
Already-admitted calls can complete or encounter the runtime pause. Native calls
are not interrupted and rejected commands are not replayed.

The caller stores an asynchronously created host, immediately refreshes scene
state and then awaits synchronization as needed. This covers callbacks missed
during startup; an unattached scene requests close. Disconnect blocks admission
immediately and requests destruction. Later activation cannot reopen it. A
transition error is stored and rethrown by synchronization, leaving admission
closed; explicit close can clean up afterward. Independent runtime pause holds
remain effective even when the host's own gate is active.

Expose the owned runtime for copying, fault inspection, lease release and separate
pause holds. UI invoke/import/clone must use the host; bypassing it loses immediate
UI admission semantics. Runtime destruction goes through host close. This is an
integration contract, not a security boundary. No C ABI, native code, background
execution allowance, persistent checkpoint or application game command changes.

## Evidence and review

The public session fixture has a UIKit app consumer and a bounded opt-in harness.
The harness installs two uniquely identified apps on an explicit already-booted
simulator. A cover app backgrounds the probe; bringing the same probe process
forward resumes it. Real UIKit active/inactive/background callbacks are recorded.
Session fields, exact wide values and UTF-16 containing a zero and unpaired
surrogate survive; mutation resumes with the expected result. Normal and forced
collection modes pass on arm64 iOS 26.1. Both device and simulator consumers link
under Swift 6 warnings-as-errors using existing native packages.

Synthetic checks within the app cover 100 rapid inactive/active pairs per mode,
an immediate inactive gate, independent pause ownership, exhausted pause capacity,
visible fail-closed transition handling, explicit cleanup and disconnect racing
activation. The final disconnect is forwarded synthetically; it is not evidence
of UIKit discarding the scene. Optional no-string/no-record variants with capture
off/on also type-check. The three existing generator regressions pass.

Review covered MainActor/worker isolation, post-await reconciliation, missed
startup callbacks, weak scene ownership, cleanup after failure and bounded task/
hold storage. Startup refresh and unattached-scene cleanup were made explicit;
the harness continues attempting cleanup of both owned apps after a cleanup
timeout and records failures. Final simulator runs confirm both apps removed.
No unresolved defect remains in the admitted scope. Private receipts/logs/hashes
are in `artifacts/milestone7-uikit/evidence-7d4.json`.

No simulator was booted/stopped, no device signing/execution, hosted work or push
ran. Actual OS suspension, eviction/restoration, deadlines, simultaneous windows
and complete application/Expo commands remain separate gates.

The forwarding model follows Apple's [scene lifecycle](https://developer.apple.com/documentation/uikit/transitioning-to-the-uikit-scene-based-life-cycle)
and [scene delegate callbacks](https://developer.apple.com/documentation/uikit/uiscenedelegate).
