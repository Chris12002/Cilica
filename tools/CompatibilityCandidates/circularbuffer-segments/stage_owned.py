#!/usr/bin/env python3
"""Stage only owned reference inputs into a new directory; never executes them."""
import argparse
from pathlib import Path
import shutil
p=argparse.ArgumentParser(description=__doc__);p.add_argument('destination',type=Path);a=p.parse_args()
source=Path(__file__).resolve().parent
if a.destination.exists():p.error('Destination must not exist')
shutil.copytree(source/'template/ProjectData~',a.destination)
for name in ['SegmentCompositionProbe.cs','SegmentReferenceCases.cs']:
    shutil.copyfile(source/name,a.destination/'Assets'/name)
