# ADR 0193: Kotlin runtime ownership

Accepted 2026-10-06; milestone 7e.3.

## Decision

Generate a Kotlin owner and a small C JNI bridge from each verified Android v3
package. Keep compiler/runtime code and the native C ABI unchanged. Link the bridge
as a separate shared library with a single module-specific JNI entrypoint and a
dependency on the existing native module. JVM packets contain fixed-width copies;
opaque tokens are never pointers. Copy UTF-16 code units directly. Keep native
scratch allocations bounded and free them on every exit. On a JVM copy failure,
release any newly acquired native runtime/handle/exception before returning.

Match the Swift ownership model: one serial worker per owner, scoped handles,
manifest-driven record flattening, copied fault identity and optional retained
exceptions. Kotlin `Long` preserves all 64 bits. Require explicit asynchronous
close, including failed startup; no finalizer guarantees. Cancellation changes
future observation only and never rolls back or dequeues native work. Discarded
leases stay bounded by the runtime quota until close.

Bound normal pending work to 256, reserve one close slot, and stop admission
synchronously when close is requested. Execute earlier work before destruction.
Support up to 64 independent pause holds. Pause gates invocation/import/clone but
permits copying, fault inspection, release and close. Serialize all native entry
and copying so query/copy never reexecutes exports or races mutation. Completion
callbacks must not block or interrupt the worker; compose dependent work instead.

The JNI profile bounds flattened input to 64 values and UTF-16 buffers to 524,288
units. Optional native functions are selected at generation. Add an explicit local
build pin for installed JBR/Kotlin/build-tools and a CheckJNI `app_process` mode to
the disposable emulator harness. Verify DEX/JNI/source hashes and package identity
before launch; reject shell syntax in main-class names. Keep logcat on failure.

## Evidence and review

On the pinned arm64 Android 16/API-36 emulator with 4 KB pages, public tests pass
six lifecycle cycles, 96 concurrent calls, exact UTF-16/Integer64, numeric record
round trips, captured fault details after subsequent calls, invalid lease/hold
rejection, pause limits, 256 queued calls, cancellation observation and close at
capacity. The unchanged kernel passes 5,376 exact .NET comparisons and 780
resumed-field checks in normal/forced collection. CheckJNI is enabled. The
harness cleans up its emulator, adb server and temporary device data.

The first run failed in record echo. A second diagnostic run exposed native
INVALID_ARGUMENT: the new encoder had prepended a record header. The ABI accepts
only flattened numeric fields. The encoder and argument bound were corrected;
the same reduced record round trip and full adapter/kernel run now pass. No game
rules or native implementation changed. Twelve focused Python tests pass; four
optional string/capture profiles compile with warnings as errors. Review covered
JNI pending exceptions, zero-length arrays, scratch ownership, integer widths,
record layout, cancellation, close admission and worker serialization. No
unresolved defect remains in this slice.

Private evidence: `artifacts/milestone7-android-jni/evidence-7e3.json`.
No hosted work, downloads, publication or agents ran. This is ART/JNI execution,
not APK/activity lifecycle, API-26/16-KB or physical-device validation.
