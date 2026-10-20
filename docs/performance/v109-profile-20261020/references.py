from pathlib import Path
import json,hashlib,subprocess,re
out=Path(__file__).resolve().parent;prior=Path('/Users/christian/Cilica/artifacts');refs=json.loads((prior/'v101-profile-20261020/reference-sources.json').read_text());records={};excerpts=[]
def record(p,start=None,end=None):
 p=Path(p);raw=p.read_bytes();records[str(p)]={'sha256':hashlib.sha256(raw).hexdigest()}
 if start:
  lines=raw.decode().splitlines();excerpts.append({'file':str(p),'first_line':start,'last_line':end,'text':'\n'.join(lines[start-1:end])})
for path,sha in refs.items():
 if path.startswith('src/'):continue
 assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==sha,path
 record(path)
roslyn=Path('/tmp/cilica-roslyn-inspection-20261019/src/Compilers/CSharp/Portable/CodeGen/EmitExpression.cs');record(roslyn,2066,2124)
unity=Path('/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app/Contents/Resources/Scripting/il2cpp/libil2cpp')
record(unity/'il2cpp-config.h',79,107);record(unity/'codegen/il2cpp-codegen.h',715,729);record(unity/'vm/String.cpp',101,126);record(unity/'vm/ClassInlines.h',1,40)
app=Path(next(p for p in refs if p.endswith('/DiffPlex.Benchmark.cpp')));record(app,15,53);record(app,2665,2734)
generic=Path(next(p for p in refs if p.endswith('/Generics__3.cpp')));lines=generic.read_text().splitlines();start=next(i+1 for i,l in enumerate(lines) if l.startswith('IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR int32_t Dictionary_2_FindEntry_m68D71E2AAD') and not l.rstrip().endswith(';'));record(generic,start,start+85)
record(Path('library/upstream/dotnet-runtime/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/Dictionary.cs'),1745,1765)
record(prior/'roslyn-il-investigation-20261019/DiffPlex.Benchmark.il',1418,1490)
record(Path('/Users/christian/Cilica/docs/performance/runtime-costs-20261020/dotnet-arm64.txt'),349,430)
inspection=json.loads((prior/'roslyn-il-investigation-20261019/inspection-summary.json').read_text());rea=json.loads((prior/'v101-profile-20261020/rea-selected.json').read_text());assert rea['subject']['digest']['sha256']==hashlib.sha256((out/'baseline-core/Cilica.Core.dll').read_bytes()).hexdigest()
(out/'rea-selected.json').write_text(json.dumps(rea,indent=2));(out/'reference-excerpts.json').write_text(json.dumps(excerpts,indent=2));(out/'reference-sources.json').write_text(json.dumps({'files':records,'roslyn_build_options':inspection['roslyn_build_options'],'roslyn_source_revision':subprocess.check_output(['git','-C','/tmp/cilica-roslyn-inspection-20261019','rev-parse','HEAD'],text=True).strip(),'exact_roslyn_match':False,'unity_editor':'6000.3.2f1','unity_retimed':False,'rea':'Retained REA 6.7.0 bodies reused only after exact core hash verification','primary_web_references':['https://clang.llvm.org/docs/AttributeReference.html','https://unity.com/blog/engine-platform/il2cpp-internals-method-calls','https://unity.com/blog/engine-platform/il2cpp-optimizations-devirtualization']},indent=2))
print('Hashed',len(records),'reference files; retained',len(excerpts),'line-addressed excerpts')
