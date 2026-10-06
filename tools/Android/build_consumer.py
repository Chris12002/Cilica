#!/usr/bin/env python3
"""Link a C11 template against an existing Android package; do not execute it."""
import argparse
import json
from pathlib import Path
import re
import subprocess

from package_support import package_files
from build_library import sha256


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('package', 'source', 'ndk', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    package = args.package.resolve(strict=True)
    receipt, library = package_files(package)
    pin = json.loads(Path(__file__).with_name('toolchain.json').read_bytes())
    if receipt['toolchain'] != pin:
        raise ValueError('Package toolchain differs from the current pin')
    ndk = args.ndk.resolve(strict=True)
    revision = re.search(r'^Pkg.Revision\s*=\s*(\S+)', (ndk/'source.properties').read_text(), re.MULTILINE)
    if not revision or revision[1] != pin['ndkRevision']:
        raise ValueError('NDK differs from pin')
    clang = ndk/'toolchains/llvm/prebuilt'/pin['hostTag']/'bin/clang'
    version = subprocess.check_output([str(clang), '--version'], text=True, timeout=30)
    if version.splitlines()[0] != pin['clangVersion']:
        raise ValueError('Clang differs from pin')
    output = args.output.resolve(); output.mkdir(parents=True, exist_ok=False)
    manifest = json.loads((package/'contract.json').read_bytes())
    name = manifest['contract']['name']
    if not re.fullmatch(r'[a-z][a-z0-9_]{0,47}', name):
        raise ValueError('Invalid contract name')
    prefix = 'cilica_' + name + '_abi_v3'
    source = output/'consumer.c'
    source.write_text(args.source.read_text().replace('@PREFIX@', prefix).replace('@HASH@', prefix.upper()+'_CONTRACT_HASH'))
    command = [str(clang), '--target='+pin['triple']+str(pin['minimumAPI']), '-std=c11', '-O2', '-fPIE', '-pie',
               '-Wall', '-Wextra', '-Werror', '-Wframe-larger-than=2048', '-I', str(package/'include'),
               str(source), str(library), '-Wl,--no-undefined', '-Wl,-z,max-page-size=16384',
               '-Wl,-z,common-page-size=16384', '-o', str(output/'consumer')]
    with (output/'build.log').open('w') as log:
        subprocess.run(command, check=True, stdout=log, stderr=subprocess.STDOUT, timeout=120)
    (output/'consumer-receipt.json').write_text(json.dumps({'command':command,
        'packageReceiptSha256':sha256((package/'build-receipt.json').read_bytes()),
        'sourceSha256':sha256(source.read_bytes()), 'binarySha256':sha256((output/'consumer').read_bytes()),
        'runtimeExecuted':False}, indent=2)+'\n')
    print(output/'consumer')


if __name__ == '__main__':
    main()
