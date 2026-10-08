"""Build diagnostic variants with authored boundary cases, separate from timing."""
from pathlib import Path
import argparse,hashlib
from lazy_prefix import transform,SOURCE_SHA
from transform import replace_once
p=argparse.ArgumentParser();p.add_argument('source',type=Path);p.add_argument('output',type=Path);p.add_argument('mode',choices=['tests','forced','baseline-tail']);a=p.parse_args()
assert hashlib.sha256(a.source.read_bytes()).hexdigest()==SOURCE_SHA
text=a.source.read_text()
if a.mode!='baseline-tail':text=transform(text)
if a.mode=='forced':
    text=replace_once(text,'Instance heap;Context c(heap);','Instance heap;heap.collect_before_allocation=true;Context c(heap);')
else:
    cases=Path(__file__).with_name('prefix_cases.cpp').read_text()
    if a.mode=='baseline-tail':
        first=cases.index('[[gnu::noinline]] static void unreadable_tail(');last=cases.index('\nstatic void nested_prepare',first)
        cases=cases[:cases.index('[[gnu::noinline]]')]+cases[first:last]
        start='int main(int argc,char** argv) {\n    if(argc<3)return 1;\n    unreadable_tail(std::strcmp(argv[1],"bad-tail")==0);return 0;'
    else:
        text='#define CILICA_PREFIX_TEST 1\n'+text
        start='int main(int argc,char** argv) {\n    if(argc>1 && (std::strcmp(argv[1],"tests")==0 || std::strcmp(argv[1],"tail")==0 || std::strcmp(argv[1],"bad-tail")==0)) return prefix_cases(argv[1]);'
    text=replace_once(text,'int main(int argc,char** argv) {',cases+'\n'+start)
a.output.write_text(text)
