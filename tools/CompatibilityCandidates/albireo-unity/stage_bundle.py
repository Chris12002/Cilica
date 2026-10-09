"""Stage reviewed data for the Albireo Unity lane. Never downloads, launches or builds."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

PIN = '1eabd2fcba515d0bd63881fd2a2879555c43648b'
LICENSE_BLOB = '1b3da92904fd9e9c1f4f327fafa5c7e0c771b150'
TEMPLATE_FILES = ('package.json', 'ProjectData~/Packages/manifest.json',
                  'ProjectData~/Assets/AlbireoUnityHarness.cs',
                  'ProjectData~/Assets/Editor/AlbireoReferenceBuild.cs')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def stage(source, dll, dll_sha256, license_file, output):
    # Opaque file copy only. A real approved DLL receipt is required by the operator;
    # these hashes establish data identity, not execution permission or safety.
    if dll.is_symlink() or license_file.is_symlink() or not dll.is_file() or not license_file.is_file():
        raise ValueError('Regular input DLL and licence files required')
    if dll.stat().st_size > 16 * 1024 * 1024:
        raise ValueError('Selected DLL exceeds16 MiB bound')
    if digest(dll) != dll_sha256 or len(dll_sha256) != 64:
        raise ValueError('Candidate DLL differs from isolated-build receipt')
    license_data = license_file.read_bytes()
    if hashlib.sha1(b'blob ' + str(len(license_data)).encode() + b'\0' + license_data).hexdigest() != LICENSE_BLOB:
        raise ValueError('Pinned MIT licence differs')
    for path in TEMPLATE_FILES:
        if not (source / 'template' / path).is_file() or (source / 'template' / path).is_symlink():
            raise ValueError('Missing owned template file: ' + path)
    output = output.absolute()
    if any(p.is_symlink() for p in [output, *output.parents]) or output.exists():
        raise ValueError('Use a new nonsymlink output directory')
    # All input checks precede writes; unexpected I/O failures retain partial data.
    output.mkdir(parents=True)
    rows = {}
    for path in TEMPLATE_FILES:
        dest = output / 'template' / path; dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source / 'template' / path, dest); rows[str(dest.relative_to(output))] = digest(dest)
    plugins = output / 'template/ProjectData~/Assets/Plugins'; plugins.mkdir()
    copied = plugins / 'Albireo.SelectedDecode.dll'; shutil.copyfile(dll, copied)
    shutil.copyfile(license_file, output / 'LICENSE.Albireo.txt')
    if digest(copied) != dll_sha256:
        raise ValueError('DLL changed during copy; partial staging retained')
    rows[str(copied.relative_to(output))] = digest(copied); rows['LICENSE.Albireo.txt'] = digest(output / 'LICENSE.Albireo.txt')
    receipt = {'candidate_commit': PIN, 'candidate_dll_sha256': dll_sha256, 'files': rows,
               'executed': False, 'approval': 'No execution permission is conferred; separate Unity host/environment approval required.'}
    (output / 'staging.json').write_text(json.dumps(receipt, indent=2) + '\n')
    return receipt


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--dll', type=Path, required=True); p.add_argument('--dll-sha256', required=True)
    p.add_argument('--license', type=Path, required=True); p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    try:
        result = stage(Path(__file__).resolve().parent, a.dll, a.dll_sha256, a.license, a.output)
        print(json.dumps({'files': len(result['files']), 'executed': False}))
    except (ValueError, OSError) as error:
        p.error(str(error))


if __name__ == '__main__':
    main()
