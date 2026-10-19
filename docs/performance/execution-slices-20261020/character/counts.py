from pathlib import Path
import re,subprocess,json,hashlib
out=Path(__file__).resolve().parent
source=(out/'candidate.cpp').read_text()
s='#include <cstdio>\nstatic unsigned long long observed_arrays=0, observed_strings=0;\n'+source
s,n=re.subn(r'(static Object\* allocate_array\([^\n]+\) noexcept \{)',r'\1 ++observed_arrays;',s);assert n==1
s,n=re.subn(r'(static Object\* allocate_string\([\s\S]*?\) noexcept \{)',r'\1 ++observed_strings;',s);assert n==1
anchor='auto ns=std::chrono::duration_cast<std::chrono::nanoseconds>(clock::now()-start).count();\n        std::printf'
assert s.count(anchor)==2
s=s.replace(anchor,'std::fprintf(stderr,"arrays=%llu strings=%llu\\n",observed_arrays,observed_strings);\n        '+anchor,1)
path=out/'counted.cpp';path.write_text(s)
command=['clang++','-std=c++20','-O2','-DNDEBUG','-fno-exceptions','-fno-rtti','-fstack-usage','-Wframe-larger-than=2048','-Werror',str(path),'-o',str(out/'counted')]
build=subprocess.run(command,capture_output=True,text=True,timeout=240);(out/'counted-build.log').write_text(build.stdout+build.stderr);build.check_returncode()
r=subprocess.run([str(out/'counted'),'cold','0'],capture_output=True,text=True,check=True,timeout=60)
assert json.loads(r.stdout)['checksum']==6146
(out/'counted-output.json').write_text(r.stdout);(out/'counts.txt').write_text(r.stderr)
rows=[line.split('\t') for line in (out/'counted.su').read_text().splitlines()]
summary={'method':'Diagnostic allocation call counts only; not timing evidence','command':command,'source_sha256':hashlib.sha256(source.encode()).hexdigest(),'counts':r.stderr.strip(),'maximum_native_frame_bytes':max(int(row[-2]) for row in rows),'stack_usage_kinds':sorted(set(row[-1] for row in rows))}
(out/'counts-summary.json').write_text(json.dumps(summary,indent=2)+'\n');print(json.dumps(summary,indent=2))
