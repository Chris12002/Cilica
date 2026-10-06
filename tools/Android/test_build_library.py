import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import build_library as build


class AndroidPackageTests(unittest.TestCase):
    def test_alignment_architecture_and_memory_permissions_are_enforced(self):
        layout = {'FileSummary': {'Format': 'elf64-littleaarch64'}, 'ElfHeader': {'Machine': {'Value': 183}},
                  'ProgramHeaders': [
                      {'ProgramHeader': {'Type': {'Name': 'PT_LOAD'}, 'Alignment': 16384, 'Offset': 128,
                                         'VirtualAddress': 16512, 'Flags': {'Value': 5}}},
                      {'ProgramHeader': {'Type': {'Name': 'PT_GNU_STACK'}, 'Flags': {'Value': 6}}}]}
        self.assertEqual(build.inspect_layout(layout, 16384), [16384])
        for field, value in [('Alignment', 4096), ('VirtualAddress', 16513), ('Flags', {'Value': 7})]:
            changed = copy.deepcopy(layout)
            changed['ProgramHeaders'][0]['ProgramHeader'][field] = value
            with self.assertRaises(ValueError):
                build.inspect_layout(changed, 16384)
        layout['ProgramHeaders'][1]['ProgramHeader']['Flags']['Value'] = 7
        with self.assertRaisesRegex(ValueError, 'non-executable'):
            build.inspect_layout(layout, 16384)
        layout['ElfHeader']['Machine']['Value'] = 62
        with self.assertRaisesRegex(ValueError, 'AArch64'):
            build.inspect_layout(layout, 16384)

    def test_export_list_is_scoped_to_the_emitted_module(self):
        header = ('CILICA_V3_API uint32_t cilica_probe_abi_v3_create(void);\n'
                  'CILICA_V3_API uint64_t cilica_probe_abi_v3_capabilities(void);\n'
                  'uint32_t private_helper(void);\n')
        self.assertEqual(build.exported_functions(header, 'probe'),
                         ['cilica_probe_abi_v3_capabilities', 'cilica_probe_abi_v3_create'])
        with self.assertRaisesRegex(ValueError, 'unique'):
            build.exported_functions(header + header, 'probe')
        with self.assertRaisesRegex(ValueError, 'declarations'):
            build.exported_functions(header, 'other')

    def test_output_protection_and_wrong_ndk_fail_before_compiler(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            contract = {'abiVersion': 3, 'name': 'probe', 'exports': []}
            fingerprint = build.sha256(json.dumps(contract, separators=(',', ':')).encode())
            manifest = root / 'contract.json'
            manifest.write_text(json.dumps({'profile': 'embedding-object-handles-v3', 'contract': contract, 'contractHash': fingerprint}))
            header = root / 'api.h'
            header.write_text(f'#define CILICA_PROBE_ABI_V3_CONTRACT_HASH "{fingerprint}"\n'
                              'CILICA_V3_API uint32_t cilica_probe_abi_v3_create(void);\n')
            source = root / 'engine.cpp'; source.write_bytes(header.read_bytes())
            (root / 'source.properties').write_text('Pkg.Revision = 0\n')
            args = ['--source', str(source), '--header', str(header), '--manifest', str(manifest),
                    '--ndk', str(root), '--module', 'Probe', '--output', str(root)]
            with patch.object(build.subprocess, 'run') as run:
                with self.assertRaisesRegex(ValueError, 'already exists'):
                    build.main(args)
                with self.assertRaisesRegex(ValueError, 'NDK revision'):
                    build.main(args[:-1] + [str(root/'new')])
                run.assert_not_called()
            self.assertFalse((root / 'new').exists())


if __name__ == '__main__':
    unittest.main()
