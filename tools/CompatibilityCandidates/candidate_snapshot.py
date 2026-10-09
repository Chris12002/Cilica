#!/usr/bin/env python3
"""Inspect/download reviewed source as data; never build or execute candidates."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
from urllib.parse import quote
from urllib.request import urlopen

OWNER = 'cilica-compatibility-candidate-v1'
MARKER = '.cilica-candidate.json'
MAX_BYTES = 2 * 1024 * 1024


def blob_hash(data):
    return hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()


def validate(candidate):
    for key in ('id', 'commit', 'repository', 'status', 'next_action', 'files'):
        if not candidate.get(key):
            raise ValueError('Missing field: ' + key)
    if not re.fullmatch(r'[a-z0-9-]+', candidate['id']):
        raise ValueError('Invalid candidate ID')
    if not re.fullmatch(r'[0-9a-f]{40}', candidate['commit']):
        raise ValueError('A full pinned commit is required')
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', candidate['repository']):
        raise ValueError('Invalid GitHub repository')
    seen = set()
    for entry in candidate['files']:
        raw = entry['path']
        path = PurePosixPath(raw)
        if (path.is_absolute() or not path.parts or '..' in path.parts or '\\' in raw
                or raw != path.as_posix() or raw == MARKER or raw in seen):
            raise ValueError('Unsafe or duplicate source path: ' + raw)
        if not re.fullmatch(r'[0-9a-f]{40}', entry['git_blob_sha1']):
            raise ValueError('Missing pinned blob hash: ' + raw)
        seen.add(raw)


def target(root, candidate):
    base = root / 'artifacts' / 'compatibility-candidates'
    # Refuse all redirected output parents, including an artifacts symlink.
    for path in (root, root / 'artifacts', base):
        if path.is_symlink():
            raise ValueError('Symlinked snapshot parent: ' + str(path))
    return base / (candidate['id'] + '-' + candidate['commit'])


def marker(candidate):
    return {'owner': OWNER, 'id': candidate['id'], 'commit': candidate['commit']}


def inspect(directory, candidate, complete=True):
    if directory.is_symlink():
        raise ValueError('Symlinked snapshot')
    expected = {entry['path']: entry['git_blob_sha1'] for entry in candidate['files']}
    expected_dirs = {parent.as_posix() for name in expected for parent in PurePosixPath(name).parents if parent != PurePosixPath('.')}
    actual = set()
    for path in directory.rglob('*'):
        name = path.relative_to(directory).as_posix()
        if path.is_symlink():
            raise ValueError('Snapshot contains symlink: ' + name)
        if path.is_dir():
            if name not in expected_dirs:
                raise ValueError('Snapshot contains unexpected directory: ' + name)
            continue
        if name == MARKER:
            continue
        if name not in expected or not path.is_file():
            raise ValueError('Snapshot contains unique/unrecorded content: ' + name)
        if path.stat().st_size > MAX_BYTES or blob_hash(path.read_bytes()) != expected[name]:
            raise ValueError('Snapshot contains modified content: ' + name)
        actual.add(name)
    notice = directory / MARKER
    if not notice.is_file() or json.loads(notice.read_text()) != marker(candidate):
        raise ValueError('Snapshot ownership marker does not match')
    if complete and actual != set(expected):
        raise ValueError('Snapshot is incomplete; resume fetch before using it')
    return actual


def fetch(directory, candidate):
    if directory.exists() or directory.is_symlink():
        existing = inspect(directory, candidate, complete=False)
    else:
        directory.mkdir(parents=True)
        (directory / MARKER).write_text(json.dumps(marker(candidate)) + '\n')
        existing = set()
    for entry in candidate['files']:
        if entry['path'] in existing:
            continue
        url = 'https://raw.githubusercontent.com/{}/{}/{}'.format(
            candidate['repository'], candidate['commit'], quote(entry['path'], safe='/'))
        with urlopen(url, timeout=30) as response:
            if response.geturl() != url:
                raise ValueError('Unexpected source redirect')
            data = response.read(MAX_BYTES + 1)
        if len(data) > MAX_BYTES or blob_hash(data) != entry['git_blob_sha1']:
            raise ValueError('Pinned content mismatch: ' + entry['path'])
        destination = directory / entry['path']
        destination.parent.mkdir(parents=True, exist_ok=True)
        # Exclusive creation never overwrites unique work.
        with destination.open('xb') as stream:
            stream.write(data)
    inspect(directory, candidate)


def stage(directory, candidate, source):
    """Stage connector-returned UTF-8 text, checking every pinned blob first."""
    contents = json.loads(Path(source).read_text())
    expected = {entry['path']: entry['git_blob_sha1'] for entry in candidate['files']}
    if not isinstance(contents, dict) or set(contents) != set(expected):
        raise ValueError('Import must contain exactly the reviewed files')
    encoded = {}
    for name, value in contents.items():
        if not isinstance(value, str):
            raise ValueError('Import content must be text')
        data = value.encode('utf-8')
        if len(data) > MAX_BYTES or blob_hash(data) != expected[name]:
            raise ValueError('Pinned content mismatch: ' + name)
        encoded[name] = data
    if directory.exists() or directory.is_symlink():
        existing = inspect(directory, candidate, complete=False)
    else:
        directory.mkdir(parents=True)
        (directory / MARKER).write_text(json.dumps(marker(candidate)) + '\n')
        existing = set()
    for name, data in encoded.items():
        if name in existing:
            continue
        path = directory / name
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open('xb') as stream:
            stream.write(data)
    inspect(directory, candidate)


def cleanup(directory, candidate):
    if candidate['status'] not in ('mapped-skip', 'mapped-blocked', 'verified'):
        raise ValueError('Record a mapped/finished outcome and next action before cleanup')
    actual = inspect(directory, candidate, complete=False)
    # No recursive deletion: only verified pinned files and our exact marker.
    for name in actual:
        (directory / name).unlink()
    (directory / MARKER).unlink()
    for path in sorted(directory.rglob('*'), key=lambda p: len(p.parts), reverse=True):
        path.rmdir()
    directory.rmdir()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['status', 'validate', 'fetch', 'stage', 'verify', 'cleanup'])
    parser.add_argument('candidate', nargs='?')
    parser.add_argument('--batch', default='batch-001')
    parser.add_argument('--input-json')
    args = parser.parse_args()
    if not re.fullmatch(r'batch-[0-9]{3}', args.batch):
        parser.error('Batch must be named batch-NNN')
    root = Path(__file__).resolve().parents[2]
    ledger = json.loads((root / 'docs/compatibility-candidates' / (args.batch + '.json')).read_text())
    candidates = ledger['candidates']
    from reference_workflow import check_batch
    for candidate in candidates:
        validate(candidate)
        check_batch(root, args.batch, candidate)
    if args.action in ('status', 'validate'):
        for candidate in candidates:
            print(candidate['id'], candidate['commit'], candidate['status'], candidate['next_action'], sep=' | ')
        return
    matches = [c for c in candidates if c['id'] == args.candidate]
    if len(matches) != 1:
        parser.error('Select one known candidate ID')
    candidate = matches[0]
    directory = target(root, candidate)
    if args.action == 'stage':
        if not args.input_json:
            parser.error('stage requires --input-json with reviewed UTF-8 text')
        stage(directory, candidate, args.input_json)
    else:
        {'fetch': fetch, 'verify': inspect, 'cleanup': cleanup}[args.action](directory, candidate)
    print(args.action + ': ' + str(directory))


if __name__ == '__main__':
    main()
