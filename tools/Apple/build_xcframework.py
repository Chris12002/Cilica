#!/usr/bin/env python3
"""Package an emitted v3 module using a pinned local Apple toolchain. No downloads/signing."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import plistlib
import re
import shutil
import subprocess
import sys


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def swift_header(header, module, name):
    # Swift cannot import the nested UINT32_C/UINT64_C macro expressions.
    constants = re.findall(r"^#define ([A-Z][A-Z0-9_]+) UINT(32|64)_C\(([0-9]+)\)$", header, re.MULTILINE)
    lines = ['#pragma once', '#include "cilica.h"']
    for symbol, width, value in constants:
        lines.append('static const uint' + width + '_t ' + module + '_' + symbol + ' = UINT' + width + '_C(' + value + ');')
    prefix = "cilica_" + name + "_apple"
    lines += ['static inline cilica_v3_config ' + prefix + '_default_config(void) {',
              '    cilica_v3_config config = CILICA_V3_CONFIG_INIT; return config;', '}',
              'static inline const char* ' + prefix + '_contract_hash(void) {',
              '    return CILICA_' + name.upper() + '_ABI_V3_CONTRACT_HASH;', '}']
    return '\n'.join(lines) + '\n'


def read_inputs(source, header, manifest):
    source_bytes, header_bytes = source.read_bytes(), header.read_bytes()
    document = json.loads(manifest.read_bytes())
    contract = document["contract"]
    if document["profile"] != "embedding-object-handles-v3" or contract["abiVersion"] != 3:
        raise ValueError("Only emitted embedding-object-handles-v3 modules are supported.")
    # The emitter's canonical contract contains ASCII identifiers and numeric shapes.
    canonical = json.dumps(contract, ensure_ascii=True, separators=(",", ":")).encode("ascii")
    fingerprint = sha256(canonical)
    if fingerprint != document["contractHash"]:
        raise ValueError("Manifest contract fingerprint does not match its canonical schema.")
    name = contract["name"]
    if not re.fullmatch(r"[a-z][a-z0-9_]{0,47}", name):
        raise ValueError("Invalid emitted module name.")
    prefix = "cilica_" + name + "_abi_v3"
    macro = (prefix.upper() + '_CONTRACT_HASH "' + fingerprint + '"').encode("ascii")
    if macro not in header_bytes or not source_bytes.startswith(header_bytes):
        raise ValueError("Source, header and manifest are not the same emitted artifact.")
    return document, {"source": sha256(source_bytes), "header": sha256(header_bytes),
                      "manifest": sha256(manifest.read_bytes())}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    for flag in ("source", "header", "manifest", "output"):
        parser.add_argument("--" + flag, type=Path, required=True)
    parser.add_argument("--module", required=True, help="Clang/Swift module and XCFramework name")
    parser.add_argument("--toolchain", type=Path, default=Path(__file__).with_name("toolchain.json"))
    args = parser.parse_args(argv)
    if not re.fullmatch(r"[A-Z][A-Za-z0-9]{0,47}", args.module):
        raise ValueError("Module must be a 1..48 character ASCII identifier starting with an uppercase letter.")
    source, header, manifest = (p.resolve(strict=True) for p in (args.source, args.header, args.manifest))
    document, inputs = read_inputs(source, header, manifest)
    pin = json.loads(args.toolchain.read_bytes())
    if pin["architecture"] != "arm64" or not re.fullmatch(r"[0-9]+\.[0-9]+", pin["minimumIOS"]):
        raise ValueError("This bounded packaging profile requires arm64 and a major.minor deployment target.")
    output = args.output.resolve()
    if output.exists():
        raise ValueError("Output already exists; choose a new directory. Existing artifacts are never overwritten.")
    # Fail on incompatible tools before creating any build products.
    def query(*command):
        return subprocess.run(command, check=True, text=True, stdout=subprocess.PIPE, timeout=30).stdout.strip()
    xcode = query("xcodebuild", "-version")
    if xcode != "Xcode " + pin["xcodeVersion"] + "\nBuild version " + pin["xcodeBuild"]:
        raise ValueError("Selected Xcode differs from the toolchain pin: " + xcode)
    sdks = {}
    for sdk in ("iphoneos", "iphonesimulator"):
        version = query("xcrun", "--sdk", sdk, "--show-sdk-version")
        if version != pin["sdkVersion"]:
            raise ValueError(sdk + " SDK differs from the toolchain pin: " + version)
        sdks[sdk] = {"version": version, "path": query("xcrun", "--sdk", sdk, "--show-sdk-path"),
                     "build": query("xcrun", "--sdk", sdk, "--show-sdk-build-version")}
        if sdks[sdk]["build"] != pin["sdkBuild"]:
            raise ValueError(sdk + " SDK build differs from the toolchain pin.")
    clang = query("xcrun", "--find", "clang++")
    clang_version = query(clang, "--version")
    if clang_version.splitlines()[0] != pin["clangVersion"]:
        raise ValueError("Selected Clang differs from the toolchain pin.")
    libtool = query("xcrun", "--find", "libtool")
    output.mkdir(parents=True)
    env = dict(os.environ, ZERO_AR_DATE="1")
    commands = []
    def run(command, label):
        commands.append(command)
        with (output / (label + ".log")).open("w") as log:
            subprocess.run(command, check=True, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=300)
    headers = output / "Headers"
    headers.mkdir()
    shutil.copyfile(header, headers / "cilica.h")
    (headers / "cilica_apple.h").write_text(swift_header(header.read_text(), args.module, document["contract"]["name"]))
    (headers / "module.modulemap").write_text('module ' + args.module + ' {\n  header "cilica_apple.h"\n  export *\n  link "c++"\n}\n')
    shutil.copyfile(manifest, output / "contract.json")
    slices = []
    for sdk, suffix in (("iphoneos", ""), ("iphonesimulator", "-simulator")):
        folder = output / sdk
        folder.mkdir()
        target = "arm64-apple-ios" + pin["minimumIOS"] + suffix
        obj = folder / "engine.o"
        archive = folder / ("lib" + args.module + ".a")
        run([clang, "-std=c++20", "-stdlib=libc++", "-target", target, "-isysroot", sdks[sdk]["path"],
             "-O2", "-fno-exceptions", "-fno-rtti", "-fvisibility=hidden", "-Wall", "-Wextra", "-Werror",
             "-Wframe-larger-than=2048", "-c", str(source), "-o", str(obj)], sdk + "-compile")
        run([libtool, "-static", "-o", str(archive), str(obj)], sdk + "-archive")
        slices.append({"sdk": sdk, "target": target, "archive": str(archive), "sha256": sha256(archive.read_bytes())})
    bundle = output / (args.module + ".xcframework")
    command = ["xcodebuild", "-create-xcframework"]
    for item in slices:
        command.extend(["-library", item["archive"], "-headers", str(headers)])
    run(command + ["-output", str(bundle)], "xcframework")
    info = plistlib.loads((bundle / "Info.plist").read_bytes())
    libraries = info["AvailableLibraries"]
    expected = {("ios", ""), ("ios", "simulator")}
    actual = {(lib["SupportedPlatform"], lib.get("SupportedPlatformVariant", "")) for lib in libraries}
    if len(libraries) != 2 or actual != expected or any(lib["SupportedArchitectures"] != ["arm64"] for lib in libraries):
        raise ValueError("Unexpected XCFramework platform/architecture layout.")
    for lib in libraries:
        packaged = bundle / lib["LibraryIdentifier"]
        expected_slice = slices[1 if lib.get("SupportedPlatformVariant") == "simulator" else 0]
        if sha256((packaged / lib["LibraryPath"]).read_bytes()) != expected_slice["sha256"]:
            raise ValueError("Packaged archive differs from its compiled slice.")
        for filename in ("cilica.h", "cilica_apple.h", "module.modulemap"):
            if (packaged / lib["HeadersPath"] / filename).read_bytes() != (headers / filename).read_bytes():
                raise ValueError("Packaged header/module map differs from its input.")
    receipt = {"profile": "apple-static-v3-arm64-v1", "module": args.module,
               "contractHash": document["contractHash"], "inputs": inputs, "toolchain": pin,
               "toolchainPinSha256": sha256(args.toolchain.read_bytes()), "selectedXcode": xcode,
               "clang": clang_version, "sdks": sdks, "slices": slices, "commands": commands,
               "validation": "Cross-compiled/package layout verified; no runtime or physical-device claim.",
               "bundleFiles": {str(p.relative_to(bundle)): sha256(p.read_bytes())
                               for p in sorted(bundle.rglob("*")) if p.is_file()}}
    (output / "build-receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")
    print(bundle)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, KeyError, OSError, subprocess.SubprocessError) as error:
        print("Apple package build failed: " + str(error), file=sys.stderr)
        sys.exit(1)
