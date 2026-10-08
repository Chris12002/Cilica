# Bounded DiffPlex performance regression suite

This suite compares one approved, pinned 128-character workload. It is informational:
checksum/transcript mismatches fail, but elapsed times and speed ratios never gate.
No CI service is added. Execute candidate code only in the documented offline guest.

`prepare.py` exports the exact compiler/library and benchmark commits, verifies
cached package hashes, and retrieves the previously reviewed hash-pinned candidate
texts. Only the 14 compile files in the owned project are compiled; no upstream
build/test/generator scripts run. Preparation does not execute candidate code.

The suite separates:

- `noop`: process startup, minimal host/output and teardown, with equal `0` output.
- `cold`: first root invocation and separately whole-command elapsed time.
- `measure`: warmed managed loop, excluding host output/launch/build; per-sample checksum.
- `profile`: separate counter binary, root/collection scope clocks and heap-phase series.
- `collect-each`: untimed post-operation survivor checks; not a timing-policy recommendation.
- `micro`: independent exact-ownership lookup reducer, including removed/foreign/null addresses.
- `check_index.py`: all 523 output observations with forced GC, ASan/UBSan and leak detection.

Each clean runtime gets three fresh processes and seven samples per process;
native uses 16 operations/sample, .NET 32,768. Raw totals, iterations, warm counts,
RSS, managed counters and process user/system/wall time remain separate. See
[limitations and the measured result](../../../docs/performance/diffplex-20261008.md).
The index transformation is an experiment with uncharged 64 KiB instance storage;
it is not a production runtime patch.

## Reproduce in the already approved environment

Prerequisites are installed Docker Desktop, the retained image
`sha256:68b78e3c1c62c8ed6db8421d6f11cd0619829bd8b77ad1368cae9328e3d40f80`, and the
offline packages named in the original provenance receipt. Do not substitute images,
download new tools/packages, broaden sources, or mount the checkout/home into a guest.
The scripts use the existing local Docker socket and an empty task-specific config.
There must be no existing container with the task's name before starting a new run.

From the repository root, choose a fresh writable output directory:

```sh
python3 tools/Performance/diffplex/prepare.py \
  --output /tmp/cilica-performance-run \
  --package-cache /tmp/cilica-diffplex-retry-20261008 \
  --package-cache /Users/christian/.nuget/packages
docker --config /tmp/cilica-performance-run/docker-config \
  --host unix:///Users/christian/.docker/run/docker.sock build \
  --network none --pull=false \
  --iidfile /tmp/cilica-performance-run/image-id.txt \
  /tmp/cilica-performance-run/inputs
python3 /tmp/cilica-performance-run/create.py
python3 /tmp/cilica-performance-run/supervise.py
```

The supervisor checks the exact resource/isolation settings before starting and
removes only its own disposable container after exporting bounded evidence.
While it runs, diagnostics finish before `/evidence/ready-for-clean.json` appears.
In another terminal, inspect that marker and coordinate a roughly two-minute window
with feature work. Only then release timing:

```sh
docker --config /tmp/cilica-performance-run/docker-config \
  --host unix:///Users/christian/.docker/run/docker.sock exec \
  cilica-performance-fingerprint-20261008 cat /evidence/ready-for-clean.json
docker --config /tmp/cilica-performance-run/docker-config \
  --host unix:///Users/christian/.docker/run/docker.sock exec \
  cilica-performance-fingerprint-20261008 touch /work/run-clean
```

Do not start a second benchmark while one is active. The watchdog bounds the entire
run to 1,790 seconds; absent a clean window, the guest stops waiting after 1,400 s.
After the supervisor exports evidence and removes the container:

```sh
python3 tools/Performance/diffplex/analyze.py /tmp/cilica-performance-run/evidence
```

For the recorded run, the late host build overlaps repetition 2 (zero-based).
Reproduce its conservative accepted summary without deleting any raw samples:

```sh
python3 tools/Performance/diffplex/analyze.py docs/performance/diffplex-20261008 --exclude-repeat 2
python3 tools/Performance/diffplex/analyze_residual.py docs/performance/residual-20261008
```

`residual.py` produces diagnostic `scopes`, Linux ARM64 `sample`, and
`boundary-baseline`/`boundary-index` variants from the exact retained original C++.
`runner_residual.py` is the executed bounded guest recipe: stage only that original
file plus `residual.py`, `transform.py` and the runner as `/approved` in a disposable
image based on the same verified tooling image. Reuse the same isolation supervisor
with a distinct residual container name. Its build/run/symbolization commands and
hashes are retained in `docs/performance/residual-20261008/result.json`.
This is CPU attribution and semantic boundary testing, not another clean timing.
No production changes are made. Full sources can be regenerated from the original
emission and the recorded executed transforms; source hashes were checked exactly.

Keep failed runs, exact generated/binary hashes, commands and load observations.
Remove only that run's disposable input image and input/archive copies after
retaining evidence; retain the shared approved tooling image. Original benchmark
receipts and this report are immutable. Rebaseline explicitly when versions or
inputs change, and retain the old result. A future baseline should lengthen the
fast experimental sample windows and test more hosts before considering thresholds.

The recorded executed scripts in `docs/performance/diffplex-20261008` are exact
run evidence. These reusable wrappers additionally enforce the matching source/DLL
hashes and incorporate the separately executed sanitizer check into the recipe.

Local transformer checks only read Cilica source strings and never run candidates:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tools/Performance/diffplex -p 'test_*.py'
```
