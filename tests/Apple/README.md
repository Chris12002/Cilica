# Local Apple consumers

`Session.swift.in` is the bounded command-line regression used by the opt-in
`AppleAdapterTests`. `SceneApp.swift.in` supplies a reusable UIKit application
shell; `SessionSceneScenario.swift.in` supplies checks for the public
`EmbeddingSessionProbe` package. Neither runs in hosted CI.

`Ownership.swift.in` is a focused quota/cancellation/recovery consumer for the
same public-session package. It runs 80 close/recreate cycles in normal/forced
collection, checks capture failure before mutation, released handle-slot reuse,
stale lease rejection and cancellation before clone/pause/close. It then fills
all 64 native runtime slots twice and proves failed-open recovery without losing
the remaining instances. Its cancellation gate deliberately allows continuation
after cancellation, matching the adapter's documented cooperative policy.

```bash
python3 -B examples/Apple/check_package.py \
  --package out/session/package --output out/ownership-run \
  --adapter --consumer tests/Apple/Ownership.swift.in \
  --simulator YOUR-BOOTED-SIMULATOR-UUID
```

The same source links for device/simulator, but this command executes only on the
explicit already-booted simulator. No device execution, app installation or
native-library rebuild is implied.

For UIKit, first retain a public session package by running `AppleAdapterTests`
with `CILICA_APPLE_OUTPUT` set to an absolute new directory (see
[adapter instructions](../../adapters/Apple/README.md)). Then link the app against
that existing package for device and simulator; no native rebuild is needed:

```bash
python3 -B examples/Apple/check_package.py \
  --package out/session/package --output out/scene-build \
  --adapter --scene-host --consumer tests/Apple/SceneApp.swift.in \
  --support-source tests/Apple/SessionSceneScenario.swift.in

python3 -B tests/Apple/check_scene.py \
  --build out/scene-build --output out/scene-run \
  --simulator YOUR-BOOTED-SIMULATOR-UUID --cycles 3
```

Both output folders must be new. The supplied scenario expects the session
fixture's specific exports/records; arbitrary v3 packages need their own scenario.
Do not pass `--simulator` to the first command: a UIKit app is installed/launched
by the second command rather than spawned as a CLI process.

The harness requires an explicitly selected already-booted simulator. It installs
two apps with unique bundle IDs, runs the probe, launches its own cover app to
background it and relaunches the existing probe process to foreground it. It tests
normal and collect-before-allocation modes, records actual UIKit events and checks
that retained sessions/text/fault details survive. The default is three cycles per
mode; `--cycles` accepts 1..10. Cycle-numbered ready/paused/resumed markers prevent
an earlier transition from satisfying a later check. It also runs synthetic transition storms,
independent pause holds, transition failure cleanup and forwarded disconnect races.
Subprocesses and log waits have timeouts. Finally it terminates/uninstalls only its
own apps and records cleanup results, command output and build/binary hashes in
`scene-receipt.json`. Inspect that receipt after failure; a timeout is not a pass.
The runner checks source/executable hashes before simulator work; rebuild old
receipts with the current checker. These hashes associate evidence with local
artifacts; they do not authenticate an untrusted package or replace code review.
The test changes the simulator's foreground app. It never boots/stops a simulator,
touches unrelated app processes, signs a device app or requests hosted work.

Device linking is not device execution. Background callbacks do not prove the OS
actually suspended the process, preserved it indefinitely or met a background
deadline. Multiple simultaneous scenes and earlier iOS versions remain untested.

To test another contract, supply a Swift template defining a MainActor `Scenario`
that conforms to the shell's `SceneScenario` protocol. `start` prepares retained
state, `background` inspects it while paused, `resume` compares and advances it,
and `finish` releases remaining leases. These hooks run in the host process;
oracle data and game-specific checks stay outside compiler/runtime sources.
Pass it with `--support-source` and choose its required cycle count explicitly.
The unchanged real-kernel package has separate private scenario evidence across
ten transitions per mode; private kernel source/oracle data are not published here.

Fast evidence/marker regressions require no Apple tools:

```bash
python3 -B -m unittest discover -s tests/Apple -p 'test_*.py' -v
```
