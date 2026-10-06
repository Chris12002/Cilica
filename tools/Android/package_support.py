"""Read local package evidence without trusting receipt paths as shell commands."""
import json
from pathlib import Path
import re

from build_library import sha256


def package_files(package):
    receipt = json.loads((package / 'build-receipt.json').read_bytes())
    if receipt['profile'] != 'android-shared-v3-arm64-v1' or not re.fullmatch(r'[A-Z][A-Za-z0-9]{0,47}', receipt['module']):
        raise ValueError('Expected an arm64 Android v3 package')
    library = 'lib/arm64-v8a/lib' + receipt['module'] + '.so'
    required = {library, 'include/cilica.h', 'contract.json', 'NOTICE.toolchain'}
    if set(receipt['packageFiles']) != required:
        raise ValueError('Unexpected package file set')
    for relative in required:
        path = (package / relative).resolve(strict=True)
        if package not in path.parents or sha256(path.read_bytes()) != receipt['packageFiles'][relative]:
            raise ValueError('Package file/hash mismatch: ' + relative)
    return receipt, package / library


def case_files(package, consumer):
    receipt, library = package_files(package)
    if consumer == 'lifecycle':
        binary = package / 'checks/lifecycle'
        expected = receipt['consumerSha256']
    else:
        folder = Path(consumer).resolve(strict=True)
        evidence = json.loads((folder / 'consumer-receipt.json').read_bytes())
        if evidence['packageReceiptSha256'] != sha256((package / 'build-receipt.json').read_bytes()):
            raise ValueError('Consumer was linked against a different package')
        if sha256((folder / 'consumer.c').read_bytes()) != evidence['sourceSha256']:
            raise ValueError('Consumer source changed after compilation')
        binary = folder / 'consumer'
        expected = evidence['binarySha256']
    if sha256(binary.read_bytes()) != expected:
        raise ValueError('Consumer executable differs from receipt')
    return library, binary
