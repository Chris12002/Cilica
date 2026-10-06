#!/usr/bin/env python3
"""Generate a bounded Kotlin owner and JNI C bridge for an Android v3 package."""
import argparse
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2]/'tools/Android'))
from package_support import package_files


def generate(document, module, activity_host=False):
    contract = document['contract']
    if document['profile'] != 'embedding-object-handles-v3' or contract['abiVersion'] != 3:
        raise ValueError('Expected v3 contract')
    if not re.fullmatch(r'[A-Z][A-Za-z0-9]{0,47}', module) or not re.fullmatch(r'[a-z][a-z0-9_]{0,47}', contract['name']):
        raise ValueError('Invalid module or C prefix')
    records = {r['id']: r for r in contract.get('records', [])}
    def identifier(value):
        if type(value) is not int or not 0 < value <= 0xffffffff:
            raise ValueError('Expected nonzero UInt32 ID')
        return str(value)+'L'
    def shape(value):
        kind = value['kind']
        if kind in ('void', 'int32', 'handle', 'integer64'):
            return 'Shape('+str({'void':0, 'int32':1, 'handle':2, 'integer64':3}[kind])+')', 1
        if kind == 'record':
            record = records[value['recordTypeId']]
            fields = sorted(record['fields'], key=lambda f: f['id'])
            if not 1 <= len(fields) <= 64 or any(f['shape']['kind'] not in ('int32', 'integer64') for f in fields):
                raise ValueError('Only bounded numeric records supported')
            return 'Shape(4, '+identifier(record['id'])+', listOf('+', '.join(shape(f['shape'])[0] for f in fields)+'))', len(fields)
        raise ValueError('Unsupported kind')
    schemas = []
    for export in contract['exports']:
        parameters = [shape(p) for p in export['parameters']]
        if sum(p[1] for p in parameters) > 64:
            raise ValueError('JNI profile admits at most 64 flattened argument values')
        schemas.append('        '+identifier(export['id'])+' -> Pair(listOf('+', '.join(p[0] for p in parameters)+'), '+shape(export['result'])[0]+')')
    root = Path(__file__).parent
    replacements = {'@MODULE@': module, '@PREFIX@': 'cilica_'+contract['name']+'_abi_v3',
                    '@HASH@': 'CILICA_'+contract['name'].upper()+'_ABI_V3_CONTRACT_HASH',
                    '@SCHEMAS@': '\n'.join(schemas), '@STRINGS@': str(bool(contract.get('utf16StringTypeId'))).lower(),
                    '@CAPTURE@': str(bool(contract.get('captureExceptions'))).lower(),
                    '@HAS_STRINGS@': str(int(bool(contract.get('utf16StringTypeId')))),
                    '@HAS_CAPTURE@': str(int(bool(contract.get('captureExceptions')))),
                    '@HAS_RECORDS@': str(int(bool(records)))}
    result = {}
    for template, name in [('Runtime.kt.in', module+'Runtime.kt'), ('Bridge.c.in', 'bridge.c')]:
        text = (root/template).read_text()
        for key, value in replacements.items():
            text = text.replace(key, value)
        result[name] = text
    if activity_host:
        result[module+'ActivityHost.kt'] = (root/'ActivityHost.kt.in').read_text().replace('@MODULE@', module)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--activity-host', action='store_true')
    args = parser.parse_args()
    package = args.package.resolve(strict=True)
    receipt, _ = package_files(package)
    sources = generate(json.loads((package/'contract.json').read_bytes()), receipt['module'], args.activity_host)
    args.output.mkdir(parents=True, exist_ok=False)
    for name, text in sources.items():
        (args.output/name).write_text(text)
    print(args.output)


if __name__ == '__main__':
    main()
