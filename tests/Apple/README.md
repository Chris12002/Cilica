# Local Apple consumers

`Session.swift.in` is the bounded command-line regression used by the opt-in
`AppleAdapterTests`. `SceneApp.swift.in` is a separate UIKit application test for
the same public `EmbeddingSessionProbe` package. Neither runs in hosted CI.

For UIKit, first retain a public session package by running `AppleAdapterTests`
with `CILICA_APPLE_OUTPUT` set to an absolute new directory (see
[adapter instructions](../../adapters/Apple/README.md)). Then link the app against
that existing package for device and simulator; no native rebuild is needed:

```bash
python3 -B examples/Apple/check_package.py \
  --package out/session/package --output out/scene-build \
  --adapter --scene-host --consumer tests/Apple/SceneApp.swift.in

python3 -B tests/Apple/check_scene.py \
  --build out/scene-build --output out/scene-run \
  --simulator YOUR-BOOTED-SIMULATOR-UUID
```

Both output folders must be new. The app template expects the session fixture's
specific exports/records; arbitrary v3 packages are not compatible with this test.
Do not pass `--simulator` to the first command: a UIKit app is installed/launched
by the second command rather than spawned as a CLI process.

The harness requires an explicitly selected already-booted simulator. It installs
two apps with unique bundle IDs, runs the probe, launches its own cover app to
background it and relaunches the existing probe process to foreground it. It tests
normal and collect-before-allocation modes, records actual UIKit events and checks
that retained sessions/text survive. It also runs synthetic transition storms,
independent pause holds, transition failure cleanup and forwarded disconnect races.
Subprocesses and log waits have timeouts. Finally it terminates/uninstalls only its
own apps and records cleanup results, command output and build/binary hashes in
`scene-receipt.json`. Inspect that receipt after failure; a timeout is not a pass.
The test changes the simulator's foreground app. It never boots/stops a simulator,
touches unrelated app processes, signs a device app or requests hosted work.

Device linking is not device execution. Background callbacks do not prove the OS
actually suspended the process, preserved it indefinitely or met a background
deadline. Multiple simultaneous scenes and earlier iOS versions remain untested.
