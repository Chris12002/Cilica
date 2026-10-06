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
