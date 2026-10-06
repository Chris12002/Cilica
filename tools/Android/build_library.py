#!/usr/bin/env python3
"""Cross-build an emitted v3 module with a pinned local NDK. No download or execution."""
import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from embedding_inputs import read_inputs, sha256


def exported_functions(header, name):
    symbols = re.findall(r'^CILICA_V3_API uint(?:32|64)_t (cilica_' + re.escape(name) + r'_abi_v3_[a-z_]+)\(', header, re.MULTILINE)
    if not symbols or len(symbols) != len(set(symbols)):
        raise ValueError('Expected unique emitted C API declarations')
    return sorted(symbols)


def inspect_layout(report, page_alignment):
    if report['FileSummary']['Format'] != 'elf64-littleaarch64' or report['ElfHeader']['Machine']['Value'] != 183:
        raise ValueError('Expected little-endian ELF64 AArch64')
    headers = [item['ProgramHeader'] for item in report['ProgramHeaders']]
    loads = [h for h in headers if h['Type']['Name'] == 'PT_LOAD']
    if not loads or any(h['Alignment'] < page_alignment or
                        h['Offset'] % page_alignment != h['VirtualAddress'] % page_alignment for h in loads):
        raise ValueError('ELF load segments do not support the pinned page alignment')
    stacks = [h for h in headers if h['Type']['Name'] == 'PT_GNU_STACK']
    if len(stacks) != 1 or stacks[0]['Flags']['Value'] & 1:
        raise ValueError('Expected a non-executable GNU stack')
    if any(h['Flags']['Value'] & 3 == 3 for h in loads):
        raise ValueError('Writable executable load segment')
    return [h['Alignment'] for h in loads]


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    for flag in ('source', 'header', 'manifest', 'ndk', 'output'):
        parser.add_argument('--' + flag, type=Path, required=True)
    parser.add_argument('--module', required=True)
    args = parser.parse_args(argv)
    if not re.fullmatch(r'[A-Z][A-Za-z0-9]{0,47}', args.module):
        raise ValueError('Module must be a 1..48 character ASCII identifier starting uppercase')
    output = args.output.resolve()
    if output.exists():
        raise ValueError('Output already exists; choose a new directory')
    source, header, manifest = (p.resolve(strict=True) for p in (args.source, args.header, args.manifest))
    document, inputs = read_inputs(source, header, manifest)
    symbols = exported_functions(header.read_text(), document['contract']['name'])
    pin_path = Path(__file__).with_name('toolchain.json')
    pin = json.loads(pin_path.read_bytes())
    ndk = args.ndk.resolve(strict=True)
    revision = re.search(r'^Pkg.Revision\s*=\s*(\S+)', (ndk / 'source.properties').read_text(), re.MULTILINE)
    if revision is None or revision[1] != pin['ndkRevision']:
        raise ValueError('NDK revision differs from the toolchain pin')
    toolchain = ndk / 'toolchains/llvm/prebuilt' / pin['hostTag']
    tools = toolchain / 'bin'
    clang = tools / 'clang++'
    def query(*command):
        return subprocess.run(command, check=True, text=True, capture_output=True, timeout=30).stdout.strip()
    version = query(str(clang), '--version')
    if version.splitlines()[0] != pin['clangVersion']:
        raise ValueError('Clang version differs from the toolchain pin')
    target = pin['triple'] + str(pin['minimumAPI'])
    output.mkdir(parents=True)
    include = output / 'include'; include.mkdir()
    library_dir = output / 'lib' / pin['abi']; library_dir.mkdir(parents=True)
    checks = output / 'checks'; checks.mkdir()
    shutil.copyfile(header, include / 'cilica.h')
    shutil.copyfile(manifest, output / 'contract.json')
    shutil.copyfile(ndk / 'NOTICE.toolchain', output / 'NOTICE.toolchain')
    exports = output / 'exports.map'
    exports.write_text('{\n  global:\n' + ''.join('    ' + symbol + ';\n' for symbol in symbols) + '  local: *;\n};\n')
    commands = []
    def run(command, label):
        commands.append(command)
        with (output / (label + '.log')).open('w') as log:
            subprocess.run(command, check=True, stdout=log, stderr=subprocess.STDOUT, timeout=300)
    obj = checks / 'engine.o'
    library = library_dir / ('lib' + args.module + '.so')
    common = ['--target=' + target, '-O2', '-fPIC', '-Wall', '-Wextra', '-Werror', '-Wframe-larger-than=2048']
    run([str(clang), *common, '-std=c++20', '-fno-exceptions', '-fno-rtti', '-fvisibility=hidden',
         '-c', str(source), '-o', str(obj)], 'compile')
    alignment = ['-Wl,-z,max-page-size=16384', '-Wl,-z,common-page-size=16384']
    run([str(clang), '--target=' + target, '-shared', '-static-libstdc++', str(obj),
         '-Wl,--version-script=' + str(exports), '-Wl,--no-undefined-version', '-Wl,--no-undefined',
         '-Wl,-soname,' + library.name, '-Wl,-z,relro,-z,now,-z,noexecstack', *alignment,
         '-o', str(library)], 'link')
    layout_command = [str(tools / 'llvm-readobj'), '--elf-output-style=JSON', '--file-headers', '--program-headers', str(library)]
    layout_text = query(*layout_command); commands.append(layout_command)
    (output / 'elf-layout.json').write_text(layout_text + '\n')
    layout = json.loads(layout_text)[0]
    if layout['ElfHeader']['Type'] != 'SharedObject (0x3)' or layout['FileSummary']['LoadName'] != library.name:
        raise ValueError('Unexpected library ELF type or SONAME')
    alignments = inspect_layout(layout, pin['pageAlignment'])
    dynamic_command = [str(tools / 'llvm-readelf'), '--dynamic', str(library)]
    dynamic = query(*dynamic_command); commands.append(dynamic_command)
    (output / 'elf-dynamic.txt').write_text(dynamic + '\n')
    needed = re.findall(r'\(NEEDED\).*\[([^\]]+)\]', dynamic)
    if not needed or not set(needed) <= {'libc.so', 'libm.so', 'libdl.so'} or '(TEXTREL)' in dynamic:
        raise ValueError('Unexpected dynamic dependency or text relocation')
    symbol_command = [str(tools / 'llvm-nm'), '--dynamic', '--defined-only', '--extern-only', '--format=posix', str(library)]
    symbol_text = query(*symbol_command); commands.append(symbol_command)
    (output / 'elf-exports.txt').write_text(symbol_text + '\n')
    actual = sorted(line.split()[0] for line in symbol_text.splitlines())
    if actual != symbols:
        raise ValueError('Dynamic exports differ from the emitted C declarations')
    prefix = 'cilica_' + document['contract']['name'] + '_abi_v3'
    consumer = checks / 'lifecycle.c'
    consumer.write_text(Path(__file__).with_name('Lifecycle.c.in').read_text().replace('@PREFIX@', prefix)
                        .replace('@HASH@', prefix.upper() + '_CONTRACT_HASH'))
    c_object = checks / 'lifecycle.o'
    run([str(tools / 'clang'), *common, '-std=c11', '-I', str(include), '-c', str(consumer), '-o', str(c_object)], 'consumer-compile')
    run([str(tools / 'clang'), '--target=' + target, '-pie', str(c_object), str(library),
         '-Wl,--no-undefined', *alignment, '-o', str(checks / 'lifecycle')], 'consumer-link')
    packaged = [include / 'cilica.h', library, output / 'contract.json', output / 'NOTICE.toolchain']
    receipt = {'profile': 'android-shared-v3-arm64-v1', 'module': args.module,
               'contractHash': document['contractHash'], 'inputs': inputs, 'toolchain': pin,
               'toolchainPinSha256': sha256(pin_path.read_bytes()), 'ndkPath': str(ndk), 'clang': version,
               'target': target, 'exports': symbols, 'needed': needed, 'loadAlignments': alignments,
               'commands': commands, 'runtimeExecuted': False, 'physicalDeviceExecuted': False,
               'validation': 'ELF layout/exports/dependencies verified; C11 consumer linked only',
               'packageFiles': {str(p.relative_to(output)): sha256(p.read_bytes()) for p in packaged},
               'consumerSha256': sha256((checks / 'lifecycle').read_bytes())}
    (output / 'build-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print(library)
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (ValueError, KeyError, OSError, subprocess.SubprocessError) as error:
        print('Android package build failed: ' + str(error), file=sys.stderr)
        sys.exit(1)
