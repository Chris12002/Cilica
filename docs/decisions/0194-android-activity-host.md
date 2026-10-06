# ADR 0194: Android activity host

Accepted 2026-10-06; milestone 7e.4. Local simulator checkpoint only.

## Decision

Add an opt-in generated activity host around the Kotlin owner. Create it on the
main thread in `onCreate` and forward same-instance resume/pause/stop/destroy
callbacks synchronously. It immediately gates UI admission on inactivity, owns
one runtime pause hold and reconciles the latest desired state through at most
one pending transition. A stale resume completion cannot reopen a closing or
inactive host. Independent pause holds still apply at the runtime layer.

Copy/release operations remain accessible through the runtime while inactive.
Expose transition failure and a bounded synchronization barrier. Main-thread
callers must compose futures rather than block awaiting transitions. Destruction
is terminal and closes the runtime, including configuration recreation. A close
requested during a failed initial pause must still run native destruction.
Never share one runtime across these owning activity hosts. State restoration and
longer-lived application ownership need the final application's separate policy.

Test through locally built APKs, reusing verified JNI/native libraries. Use unique
test application IDs and temporary local signing certificates; delete private keys
after signing. Reject changed source/APK hashes before launch. The disposable
emulator harness checks that an ID is absent before installation, performs ten
home/resume cycles per collection mode, requests explicit runtime close via a
test-only broadcast, verifies exact ordered markers, then uninstalls and checks
absence. Each wait is bounded; failed runs retain traces/logcat and clean up.

## Evidence and review

On the pinned arm64 Android 16/API-36 emulator with 4 KB pages, both public session
and unchanged kernel apps pass ten real background/resume cycles in normal and
forced collection: **40 total cycles**. The public scenario retains state, exact
UTF-16 snapshots and captured fault details across mutations. The kernel matches
**1,792 exact .NET transcript comparisons plus 260 resumed-field checks**, retaining
its first snapshot across later transitions. All four mode runs explicitly close
their runtime and uninstall successfully. The harness removes its owned adb,
emulator and temporary device; a host process check finds no Android test process.

Synthetic checks cover rapid coalesced transitions and close during pause-limit
failure. The first activity run exposed stale test work after Android's observed
startup relaunch: the old host correctly closed, but obsolete checks wrote into
the shared trace. The scaffold now scopes work/trace writes to the live Activity
generation and stops obsolete work. A recreation during measured cycles cannot
silently pass because cycle markers restart. No compiler/runtime, JNI or game-rule
change was needed. Fourteen focused Python tests pass. Review covered main-thread
confinement, transition/close races, worker ownership, test identity, APK provenance,
stale callback checks, bounded waits and verified removal. No unresolved defect
remains in this same-instance lifecycle scope.

Private evidence: `artifacts/milestone7-android-activity/evidence-7e4.json`.
The test certificates are local/disposable; no publication, hosted work, tool
download or production signing identity was used. Configuration/process restoration,
OS suspension, multiple windows, API-26/16-KB execution and physical devices remain
unverified. This is a scripted kernel scenario rather than the final application
command adapter.
