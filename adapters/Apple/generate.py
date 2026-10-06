#!/usr/bin/env python3
"""Generate a serial Swift runtime owner from an existing Apple v3 package."""
import argparse
import hashlib
import json
from pathlib import Path
import re


def generate(document, module, scene_host=False):
    if not re.fullmatch(r"[A-Z][A-Za-z0-9]{0,47}", module):
        raise ValueError("Invalid Swift module name")
    contract = document["contract"]
    name = contract["name"]
    if document['profile'] != 'embedding-object-handles-v3' or contract['abiVersion'] != 3:
        raise ValueError('Expected an emitted v3 module')
    if not re.fullmatch(r"[a-z][a-z0-9_]{0,47}", name):
        raise ValueError("Invalid C prefix")
    def identifier(value):
        if type(value) is not int or not 0 < value <= 0xffffffff:
            raise ValueError('IDs must be nonzero UInt32 values')
        return str(value)
    records = {r['id']: r for r in contract.get('records', [])}
    def shape(value):
        kind = value['kind']
        if kind in ('int32', 'integer64', 'handle', 'void'):
            return '.' + kind
        if kind == 'record':
            record = records[value['recordTypeId']]
            fields = sorted(record['fields'], key=lambda f: f['id'])
            if not 1 <= len(fields) <= 64 or any(f['shape']['kind'] not in ('int32', 'integer64') for f in fields):
                raise ValueError('Only bounded numeric records are supported')
            return '.record(' + identifier(record['id']) + ', [' + ', '.join(shape(f['shape']) for f in fields) + '])'
        raise ValueError('Unsupported shape: ' + kind)
    schemas = []
    for export in contract['exports']:
        schemas.append('        case ' + identifier(export['id']) + ': return ([' +
                       ', '.join(shape(p) for p in export['parameters']) + '], ' + shape(export['result']) + ')')
    capture = contract.get('captureExceptions', False)
    invocation = '' if capture else '                if captureException { throw Failure.unsupported }\n'
    if records:
        invocation += '''                return withUnsafeMutablePointer(to: &exception) { captured in
                    @PREFIX@_invoke_records(token, export, input.baseAddress, UInt32(input.count), output.baseAddress,
                        UInt32(output.count), &result, captureException ? captured : nil)
                }'''
    elif capture:
        invocation += '''                if captureException {
                    return @PREFIX@_invoke_capture(token, export, input.baseAddress, UInt32(input.count), &result, &exception)
                }
                return @PREFIX@_invoke(token, export, input.baseAddress, UInt32(input.count), &result)'''
    else:
        invocation += '                return @PREFIX@_invoke(token, export, input.baseAddress, UInt32(input.count), &result)'
    root = Path(__file__).parent
    text = (root/'Runtime.swift.in').read_text().replace('@SCHEMAS@', '\n'.join(schemas)).replace('@INVOKE@', invocation)
    text = text.replace('@STRING_METHODS@', (root/'Strings.swift.in').read_text() if contract.get('utf16StringTypeId', 0) else '')
    text = text.replace('@EXCEPTION_METHODS@', (root/'Exceptions.swift.in').read_text() if capture else '')
    # Capturing native paths do not throw inside their buffer closures.
    if capture:
        text = text.replace('let status: UInt32 = try inputs', 'let status: UInt32 = inputs').replace('            try fields.with', '            fields.with')
    if not capture and not records:
        text = text.replace('var exception: UInt64 = 0', 'let exception: UInt64 = 0')
    if scene_host:
        host = (root/'SceneHost.swift.in').read_text()
        strings = '''    public func importUTF16(_ units: [UInt16]) async throws -> @MODULE@Runtime.Handle {
        try requireAdmission()
        return try await runtime.importUTF16(units)
    }''' if contract.get('utf16StringTypeId', 0) else ''
        text += '\n' + host.replace('@HOST_STRING_METHODS@', strings)
    return text.replace('@MODULE@', module).replace('@PREFIX@', 'cilica_' + name + '_abi_v3').replace('@APPLE_PREFIX@', 'cilica_' + name + '_apple')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--scene-host', action='store_true', help='include a UIKit scene lifecycle owner')
    args = parser.parse_args()
    package = args.package.resolve(strict=True)
    receipt = json.loads((package/'build-receipt.json').read_bytes())
    raw = (package/'contract.json').read_bytes()
    if hashlib.sha256(raw).hexdigest() != receipt['inputs']['manifest']:
        raise ValueError('Manifest differs from the package receipt')
    source = generate(json.loads(raw), receipt['module'], scene_host=args.scene_host)
    # Exclusive creation also protects an existing adapter from accidental replacement.
    with args.output.open('x') as output:
        output.write(source)
    print(args.output)


if __name__ == '__main__':
    main()
