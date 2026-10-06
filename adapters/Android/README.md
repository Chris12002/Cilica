# Kotlin runtime owner and JNI bridge

Generate a matching owner and bridge from an existing Android package:

```bash
python3 -B adapters/Android/generate.py --package out/android-engine --output out/android-adapter
```

The generated `dev.cilica.<Module>Runtime` and `bridge.c` wrap the unchanged C ABI.
Link the bridge as `lib<Module>Jni.so` against `lib<Module>.so`; package both together.
Keep the generated `dev.cilica.<Module>Native` class and its native method names
when using an obfuscator. This source adapter is not an AAR or a finished app SDK.

Each owner creates one serial worker. Native creation, calls, copies, releases
and destruction all run there. Constructor options select forced collection and
a handle limit from 1 to 1024. Await `ready()` to observe initialization failure;
always call and await `close()`, including after failed initialization. There is
no finalizer cleanup promise. The owner must remain explicitly scoped to its host.

```kotlin
val runtime = CilicaEngineRuntime()
runtime.ready().thenCompose {
    runtime.invoke(1, listOf(CilicaEngineRuntime.Value.Int32(42)))
}.whenComplete { outcome, failure ->
    // Dispatch UI updates to the main thread. Close when the host is finished.
}
```

Completions can run on the worker or inline for an already-completed future.
Never block a completion waiting for another operation on the same owner; compose
futures with `thenCompose`, or dispatch observation to another executor. Never
interrupt its worker. Cancelling or externally completing an observation future
does not cancel, roll back or dequeue admitted native work. Discarded result leases
remain within native quotas until explicitly released or the owner is closed.

Up to 256 pending operations are admitted; excess submissions fail `queueLimit`.
Close synchronously shuts admission and has a reserved queue slot, so it remains
available at capacity. Earlier admitted operations finish before destruction;
later calls fail `closed`. Repeated close shares destruction through independent
future views, so cancelling a view cannot cancel cleanup.

Values carry Int32, all 64 Integer64 bits in JVM `Long`, nullable handles, numeric
records and void. Manifest schemas check logical arguments; record inputs flatten
to numeric fields without a record header. This adapter profile caps flattened
input at 64 values and copied UTF-16 buffers at 524,288 code units. Records and
submitted strings are copied before asynchronous execution. No native pointer or
allocator ownership reaches the JVM. JNI uses region copies and releases its own
bounded native scratch buffers on every path, including JVM exceptions.

Handles are runtime-scoped leases. Copying a Kotlin reference aliases the same
lease; `clone` creates an independent retained lease, `release` consumes one and
close invalidates all. Foreign/released handles fail locally. `importUTF16` and
`copyUTF16` preserve exact `Char` units, including embedded NUL and unpaired
surrogates. Integer64 values never pass through floating-point conversion.

Managed failure produces an `Outcome` with copied code, method, IL offset and
image hash. With capture requested/supported it also owns an exception lease;
`exceptionDetails` copies exact type/message and declaration identity. Capture
can consume a native handle slot. Effects before a managed fault are not rolled
back. Other native statuses produce `Failure("native", status)`.

`pause()` is a worker barrier returning a scoped hold; at most 64 holds overlap.
While any hold exists, invocation/import/clone fail `paused`; copy, fault details,
release and close remain available. `resume` consumes exactly one hold. Foreign,
released or reused holds fail `invalidPause`; dropping/cancelling observation of a
hold does not resume admission. A pause is an in-memory policy, not persistence or
a guarantee of OS background execution. Activity callback integration is separate.

## Building and testing locally

`build.py` generates the adapter, links its JNI library and compiles an explicit
Kotlin test consumer to DEX. Supply installed tools; nothing is downloaded:

```bash
python3 -B adapters/Android/build.py \
  --package out/android-engine --ndk /path/to/sdk/ndk/27.1.12297006 \
  --sdk /path/to/sdk --java-home /path/to/jdk --kotlin-home /path/to/kotlinc \
  --source tests/Consumer.kt --main-class dev.cilica.Consumer --output out/jvm-consumer
python3 -B tools/Android/run_emulator.py --sdk /path/to/sdk --output out/jvm-run \
  --jvm-case out/android-engine out/jvm-consumer
```

The installed tool pin is JBR 21.0.8, Android Studio's bundled Kotlin
`2.2.255-dev-255`, build-tools 36.0.0 and platform 36; native NDK pins are unchanged.
This is a reproducible local checkpoint using that development compiler, not a
claim about arbitrary Kotlin/Gradle releases. Java bytecode targets 1.8 and DEX
min API is 26; execution evidence is API 36 only. Receipts hash generated sources,
DEX/JNI products and the associated native package. The runner uses `app_process`
with CheckJNI in its fresh emulator and captures logcat on failure.

Public session tests pass six normal/forced-collection cycles, exact strings and
wide records, copied captured faults, 96 concurrent calls, overlapping pauses,
256 queued operations, cancellation observation and close at capacity. The real
kernel passes 5,376 .NET comparisons plus 780 resumed-field checks. Four optional
string/capture profiles compile; twelve focused Python tests pass. A record-header
encoding bug discovered during execution is fixed and retained in the regression.
No compiler/runtime or game-rule changes were needed. Android APK/activity and
physical-device gates remain separate.

JNI buffer handling follows Android's [JNI guidance](https://developer.android.com/ndk/guides/jni-tips).
