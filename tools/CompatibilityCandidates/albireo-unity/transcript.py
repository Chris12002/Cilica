"""Data-only Albireo oracle normalization and exact Unity comparison; never loads a DLL."""
import argparse
import json
from pathlib import Path
import re

PIN = '1eabd2fcba515d0bd63881fd2a2879555c43648b'


def checksum(values):
    result = 0
    for value in values:
        result = (result * 31 + value) & 0xffffffff
    return result if result < 0x80000000 else result - 0x100000000


def normalize(rows, fingerprint):
    if not re.fullmatch('[0-9a-f]{64}', fingerprint):
        raise ValueError('Exact input fingerprint required')
    if not isinstance(rows, list) or len(rows) > 32 * 4097:
        raise ValueError('Oracle transcript bound')
    flat = []
    cursor = 0
    for scenario in range(32):
        if cursor >= len(rows) or not isinstance(rows[cursor], list) or rows[cursor][:2] != [scenario, -1]:
            raise ValueError('Missing ordered scenario length')
        length = rows[cursor][2] if len(rows[cursor]) == 3 else None
        if type(length) is not int or not 1 <= length <= 4096:
            raise ValueError('Scenario transcript bound')
        for index in range(-1, length):
            if cursor >= len(rows):
                raise ValueError('Truncated transcript')
            row = rows[cursor]
            if not isinstance(row, list) or len(row) != 3 or row[:2] != [scenario, index]:
                raise ValueError('Missing or reordered observation')
            if any(type(v) is not int or v < -2147483648 or v > 2147483647 for v in row):
                raise ValueError('Observation must be exact Int32')
            flat.extend(row); cursor += 1
    if cursor != len(rows):
        raise ValueError('Unexpected observations')
    return {'candidateCommit': PIN, 'inputFingerprint': fingerprint, 'scenarioCount': 32,
            'values': flat, 'checksum': checksum(flat)}


def compare(expected, actual):
    if actual.get('error'):
        raise ValueError('Unity reported: ' + str(actual['error']))
    if actual.get('mode') not in ('verify', 'profile'):
        raise ValueError('Expected executed verify/profile result')
    if not isinstance(actual.get('values'), list) or any(type(v) is not int for v in actual['values']) or type(actual.get('checksum')) is not int:
        raise ValueError('Actual observations/checksum must be Int32 integers')
    for key in ('candidateCommit', 'inputFingerprint', 'scenarioCount', 'values', 'checksum'):
        if actual.get(key) != expected[key]:
            raise ValueError('Exact result differs: ' + key)
    if expected['candidateCommit'] != PIN or not re.fullmatch('[0-9a-f]{64}', expected['inputFingerprint']):
        raise ValueError('Unexpected result identity')
    if expected['checksum'] != checksum(expected['values']):
        raise ValueError('Invalid expected checksum')
    # Revalidate flattened expected data instead of accepting an incomplete oracle.
    values = expected['values']
    if len(values) % 3 or normalize([values[i:i+3] for i in range(0, len(values), 3)], expected['inputFingerprint']) != expected:
        raise ValueError('Incomplete expected transcript')
    if actual['mode'] == 'profile':
        if actual.get('profileScenarios') != [2, 8, 29, 31] or actual.get('warmIterations') != 8:
            raise ValueError('Profile scope differs')
        if type(actual.get('iterations')) is not int or not 1 <= actual['iterations'] <= 128:
            raise ValueError('Profile iteration bound')
        ticks = actual.get('elapsedTicks')
        if not isinstance(ticks, list) or len(ticks) != 3 or any(type(v) is not int or v < 0 for v in ticks):
            raise ValueError('Three complete diagnostic samples required')
        if type(actual.get('frequency')) is not int or actual['frequency'] <= 0:
            raise ValueError('Clock frequency required')
        lengths = {values[i]: values[i+2] for i in range(0, len(values), 3) if values[i+1] == -1}
        expected_sum = sum(lengths[s] for s in [2, 8, 29, 31]) * actual['iterations']
        if actual.get('sampleChecksums') != [expected_sum] * 3:
            raise ValueError('Representative observations changed')
        if not actual.get('profileFile') or not actual.get('qualification'):
            raise ValueError('Raw profile location and diagnostic qualification required')
    return len(values) // 3


def main():
    p = argparse.ArgumentParser(description=__doc__)
    sub = p.add_subparsers(dest='action', required=True)
    create = sub.add_parser('expected'); create.add_argument('oracle', type=Path)
    create.add_argument('--fingerprint', required=True); create.add_argument('--output', type=Path, required=True)
    check = sub.add_parser('compare'); check.add_argument('expected', type=Path); check.add_argument('actual', type=Path)
    a = p.parse_args()
    try:
        if a.action == 'expected':
            result = normalize(json.loads(a.oracle.read_text()), a.fingerprint)
            with a.output.open('x') as f:
                json.dump(result, f); f.write('\n')
            print('Normalized32 scenarios; no candidate code executed')
        else:
            n = compare(json.loads(a.expected.read_text()), json.loads(a.actual.read_text()))
            print('PASS:', n, 'exact observations; candidate/input pins match')
    except (ValueError, OSError, KeyError, TypeError, IndexError) as error:
        p.error(str(error))


if __name__ == '__main__':
    main()
