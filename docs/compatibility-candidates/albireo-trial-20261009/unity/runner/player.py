"""One bounded LaunchServices player run. Never kill a process without exact owned identity."""
import argparse
import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import plistlib
import signal
import subprocess
import time
from build import ROOT, PROJECT, APP, verify_inputs, digest

CAP = 64 * 1024 * 1024

def processes(binary):
    # macOS comm is the full executable path; lstart pins the PID's creation.
    output = subprocess.check_output(['/bin/ps','-axo','pid=,lstart=,comm='], text=True, timeout=5)
    found = {}
    for row in output.splitlines():
        parts = row.strip().split(None, 6)
        if len(parts) == 7 and parts[6] == str(binary):
            found[int(parts[0])] = ' '.join(parts[1:6])
    return found

def terminate_owned(binary, known):
    actions = []
    for sig in (signal.SIGTERM, signal.SIGKILL):
        live = processes(binary)
        for pid, stamp in known.items():
            if live.get(pid) != stamp: continue
            try:
                os.kill(pid, sig); actions.append({'pid':pid,'start':stamp,'signal':int(sig)})
            except ProcessLookupError: pass
        if sig == signal.SIGTERM: time.sleep(1)
    return actions

def main():
    parser = argparse.ArgumentParser(); parser.add_argument('mode', choices=['verify','profile'])
    a = parser.parse_args(); h = verify_inputs()
    build = json.loads((ROOT/'build/build-provenance.json').read_text())
    for name, expected in build['native_sha256'].items(): assert digest(APP/name) == expected
    with (APP/'Contents/Info.plist').open('rb') as stream: name = plistlib.load(stream)['CFBundleExecutable']
    assert Path(name).name == name
    binary = APP/'Contents/MacOS'/name
    assert not processes(binary), 'Existing player at this exact path; do not duplicate'
    out = ROOT/a.mode; out.mkdir()
    result_path = out/'result.json'; player_log = out/'player.log'; raw = out/'profile.raw'
    cmd = ['/usr/bin/open','-W','-n','-g','-a',str(APP),'--args','-batchmode','-nographics',
           '-logFile',str(player_log),'--cilica-mode',a.mode,'--cilica-result',str(result_path),
           '--cilica-fingerprint',h['input_fingerprint']]
    if a.mode == 'profile': cmd += ['--cilica-profile',str(raw),'--cilica-iterations','128']
    receipt = {'command':cmd, 'input_fingerprint':h['input_fingerprint'], 'mode':a.mode,
               'start_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
               'wall_bound_seconds':180, 'raw_soft_cap_bytes':CAP, 'console_cap_bytes':CAP,
               'launch_route':'LaunchServices open -W -n -g', 'quiet_host':False,
               'qualification':'Development diagnostic; no competitive ratio or pure Decode timing',
               'player_identities':{}, 'stop_reason':None, 'max_observed_raw_bytes':0}
    known = {}; start = time.monotonic(); monitor_error = None
    with (out/'launch.log').open('xb') as log:
        child = subprocess.Popen(cmd, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        try:
            while child.poll() is None:
                known.update(processes(binary))
                if time.monotonic()-start > 180: receipt['stop_reason'] = '180 second wall bound'
                for file in [out/'launch.log', player_log, raw, result_path]:
                    if not file.exists(): continue
                    size = file.stat().st_size
                    if file == raw: receipt['max_observed_raw_bytes'] = max(receipt['max_observed_raw_bytes'], size)
                    if size > CAP: receipt['stop_reason'] = str(file.name)+' exceeded64 MiB soft cap'
                if receipt['stop_reason']: break
                time.sleep(.1)
        except BaseException as error:
            receipt['stop_reason'] = type(error).__name__+': '+str(error); monitor_error = error
        finally:
            if receipt['stop_reason']:
                receipt['termination'] = terminate_owned(binary, known)
                if child.poll() is None:
                    os.killpg(child.pid, signal.SIGTERM)
                    try: child.wait(timeout=3)
                    except subprocess.TimeoutExpired:
                        os.killpg(child.pid, signal.SIGKILL); child.wait()
            receipt['exit_code'] = child.poll()
            receipt['elapsed_seconds'] = time.monotonic()-start
            receipt['player_identities'] = known
            receipt['remaining_exact_path_players'] = processes(binary)
            receipt['artifacts'] = {f.name:{'bytes':f.stat().st_size,'sha256':digest(f)}
                         for f in [out/'launch.log',player_log,raw,result_path] if f.is_file()}
            (out/'launch-receipt.json').write_text(json.dumps(receipt, indent=2)+'\n')
    assert not receipt['remaining_exact_path_players'], 'Owned player still active; receipt retained'
    assert receipt['exit_code'] == 0 and not receipt['stop_reason'], 'Launch/watchdog failed; raw evidence retained'
    for item in receipt['artifacts'].values(): assert item['bytes'] <= CAP, 'Final closed artifact over cap'
    if a.mode == 'profile': assert 0 < raw.stat().st_size <= CAP
    spec = importlib.util.spec_from_file_location('transcript', ROOT/'frozen/tools/CompatibilityCandidates/albireo-unity/transcript.py')
    transcript = importlib.util.module_from_spec(spec); spec.loader.exec_module(transcript)
    expected = json.loads((ROOT/'handoff/expected.json').read_text())
    actual = json.loads(result_path.read_text())
    check = {'input_fingerprint':h['input_fingerprint'], 'mode':a.mode, 'status':'failed'}
    try:
        check['observations'] = transcript.compare(expected, actual)
        check['status'] = 'passed'
    except Exception as error:
        check['error'] = str(error)
        raise
    finally:
        (out/'comparison.json').write_text(json.dumps(check, indent=2)+'\n')
    print(json.dumps(check))

if __name__ == '__main__': main()
