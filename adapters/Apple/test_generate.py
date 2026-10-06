import copy
import unittest
import generate


class GeneratorTests(unittest.TestCase):
    def test_plain_modules_do_not_emit_a_mutable_unused_capture_slot(self):
        # Swift warnings-as-errors rejects an unmutated var when capture and
        # records are both absent. Keep this reduced regression for that variant.
        document = {'profile': 'embedding-object-handles-v3', 'contract': {'abiVersion': 3, 'name': 'sample',
            'exports': [{'id': 1, 'parameters': [], 'result': {'kind': 'int32'}}]}}
        source = generate.generate(document, 'Sample')
        self.assertIn('let exception: UInt64 = 0', source)
        self.assertNotIn('_invoke_capture(', source)
        self.assertNotIn('_invoke_records(', source)

    def test_record_ids_and_stable_field_order_are_preserved(self):
        document = {'profile': 'embedding-object-handles-v3', 'contract': {'abiVersion': 3, 'name': 'sample',
            'records': [{'id': 7, 'fields': [{'id': 20, 'shape': {'kind': 'integer64'}}, {'id': 10, 'shape': {'kind': 'int32'}}]}],
            'exports': [{'id': 1, 'parameters': [{'kind': 'record', 'recordTypeId': 7}], 'result': {'kind': 'void'}}]}}
        source = generate.generate(document, 'Sample')
        self.assertIn('case 1: return ([.record(7, [.int32, .integer64])], .void)', source)
        self.assertIn('throw Failure.unsupported', source)
        self.assertNotIn('func importUTF16', source)
        changed = copy.deepcopy(document)
        changed['contract']['records'][0]['fields'][0]['shape']['kind'] = 'handle'
        with self.assertRaisesRegex(ValueError, 'numeric records'):
            generate.generate(changed, 'Sample')

    def test_untrusted_identifiers_cannot_become_source(self):
        with self.assertRaisesRegex(ValueError, 'module name'):
            generate.generate({}, 'Bad\nimport Other')
        document = {'profile': 'embedding-object-handles-v3', 'contract': {'abiVersion': 3, 'name': 'x',
            'exports': [{'id': '1: return injected', 'parameters': [], 'result': {'kind': 'void'}}]}}
        with self.assertRaisesRegex(ValueError, 'UInt32'):
            generate.generate(document, 'Sample')


if __name__ == '__main__':
    unittest.main()
