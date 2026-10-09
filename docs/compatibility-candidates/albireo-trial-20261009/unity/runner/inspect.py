"""Inspect raw marker data and copied saved settings without loading candidate IL."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
from build import ROOT, CLI, PROJECT, APP, ADAPTER, digest

INSPECT = ROOT/'inspection'
DEST = ROOT/'inspector-projects/AlbireoInspection'

def call(name, command):
    result = subprocess.run([sys.executable,str(ROOT/'frozen/legacy-runner/run_bounded.py'),
              '--log',str(INSPECT/(name+'.log')),'--seconds','1790','--',*command],cwd=ROOT)
    if result.returncode: raise SystemExit(result.returncode)

def main():
    raw = ROOT/'profile/profile.raw'
    assert 0 < raw.stat().st_size <= 67108864
    raw_hash = digest(raw)
    INSPECT.mkdir(); template = INSPECT/'template'; data = template/'ProjectData~'
    (data/'Assets/Editor').mkdir(parents=True); (data/'Packages').mkdir()
    for name in ['ProfileInspection.cs','SettingsReadback.cs']:
        shutil.copy2(ROOT/name, data/'Assets/Editor'/name)
    shutil.copy2(ADAPTER/'template/ProjectData~/Packages/manifest.json',data/'Packages/manifest.json')
    (template/'package.json').write_text(json.dumps({'name':'com.cilica.template.raw-inspection','version':'1.0.0',
        'displayName':'Cilica owned raw inspection','type':'template','unity':'6000.3'}))
    DEST.parent.mkdir()
    call('create',[CLI,'projects','create',DEST.name,'--path',str(DEST.parent),'--editor-version','6000.3.2f1',
           '--architecture','arm64','--template',str(template),'--no-cloud','--non-interactive','--format','json'])
    created = json.loads((INSPECT/'create.log').read_text()); assert created['success'] and not created['data']['opened']
    archive = INSPECT/'create-generated-state'; archive.mkdir()
    # Restore approved jsonserialize-only manifest before the first inspection
    # launch. Preserve CLI-expanded defaults and all its generated state.
    for relative in ['Packages','ProjectSettings','Library','Logs']:
        source = DEST/relative
        if source.exists(): shutil.move(str(source),str(archive/source.name))
    (DEST/'Packages').mkdir(); shutil.copy2(data/'Packages/manifest.json',DEST/'Packages/manifest.json')
    shutil.copytree(PROJECT/'ProjectSettings',DEST/'ProjectSettings')
    (DEST/'Library').mkdir()
    snapshots = {}
    for source in sorted((PROJECT/'ProjectSettings').rglob('*')):
        if source.is_file(): snapshots[str(source.relative_to(PROJECT))] = digest(source)
    for relative in ['Library/LastBuild.buildreport','Library/EditorUserBuildSettings.asset']:
        source = PROJECT/relative
        if source.exists():
            shutil.copy2(source,DEST/relative); snapshots[relative] = digest(source)
    for name, expected in snapshots.items(): assert digest(DEST/name) == expected
    assert not list((DEST/'Assets').rglob('*.dll'))
    (INSPECT/'inputs.json').write_text(json.dumps({'raw_sha256':raw_hash,'saved_settings_sha256':snapshots,
        'inspector_sources':{n:digest(ROOT/n) for n in ['ProfileInspection.cs','SettingsReadback.cs']},
        'candidate_assembly_present':False},indent=2)+'\n')
    os.environ['UNITY_NO_CRASH_REPORT']='1'; os.environ['UNITY_NO_UPDATE_CHECK']='1'
    call('inspect',[CLI,'run',str(DEST),'--editor-version','6000.3.2f1','--timeout','1780','--',
         '-nographics','-logFile','-','-job-worker-count','2','-executeMethod','SettingsReadback.InspectAll',
         '--cilica-profile',str(raw),'--cilica-inspection',str(INSPECT/'markers.json'),
         '--cilica-settings',str(INSPECT/'settings-readback.json')])
    assert digest(raw) == raw_hash
    markers = json.loads((INSPECT/'markers.json').read_text()); assert not markers.get('error') and markers['markerSamples'] == 3
    settings = json.loads((INSPECT/'settings-readback.json').read_text())
    assert settings['backend'] == 'IL2CPP' and settings['architecture'] == 1
    assert settings['compilerConfiguration'] == 'Release' and settings['codeGeneration'] == 'OptimizeSpeed'
    assert settings['stripping'] == 'Minimal' and settings['incrementalGC']
    assert not settings['connectProfiler']
    assert settings['buildReportAvailable'] and settings['reportResult'] == 'Succeeded'
    options = set(settings['reportOptions'].split(', '))
    assert 'Development' in options and 'Il2CPP' in options
    assert not options.intersection({'ConnectWithProfiler','EnableDeepProfilingSupport','AllowDebugging'})
    assert settings['reportOutputPath'] == str(APP)
    (INSPECT/'result.json').write_text(json.dumps({'status':'passed','markers':3,'raw_sha256':raw_hash,
          'settings_verified':True,'candidate_assembly_present':False},indent=2)+'\n')
    print('PASS:3 raw marker samples and saved-settings/BuildReport API readbacks')

if __name__ == '__main__': main()
