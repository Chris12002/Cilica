#!/usr/bin/env python3
"""Replay the allocation-verification change on the retained macOS DiffPlex build.

This is a runtime-only experiment, not fresh DiffPlex compilation from this branch.
No downloads, services or hosted work. Each child process has a finite timeout.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import statistics
import subprocess

PIN = "4c5e4f743f67590ba849e3d0b91414b7229a09ee03f5bfab583c08550b5ae1da"
ROOT = Path(__file__).resolve().parents[2]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(command, output, timeout=300, environment=None):
    result = subprocess.run(command, capture_output=True, text=True,
                            timeout=timeout, env=environment)
    output.write_text(result.stdout)
    output.with_suffix(output.suffix + ".stderr").write_text(result.stderr)
    if result.returncode:
        raise RuntimeError(f"Failed ({result.returncode}): {command}; see {output}")
    return result.stdout


def replace_exact(text, old, new, count):
    if text.count(old) != count:
        raise ValueError(f"Expected {count} occurrences of {old[:70]!r}")
    return text.replace(old, new)


def prepare(args):
    source = Path(args.source).resolve()
    if digest(source) != PIN:
        raise ValueError("Source is not the reviewed retained macOS DiffPlex build")
    out = Path(args.output).resolve()
    out.mkdir(parents=True, exist_ok=False)
    runtime = (ROOT / "src/Cilica.Compiler/ObjectCppRuntime.cs").read_text()
    # Extract the actual production macro and helper, not a separately authored
    # approximation. All other generated runtime and method bodies stay pinned.
    macro = runtime.split('internal const string DiagnosticIncludes = """\n', 1)[1].split("#ifndef CILICA_ARENA_ASAN", 1)[0]
    helper = "[[maybe_unused]] static bool allocation_safepoint" + runtime.split("[[maybe_unused]] static bool allocation_safepoint", 1)[1].split("\n[[maybe_unused]] static bool collect_for_allocation", 1)[0] + "\n"
    original = source.read_text()
    candidate = replace_exact(original, "[[maybe_unused]] static bool collect_for_allocation(Context&, bool&) noexcept;",
                              helper + "[[maybe_unused]] static bool collect_for_allocation(Context&, bool&) noexcept;", 1)
    candidate = replace_exact(candidate,
        "    if (!valid_roots(context)) { context.pending = 6u; return nullptr; }\n    if (context.observe != nullptr) context.observe(context);",
        "    if (!allocation_safepoint(context)) return nullptr;", 3)
    candidate = replace_exact(candidate,
        "    if (!valid_roots(context)) { context.pending = 6; return false; }\n    if (context.observe != nullptr) context.observe(context);",
        "    if (!allocation_safepoint(context)) return false;", 2)
    candidate = macro + candidate
    # An opt-in stress switch is outside the timed region and identical in both.
    anchor = "Instance heap;Context c(heap);int work="
    replacement = 'Instance heap;if(std::getenv("CILICA_BENCH_GC_STRESS"))heap.collect_before_allocation=true;Context c(heap);int work='
    paths = {}
    for name, content in [("baseline", original), ("candidate", candidate)]:
        path = out / (name + ".cpp")
        path.write_text(replace_exact(content, anchor, replacement, 1))
        paths[name] = path
    expected = json.loads(Path(args.expected).read_text())
    if len(expected["values"]) != 523 or expected["checksum"] != 6146:
        raise ValueError("Expected the 128-character DiffPlex transcript")
    (out / "expected.json").write_text(json.dumps(expected) + "\n")
    compiler = os.environ.get("CILICA_CXX", "clang++")
    commands = {}
    flags = ["-std=c++20", "-O2", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror"]
    for name in ["baseline", "candidate", "verification", "sanitized"]:
        extra = ["-DCILICA_VERIFY_ALLOCATION_ROOTS=1"] if name == "verification" else []
        extra += ["-fsanitize=address,undefined", "-fno-sanitize-recover=all"] if name == "sanitized" else ["-DNDEBUG"]
        commands[name] = [compiler, *flags, *extra, str(paths["baseline" if name == "baseline" else "candidate"]), "-o", str(out / name)]
        run(commands[name], out / (name + "-build.log"))
    metadata = {
        "method": "Exact production allocation change replayed on retained generated C++; not full current-compiler DiffPlex emission",
        "benchmark_compiler_revision": "49dd0b1", "source_sha256": PIN,
        "checkout_revision": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
        "runtime_sha256": digest(ROOT / "src/Cilica.Compiler/ObjectCppRuntime.cs"),
        "platform": platform.platform(), "machine": platform.machine(),
        "compiler": subprocess.check_output([compiler, "--version"], text=True),
        "commands": commands,
        "hashes": {name: digest(out / name) for name in ["baseline.cpp", "candidate.cpp", "baseline", "candidate", "verification", "sanitized", "expected.json"]},
    }
    (out / "provenance.json").write_text(json.dumps(metadata, indent=2) + "\n")
    print("Prepared four binaries; validation and timing are separate phases.", flush=True)


def validate(out):
    verify_artifacts(out)
    expected = json.loads((out / "expected.json").read_text())
    for name in ["baseline", "candidate", "verification", "sanitized"]:
        observed = json.loads(run([str(out / name), "verify", "0"], out / (name + "-verify.json"), timeout=600))
        if observed != expected:
            raise ValueError(f"Transcript mismatch: {name}")
        print(f"{name}: 523 observations and checksum match", flush=True)
    env = dict(os.environ, CILICA_BENCH_GC_STRESS="1")
    for name in ["sanitized", "verification"]:
        observed = json.loads(run([str(out / name), "cold", "0"], out / (name + "-stress.json"), environment=env))
        if observed["checksum"] != 6146:
            raise ValueError(f"Stress checksum mismatch: {name}")
    (out / "validation.json").write_text(json.dumps({"transcripts": 4, "observations_each": 523, "checksum": 6146, "stress_runs": 2}) + "\n")


def measure(out):
    verify_artifacts(out)
    if not (out / "validation.json").exists():
        raise ValueError("Validate before measuring")
    if (out / "summary.json").exists():
        raise ValueError("Use a fresh prepared directory for another measurement")
    if os.environ.get("CILICA_BENCH_GC_STRESS"):
        raise ValueError("Remove the GC stress environment variable before timing")
    results = {"baseline": [], "candidate": []}
    loads = []
    for repeat in range(3):
        for name in (["baseline", "candidate"] if repeat % 2 == 0 else ["candidate", "baseline"]):
            loads.append({"name": name, "repeat": repeat, "before": os.getloadavg()})
            rows = [json.loads(line) for line in run([str(out / name), "measure", "0", "8"], out / f"{name}-{repeat}.jsonl").splitlines()]
            loads[-1]["after"] = os.getloadavg()
            if len(rows) != 8:
                raise ValueError(f"Expected warmup plus seven samples: {name}")
            for row in rows[1:]:
                if row["iterations"] != 8 or row["checksum"] != 6146 * row["iterations"]:
                    raise ValueError(f"Timed checksum mismatch: {name}")
                results[name].append(row["ns"] / row["iterations"])
            print(f"Measured {name}, process {repeat + 1}", flush=True)
    summary = {name: {"median_ns": statistics.median(values), "min_ns": min(values), "max_ns": max(values), "samples": len(values)} for name, values in results.items()}
    summary["speedup"] = summary["baseline"]["median_ns"] / summary["candidate"]["median_ns"]
    (out / "host-load.json").write_text(json.dumps(loads, indent=2) + "\n")
    (out / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
    print(json.dumps(summary, indent=2))


def verify_artifacts(out):
    provenance = json.loads((out / "provenance.json").read_text())
    for name, expected_hash in provenance["hashes"].items():
        if digest(out / name) != expected_hash:
            raise ValueError(f"Prepared artifact changed: {name}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("phase", choices=["prepare", "validate", "measure"])
    parser.add_argument("--output", required=True)
    parser.add_argument("--source")
    parser.add_argument("--expected")
    args = parser.parse_args()
    if args.phase == "prepare":
        if not args.source or not args.expected:
            parser.error("prepare requires --source and --expected")
        prepare(args)
    elif args.phase == "validate":
        validate(Path(args.output).resolve())
    else:
        measure(Path(args.output).resolve())
