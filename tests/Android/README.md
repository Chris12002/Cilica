# Android adapter tests

`Session.kt` exercises the public `EmbeddingSessionProbe` package through the
generated Kotlin owner and JNI bridge. `Kernel.kt.in` accepts the existing private
.NET oracle as `@ROWS@` (`intArrayOf` rows) and `@TEXTS@` (escaped Kotlin strings).
The unchanged scripted kernel remains outside compiler/runtime packages. Preserve
oracle hashes and source provenance when rendering that private consumer.

Use [the adapter build and execution commands](../../adapters/Android/README.md).
Consumers run on Android's runtime with CheckJNI in a test-owned emulator, using
the same packaged native libraries. Every future wait and guest execution is
bounded. No network tool installation or hosted test job is needed.

The public capacity test deliberately holds a worker completion with a latch to
prove bounded admission and the reserved close slot. This is controlled test
synchronization; application completion callbacks must never block the worker.

Local generator/evidence checks:

```bash
python3 -B -m unittest discover -s adapters/Android -p 'test_*.py' -v
python3 -B -m unittest discover -s tools/Android -p 'test_*.py' -v
```

## Real activity callbacks

`build_activity.py` reuses a verified JVM consumer's JNI library and the existing
native package, requiring generated bridge/owner sources to match. It adds the
opt-in activity host, shared `Activity.kt.in` shell and an explicit scenario.
`SessionActivity.kt` exercises public state/text/fault retention;
`KernelActivity.kt.in` uses the same private oracle placeholders as the CLI case.

```bash
python3 -B tests/Android/build_activity.py \
  --package out/android-session --consumer out/jvm-session \
  --sdk /path/to/sdk --java-home /path/to/jdk --kotlin-home /path/to/kotlinc \
  --scenario tests/Android/SessionActivity.kt --output out/activity-session
python3 -B tools/Android/run_emulator.py --sdk /path/to/sdk --output out/activity-run \
  --activity-case out/android-session out/activity-session
```

Each APK has a fresh `dev.cilica.test.p<UUID>` application ID, a temporary local
test certificate and debuggable private trace storage. Signing keys are deleted
after use. No production signing identity, Gradle download or publication is used.
The shell's close broadcast is a test-only protocol, not part of the adapter.

The runner requires an absent test ID before install, executes ten home/resume
cycles and explicit runtime close in each collection mode, verifies exact ordered
markers, then uninstalls and checks absence. Failures retain trace/logcat and still
attempt removal. The owning emulator is destroyed afterward by the outer harness.
Hash checks reject changed APKs or sources before any emulator work.

Synthetic startup checks also cover rapid desired-state changes and closing while
the initial pause fails at its limit. A destroyed Activity's queued checks stop
writing; each new instance resets its own trace. A recreation during the measured
cycles cannot silently pass because its cycle sequence restarts. This tests
same-instance pause/resume, not restoration across configuration/process loss.
