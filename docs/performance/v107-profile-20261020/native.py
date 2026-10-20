from pathlib import Path
import subprocess,re,json
out=Path(__file__).resolve().parent;summary={};selected=[]
for name in ['baseline','small_only','bounded_combined']:
 text=subprocess.check_output(['xcrun','llvm-objdump','--disassemble','--demangle',str(out/name)],text=True);(out/(name+'-native.txt')).write_text(text)
 ins=[l for l in text.splitlines() if re.match(r'^[0-9a-f]+:',l)]
 bodies=re.split(r'(?=^[0-9a-f]{16} <)',text,flags=re.M)
 d={'instruction_count':len(ins),'instruction_bytes':4*len(ins),'static_predicate_call_sites':sum('\tbl\t' in l and ('_accepts(' in l or '_implements(' in l) for l in ins),'static_kernel_call_sites':sum('\tbl\t' in l and '_kernel(' in l for l in ins),'materialized_kernels':sum('_kernel(' in b.splitlines()[0] for b in bodies if b),'executable_bytes':(out/name).stat().st_size}
 for b in bodies:
  h=b.splitlines()[0] if b else ''
  if ('0600049f' in h and '_kernel(' in h) or ('060004a0' in h and '_local_output(' in h):
   key='Find_kernel' if '0600049f' in h else 'TryGetValue_local_output'
   lines=[l for l in b.splitlines()[1:] if re.match(r'^[0-9a-f]+:',l)];d[key]={'bytes':len(lines)*4,'call_sites':sum('\tbl\t' in l for l in lines)}
   selected += [name+' '+key,b]
 summary[name]=d
(out/'native-summary.json').write_text(json.dumps(summary,indent=2));(out/'native-selected.txt').write_text('\n'.join(selected));print(json.dumps(summary,indent=2))
