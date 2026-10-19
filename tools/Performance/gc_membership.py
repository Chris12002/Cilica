#!/usr/bin/env python3
"""Compare fresh DiffPlex emissions across scoped collector membership indexing.

Reject any difference beyond the reviewed runtime fragments and native profile.
Reuse the existing bounded validation and alternating release measurement harness.
"""
import argparse
from pathlib import Path
import subprocess
import allocation_verification as bench


def prepare(args):
    baseline = subprocess.check_output(["git", "rev-parse", args.baseline_revision], cwd=bench.ROOT, text=True).strip()
    original = Path(args.source).read_text()
    candidate = Path(args.candidate_source).read_text()
    expected = original
    changed = ["ObjectCppRuntime.cs", "ObjectCppMarking.cs"]
    for name in changed:
        path = "src/Cilica.Compiler/" + name
        before = subprocess.check_output(["git", "show", baseline + ":" + path], cwd=bench.ROOT, text=True)
        after = (bench.ROOT / path).read_text()
        marker = 'internal const string Text = """\n'
        old = before.split(marker, 1)[1].split('\n""";', 1)[0]
        new = after.split(marker, 1)[1].split('\n""";', 1)[0]
        expected = bench.replace_exact(expected, old, new, 1)
    expected = bench.replace_exact(expected, "// Cilica object-frames-native-v90\n",
                                   "// Cilica object-frames-native-v91\n", 1)
    if candidate != expected:
        raise ValueError("Fresh C++ differs beyond reviewed collector runtime and profile changes")
    bench.compile_pair(args, original, candidate, {
        "method": "Independently emitted baseline/candidate; exact runtime-only and profile difference verified",
        "baseline_revision": baseline,
        "baseline_emission_sha256": bench.digest(Path(args.source)),
        "candidate_emission_sha256": bench.digest(Path(args.candidate_source)),
        "baseline_profile": original.splitlines()[0], "candidate_profile": candidate.splitlines()[0],
        "measurement_iterations_per_sample": 64,
        "changed_source_hashes": {name: bench.digest(bench.ROOT / "src/Cilica.Compiler" / name)
                                  for name in changed + ["ObjectCppEmitter.cs"]},
    }, verification_flags=["-DCILICA_VERIFY_INTERNAL_REFERENCES=1", "-DCILICA_VERIFY_ALLOCATION_ROOTS=1"])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("phase", choices=["prepare", "validate", "measure"])
    parser.add_argument("--output", required=True)
    parser.add_argument("--source")
    parser.add_argument("--candidate-source")
    parser.add_argument("--baseline-revision")
    parser.add_argument("--expected")
    args = parser.parse_args()
    if args.phase == "prepare":
        if not all([args.source, args.candidate_source, args.baseline_revision, args.expected]):
            parser.error("prepare requires both sources, baseline revision and expected transcript")
        prepare(args)
    elif args.phase == "validate":
        bench.validate(Path(args.output).resolve())
    else:
        bench.measure(Path(args.output).resolve(), iterations=64)
