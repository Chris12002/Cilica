"""Owned checks for evidence rejection and bounded execution; no Unity invocation."""
import json,pathlib,subprocess,sys,tempfile,unittest
HERE=pathlib.Path(__file__).resolve().parent
class PipelineChecks(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=pathlib.Path(self.temp.name)
 def tearDown(self):self.temp.cleanup()
 def run_script(self,name,*args):return subprocess.run([sys.executable,str(HERE/name),*map(str,args)],capture_output=True,text=True)
 def test_exact_observations_required_even_when_checksum_matches(self):
  expected=self.root/'expected.json';actual=self.root/'actual.json';expected.write_text(json.dumps({'values':[1,2,3],'checksum':9}))
  for values,code in [([1,2,3],0),([1,3,2],1),([1,2],1)]:
   actual.write_text(json.dumps({'values':values,'checksum':9}));r=self.run_script('check_result.py',expected,actual);self.assertEqual(r.returncode,code)
 def test_error_overrides_matching_output(self):
  x={'values':[1],'checksum':1};e=self.root/'e.json';a=self.root/'a.json';e.write_text(json.dumps(x));x['error']='failed';a.write_text(json.dumps(x));self.assertNotEqual(self.run_script('check_result.py',e,a).returncode,0)
 def test_capture_requires_workload_cpp_and_hashes_it(self):
  p=self.root/'project/Library/Bee/artifacts/test';p.mkdir(parents=True);(p/'other.cpp').write_text('void Other() {}')
  self.assertNotEqual(self.run_script('capture.py','--project',self.root/'project','--output',self.root/'missing').returncode,0)
  (p/'workload.cpp').write_text('void BenchProbe_Run() {}')
  self.assertEqual(self.run_script('capture.py','--project',self.root/'project','--output',self.root/'capture').returncode,0)
  m=json.loads((self.root/'capture/manifest.json').read_text());self.assertEqual(m['cpp_files'],2);self.assertEqual(len(m['benchmark_cpp']),1)
 def test_nonzero_exit_is_preserved_and_log_not_overwritten(self):
  log=self.root/'child.log';r=self.run_script('run_bounded.py','--log',log,'--',sys.executable,'-c','raise SystemExit(7)');self.assertEqual(r.returncode,7)
  before=log.read_bytes();r=self.run_script('run_bounded.py','--log',log,'--',sys.executable,'-c','print(123)');self.assertNotEqual(r.returncode,0);self.assertEqual(log.read_bytes(),before)
 def test_timeout_has_failed_receipt(self):
  log=self.root/'timeout.log';r=self.run_script('run_bounded.py','--log',log,'--seconds','1','--',sys.executable,'-c','import time; time.sleep(20)');self.assertEqual(r.returncode,124)
  receipt=json.loads(log.with_suffix('.log.receipt.json').read_text());self.assertEqual(receipt['stop_reason'],'wall timeout')
if __name__=='__main__':unittest.main()
