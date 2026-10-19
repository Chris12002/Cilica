#!/usr/bin/env python3
"""Validate and measure independently emitted DiffPlex compiler slices locally.

Emission is a separate explicit test. This runner never rewrites generated methods.
Native builds, correctness validation and timing run sequentially.
"""
import argparse
import re
from pathlib import Path
import subprocess
import allocation_verification as bench

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True)
    parser.add_argument('--candidate-source', required=True)
    parser.add_argument('--expected', required=True)
    parser.add_argument('--baseline-revision', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--verification-flag', action='append')
    args = parser.parse_args()
    candidate = Path(args.candidate_source).read_text()
    profile = re.search(r'public const string Profile = "([^"]+)"', (bench.ROOT / 'src/Cilica.Compiler/ObjectCppEmitter.cs').read_text()).group(1)
    if candidate.splitlines()[0] != '// Cilica ' + profile:
        raise ValueError('Candidate emission does not match the current native profile')
    sources = sorted((bench.ROOT / 'src/Cilica.Compiler').glob('*.cs'))
    sources += sorted((bench.ROOT / 'library/Cilica.Core').glob('*'))
    sources += sorted((bench.ROOT / 'src/Cilica.Metadata/Resolution').glob('*.cs'))
    metadata = {
        'method': 'Independently emitted compiler output; no generated-method rewriting',
        'baseline_revision': subprocess.check_output(['git', 'rev-parse', args.baseline_revision], cwd=bench.ROOT, text=True).strip(),
        'baseline_emission_sha256': bench.digest(Path(args.source)),
        'candidate_emission_sha256': bench.digest(Path(args.candidate_source)),
        'candidate_sources': {str(p.relative_to(bench.ROOT)): bench.digest(p) for p in sources if p.is_file()},
        'working_diff': subprocess.check_output(['git', 'diff', '--', 'src', 'library'], cwd=bench.ROOT, text=True),
    }
    bench.compile_pair(args, Path(args.source).read_text(), Path(args.candidate_source).read_text(), metadata,
        verification_flags=args.verification_flag)
    output = Path(args.output).resolve()
    for name, expected in metadata['candidate_sources'].items():
        if bench.digest(bench.ROOT / name) != expected:
            raise ValueError('Source changed during preparation: ' + name)
    bench.validate(output)
    bench.measure(output, iterations=256)
