# ADR 0192: Android native emulator validation

Accepted 2026-10-06; milestone 7e.2.

## Context

Slice 7e.1 cross-linked Android libraries but did not run them. Native loader and
runtime behavior need direct evidence before introducing Java/Kotlin ownership.
An arm64 Android 16 image and emulator are already installed locally. Existing
C11 lifecycle consumers and the unchanged kernel's .NET oracle can be reused.

## Decision

Keep building and execution separate. `build_consumer.py` links a supplied C11
template against a verified package with the same explicit NDK/Clang pin, C11/O2,
2 KiB frame gate and 16 KB linker alignment. A receipt associates source/binary
hashes with the exact package receipt. It executes no generated native code.

`run_emulator.py` accepts explicit packages and consumers. Verify required package
files and executable hashes before launch; reject receipt path expansion and
symlinks escaping the package. Pin emulator 36.3.10, adb 36.0.2-14143358 and the
API-36 Google Play arm64 image revision 7. Create a new AVD and private user/key
directory beneath a new output folder. Do not reuse personal AVD data, snapshots
or a shared adb daemon. Use an available console port pair and a private adb
server port; target only the owned emulator serial. Check its API, ABI and emulator
identity before pushing code.

Boot is bounded to approximately 180 seconds; each adb operation has a timeout,
and each native consumer has a guest-side 60-second limit. Consumers load their
matching library via a case-local `LD_LIBRARY_PATH`. Require exit zero plus a
PASS marker; assertion failures, missing markers and cleanup failures cannot pass.
Save device fingerprint/page size, package/binary hashes, outputs and cleanup in
a receipt. Terminate/wait only directly owned children, then remove the temporary
AVD/user data. A failed boot follows the same cleanup path. This is a disposable
test harness, not a long-running development service.

## Evidence and review

One emulator boot executed all three consumers. Public-session and real-kernel
packages each pass six C11 lifecycle cycles. The existing real-kernel consumer
passes **5,376 exact .NET transcript comparisons**, three repetitions in each of
normal and forced-collection modes. Numeric observations, UTF-16 text, retained
snapshots/aliases, independent instances and stale-token rejection are exercised.
Only its header include changed for the package layout; oracle data, generated
native libraries, compiler/runtime semantics and game rules are unchanged.

Measured fingerprint:
`google/sdk_gphone64_arm64/emu64a:16/BE2A.250530.026.D1/13818094:user/release-keys`.
The guest reports API 36, arm64-v8a and **4096-byte pages**. Both owned processes
exited and temporary device data was deleted; a separate host process check found
no Android test subprocess remaining. Logs/receipts remain inspectable in private
`artifacts/milestone7-android-run/evidence-7e2.json`.

Eight targeted local tests pass, including package/consumer tamper rejection,
package association, path containment, bounded child shutdown and failed-boot
cleanup. Review covered provenance, dynamic-library ownership, explicit serials,
timeouts, failure recording, process ownership and scope of compatibility claims.
No unresolved defect remains in this slice. No hosted jobs, downloads, app signing,
publication or additional agents ran.

This does not establish API-26 or 16-KB runtime compatibility, Android captured
fault copying, JNI/Kotlin ownership, APK packaging, app lifecycle or physical-device
support. The next adapter needs its own explicit lifetime/concurrency design.

The runner uses the documented [emulator command-line options](https://developer.android.com/studio/run/emulator-commandline)
for headless operation, explicit console ports and disabled snapshots.
