"""Regression for accidental publication matching; no third-party execution."""
from pathlib import Path
import unittest
import transform

class TransformTests(unittest.TestCase):
    def setUp(self):
        root=Path(__file__).resolve().parents[3]
        self.text='\n'.join((root/p).read_text() for p in [
            'src/Cilica.Compiler/ObjectCppRuntime.cs',
            'src/Cilica.Compiler/ObjectCppMarking.cs',
            'tools/CompatibilityCandidates/diffplex-benchmark/native-host.cpp.template'])

    def test_synthetic_inventory_increment_is_not_allocation_publication(self):
        text=transform.indexed(transform.noop(self.text))
        self.assertEqual(3,text.count('heap.remember(heap.head);'))
        self.assertIn('nodes[i].next=heap.head; heap.head=&nodes[i]; ++heap.count; }',text)
        self.assertEqual(1,text.count('heap.rebuild_index();\n    return report;'))

    def test_unrecognized_allocation_shape_fails_closed(self):
        altered=self.text.replace('heap.bytes += sizeof(T); ++heap.count;',
                                  'heap.bytes += sizeof(T); heap.count += 1;')
        with self.assertRaises(AssertionError):transform.indexed(altered)

    def test_counters_are_absent_from_clean_and_index_variants(self):
        clean=transform.noop(self.text)
        self.assertNotIn('PerfScope',clean)
        self.assertNotIn('PerfScope',transform.indexed(clean))
        profiled=transform.instrument(clean)
        self.assertIn('PerfScope timing(1)',profiled)
        self.assertIn('PerfScope timing(2)',profiled)

if __name__=='__main__':unittest.main()
