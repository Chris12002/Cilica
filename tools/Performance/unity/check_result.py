"""Exact transcript comparison; checksums alone never establish correctness."""
import argparse,json,pathlib
p=argparse.ArgumentParser();p.add_argument('expected',type=pathlib.Path);p.add_argument('actual',type=pathlib.Path);a=p.parse_args()
x=json.loads(a.expected.read_text());y=json.loads(a.actual.read_text())
if y.get('error'):raise SystemExit(y['error'])
if y.get('values')!=x['values']:raise SystemExit('Transcript differs')
if y.get('checksum')!=x['checksum']:raise SystemExit('Checksum differs')
print(f"PASS: {len(x['values'])} exact observations and checksum {x['checksum']}")
