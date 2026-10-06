import unittest
from generate import generate


class GeneratorTests(unittest.TestCase):
    def document(self):
        return {'profile': 'embedding-object-handles-v3', 'contract': {
            'abiVersion': 3, 'name': 'probe', 'exports': [
                {'id': 1, 'parameters': [{'kind': 'int32'}], 'result': {'kind': 'int32'}}]}}

    def test_optional_profiles_select_only_available_native_paths(self):
        for strings in (False, True):
            for capture in (False, True):
                document = self.document()
                document['contract'].update(utf16StringTypeId=1 if strings else 0, captureExceptions=capture)
                sources = generate(document, 'Probe')
                self.assertIn('package dev.cilica', sources['ProbeRuntime.kt'])
                self.assertIn('Java_dev_cilica_ProbeNative_call', sources['bridge.c'])
                self.assertNotIn('@', sources['bridge.c'])
                self.assertNotIn('@SCHEMAS@', sources['ProbeRuntime.kt'])
                self.assertIn('if (captureException && !'+str(capture).lower()+')', sources['ProbeRuntime.kt'])

    def test_reference_records_and_excess_flattened_inputs_fail_generation(self):
        document = self.document()
        contract = document['contract']
        contract['records'] = [{'id': 1, 'fields': [{'id': 1, 'shape': {'kind': 'handle'}}]}]
        contract['exports'][0]['parameters'] = [{'kind': 'record', 'recordTypeId': 1}]
        with self.assertRaisesRegex(ValueError, 'numeric records'):
            generate(document, 'Probe')
        contract['records'][0]['fields'] = [{'id': i+1, 'shape': {'kind': 'int32'}} for i in range(64)]
        generate(document, 'Probe') # Input records flatten to fields with no record header.
        contract['exports'][0]['parameters'].append({'kind': 'int32'})
        with self.assertRaisesRegex(ValueError, '64 flattened'):
            generate(document, 'Probe')
        contract['exports'][0]['parameters'] = []
        contract['exports'][0]['result'] = {'kind': 'record', 'recordTypeId': 1}
        generate(document, 'Probe') # 64 result fields fit as well.

    def test_invalid_identifiers_are_rejected(self):
        for identifier in [0, -1, 2**32, '1']:
            document = self.document(); document['contract']['exports'][0]['id'] = identifier
            with self.assertRaisesRegex(ValueError, 'UInt32'):
                generate(document, 'Probe')
        with self.assertRaisesRegex(ValueError, 'module'):
            generate(self.document(), 'Probe;bad')


if __name__ == '__main__':
    unittest.main()
