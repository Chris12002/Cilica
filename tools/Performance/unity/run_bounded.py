"""Run one task-owned process group with a wall/log bound; does not claim VM isolation."""
import argparse
import json
import os
import pathlib
import signal
import subprocess
import time

parser = argparse.ArgumentParser()
parser.add_argument('--log', type=pathlib.Path, required=True)
parser.add_argument('--seconds', type=int, default=1790)
parser.add_argument('command', nargs=argparse.REMAINDER)
args = parser.parse_args()
command = args.command[1:] if args.command[:1] == ['--'] else args.command
if not command or not 0 < args.seconds <= 1800:
    raise SystemExit('A command and a 1–1800 second bound are required')
args.log.parent.mkdir(parents=True, exist_ok=True)
if args.log.exists():
    raise SystemExit('Refusing to overwrite a prior log')

start = time.monotonic()
reason = None

def stop_group(child):
    # Only the new process group owned by this invocation is signalled.
    try:
        os.killpg(child.pid, signal.SIGTERM)
    except ProcessLookupError:
        return
    try:
        child.wait(timeout=5)
    except subprocess.TimeoutExpired:
        try:
            os.killpg(child.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        child.wait()

with args.log.open('xb') as log:
    child = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT,
                             start_new_session=True)
    try:
        while child.poll() is None:
            if time.monotonic() - start > args.seconds:
                reason = 'wall timeout'
            if args.log.stat().st_size > 64 * 1024 * 1024:
                reason = '64 MiB log limit'
            if reason:
                stop_group(child)
                break
            time.sleep(.5)
    except KeyboardInterrupt:
        reason = 'interrupted'
        stop_group(child)

receipt = {'command': command, 'exit_code': child.returncode,
           'elapsed_seconds': time.monotonic() - start, 'stop_reason': reason,
           'isolation': 'normal local account; wall/log bounds only'}
args.log.with_suffix(args.log.suffix + '.receipt.json').write_text(
    json.dumps(receipt, indent=2) + '\n')
print(json.dumps(receipt))
raise SystemExit(child.returncode if not reason else 124)
