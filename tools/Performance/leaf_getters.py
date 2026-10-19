#!/usr/bin/env python3
"""Compare fresh DiffPlex emissions across proven simple field getter paths.

Reject any difference beyond the reviewed getter wrappers, fallback names and native profile.
Reuse the existing bounded validation and alternating release measurement harness.
"""
import argparse
import re
from pathlib import Path
import subprocess
import allocation_verification as bench


def prepare(args):
    baseline = subprocess.check_output(["git", "rev-parse", args.baseline_revision], cwd=bench.ROOT, text=True).strip()
    original = Path(args.source).read_text()
    candidate = Path(args.candidate_source).read_text()
    pattern = r"^\[\[maybe_unused\]\] static (\S+) (cilica_m_\w+)\([^\n]+\n    // Proven leaf getter:[\s\S]*?^}\n"
    wrappers = list(re.finditer(pattern, candidate, re.M))
    if not wrappers:
        raise ValueError("Candidate contains no leaf getter paths")
    normalized = candidate
    for match in reversed(wrappers):
        normalized = normalized[:match.start()] + normalized[match.end():]
    for match in wrappers:
        normalized = bench.replace_exact(normalized,
            "[[maybe_unused, gnu::noinline]] static " + match[1] + " " + match[2] + "_framed(",
            "[[maybe_unused]] static " + match[1] + " " + match[2] + "(", 1)
    normalized = bench.replace_exact(normalized, "// Cilica object-frames-native-v92\n",
                                     "// Cilica object-frames-native-v91\n", 1)
    if normalized != original:
        raise ValueError("Fresh C++ differs beyond getter wrappers, fallback names and native profile")
    changed = ["ObjectCppLeafMethods.cs", "ObjectCppEmitter.cs"]
    bench.compile_pair(args, original, candidate, {
        "method": "Independently emitted baseline/candidate; all existing method bodies and runtime preserved; only leaf wrappers and profile added",
        "baseline_revision": baseline,
        "baseline_emission_sha256": bench.digest(Path(args.source)),
        "candidate_emission_sha256": bench.digest(Path(args.candidate_source)),
        "baseline_profile": original.splitlines()[0], "candidate_profile": candidate.splitlines()[0],
        "measurement_iterations_per_sample": 64,
        "eligible_getters": [match[2] for match in wrappers],
        "changed_source_hashes": {name: bench.digest(bench.ROOT / "src/Cilica.Compiler" / name)
                                  for name in changed},
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
