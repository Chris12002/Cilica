#!/usr/bin/env python3
"""Compare independently emitted DiffPlex C++ across internal-reference verification.

The expected difference is restricted to the two runtime fragments, diagnostic
macros and four emitter ownership-guard sites. Reuses bounded local validation
and timing; no downloads or services.
"""
import argparse
from pathlib import Path
import subprocess
import allocation_verification as bench


def expected_candidate(original, baseline):
    expected = original
    replacements = []
    for filename, names in [("ObjectCppRuntime.cs", ["DiagnosticIncludes", "Text"]),
                            ("ObjectCppLocations.cs", ["Text"])]:
        path = "src/Cilica.Compiler/" + filename
        old = subprocess.check_output(["git", "show", baseline + ":" + path], cwd=bench.ROOT, text=True)
        new = (bench.ROOT / path).read_text()
        for name in names:
            marker = 'internal const string ' + name + ' = """\n'
            before = old.split(marker, 1)[1].split('\n""";', 1)[0]
            after = new.split(marker, 1)[1].split('\n""";', 1)[0]
            placeholder = f"__REVIEWED_RUNTIME_FRAGMENT_{len(replacements)}__"
            expected = bench.replace_exact(expected, before, placeholder, 1)
            replacements.append((placeholder, after))
    # Runtime fragments are masked: only generated instruction guards remain.
    guard = "!context.instance.owns("
    count = expected.count(guard)
    if count == 0:
        raise ValueError("Baseline has no generated ownership guards")
    expected = expected.replace(guard, "!internal_reference_owned(context.instance, ")
    for placeholder, after in replacements:
        expected = bench.replace_exact(expected, placeholder, after, 1)
    return expected, count


def prepare(args):
    baseline = subprocess.check_output(["git", "rev-parse", args.baseline_revision], cwd=bench.ROOT, text=True).strip()
    original = Path(args.source).read_text()
    candidate = Path(args.candidate_source).read_text()
    expected, guards = expected_candidate(original, baseline)
    if expected != candidate:
        raise ValueError("Generated C++ differs beyond reviewed internal-reference changes")
    metadata = {
        "method": "Independently emitted baseline and candidate; exact runtime and generated guard differences verified",
        "baseline_revision": baseline,
        "baseline_emission_sha256": bench.digest(Path(args.source)),
        "candidate_emission_sha256": bench.digest(Path(args.candidate_source)),
        "baseline_profile": original.splitlines()[0], "candidate_profile": candidate.splitlines()[0],
        "generated_guards": guards, "measurement_iterations_per_sample": 64,
        "changed_source_hashes": {name: bench.digest(bench.ROOT / "src/Cilica.Compiler" / name)
                                  for name in ["ObjectCppRuntime.cs", "ObjectCppEmitter.cs", "ObjectCppLocations.cs"]},
    }
    bench.compile_pair(args, original, candidate, metadata,
                       verification_flags=["-DCILICA_VERIFY_INTERNAL_REFERENCES=1", "-DCILICA_VERIFY_ALLOCATION_ROOTS=1"])


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
