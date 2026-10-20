from pathlib import Path
import json, re
out = Path(__file__).resolve().parent
exec(compile((out/'aggregate.py').read_text(), 'aggregate.py', 'exec'))
data = json.loads((out/'profile-aggregate.json').read_text())
char_samples = char_external = 0
for repeat in range(2):
    parsed = []
    for line in (out/f'sample-{repeat}.txt').read_text().split('Call graph:',1)[1].split('Total number in stack',1)[0].splitlines():
        m = re.match(r'^([\s+!:|]+)(\d+) (.*)$',line)
        if m: parsed.append((len(m[1]),int(m[2]),m[3]))
    stack = []
    for j,(depth,count,label) in enumerate(parsed):
        while stack and stack[-1][0] >= depth: stack.pop()
        is_char = bool(re.search(r'cilica_m_655\w+_060002a[23]',label))
        child = 0; k = j+1
        while k < len(parsed) and parsed[k][0] > depth:
            child += parsed[k][1]; d = parsed[k][0]; k += 1
            while k < len(parsed) and parsed[k][0] > d: k += 1
        exclusive = max(0,count-child)
        if is_char or any(x[1] for x in stack):
            char_samples += exclusive
            if '(in profile)' not in label: char_external += exclusive
        stack.append((depth,is_char))
data['character_subtree_nonadditive'] = {'all_samples':char_samples,'external_samples':char_external,'percent_all':100*char_samples/data['all_samples']}
alloc = {k:v for k,v in data['all_exclusive'].items()
    if any(n in k.lower() for n in ['malloc','free','calloc','operator new','operator delete'])}
data['native_allocation_exclusive'] = {'samples':sum(alloc.values()),'percent_all':100*sum(alloc.values())/data['all_samples'],'members':alloc}
(out/'profile-aggregate.json').write_text(json.dumps(data,indent=2))
c = {k:int(v) for k,v in re.findall(r'(\w+)=(\d+)','\n'.join((out/'counts.txt').read_text().splitlines()[-2:]))}
assert json.loads((out/'counted-output.json').read_text())['checksum']==6146*256
(out/'counts-summary.json').write_text(json.dumps({'iterations':256,'warmup_diffs':8,'totals':c,
    'per_diff':{k:v/256 for k,v in c.items()},
    'notes':['Untimed counters; method wrapper/kernel counts must not be summed as managed calls.',
        'Cumulative arena reservation bytes are not peak live memory.',
        'Direct control flow bypasses counted step helpers.']},indent=2))
metrics = {}
for name in ['baseline','string_pool']:
    lines = [l for l in (out/(name+'-metrics.txt')).read_text().splitlines() if l.startswith(('metrics','pool'))]
    metrics[name] = {k:int(v) for k,v in re.findall(r'(\w+)=(\d+)',' '.join(lines))}
(out/'metrics-summary.json').write_text(json.dumps(metrics,indent=2))
summary = {}; selected = []
for variant,filename in [('baseline','native-arm64.txt'),('inline_fields','inline-native-arm64.txt')]:
    for body in re.split(r'(?=^[0-9a-f]{16} <)',(out/filename).read_text(),flags=re.M):
        heading = body.splitlines()[0] if body else ''
        if not ('field_address(' in heading or ('0600049f' in heading and '_kernel(' in heading) or 'allocate_string(' in heading): continue
        ins = [l for l in body.splitlines()[1:] if re.match(r'^[0-9a-f]+:',l)]
        summary[variant+'/'+heading] = {'instructions':len(ins),'bytes':len(ins)*4,
            'field_address_calls':sum('field_address(' in l for l in ins),
            'indirect_calls':sum('\tblr\t' in l or '\tbr\t' in l for l in ins)}
        if 'field_address(' in heading: selected.extend([variant,body])
(out/'native-summary.json').write_text(json.dumps(summary,indent=2))
(out/'native-selected.txt').write_text('\n'.join(selected).rstrip()+'\n')
