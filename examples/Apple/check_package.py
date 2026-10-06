#!/usr/bin/env python3
"""Link Swift lifecycle consumers for both Apple slices; optionally run on an existing simulator."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, required=True, help="build_xcframework.py output directory")
    parser.add_argument("--output", type=Path, required=True, help="new smoke build directory")
    parser.add_argument("--simulator", help="existing booted simulator UUID; omit for build-only validation")
    parser.add_argument("--adapter", action="store_true", help="generate and compile the serial Swift runtime owner")
    parser.add_argument("--scene-host", action="store_true", help="include UIKit scene lifecycle integration (requires --adapter)")
    parser.add_argument("--consumer", type=Path, help="custom Swift consumer template (default: lifecycle smoke)")
    parser.add_argument("--support-source", type=Path, action="append", default=[], help="additional Swift template, repeatable")
    args = parser.parse_args()
    if args.scene_host and not args.adapter:
        parser.error("--scene-host requires --adapter")
    package = args.package.resolve(strict=True)
    receipt = json.loads((package / "build-receipt.json").read_bytes())
    module = receipt["module"]
    if not re.fullmatch(r"[A-Z][A-Za-z0-9]{0,47}", module):
        raise ValueError("Invalid module name")
    manifest = json.loads((package / "contract.json").read_bytes())
    if hashlib.sha256((package / "contract.json").read_bytes()).hexdigest() != receipt["inputs"]["manifest"]:
        raise ValueError("Manifest differs from the package build")
    name = manifest["contract"]["name"]
    if not re.fullmatch(r"[a-z][a-z0-9_]{0,47}", name):
        raise ValueError("Invalid export prefix")
    bundle = package / (module + ".xcframework")
    for relative, expected in receipt["bundleFiles"].items():
        artifact = (bundle / relative).resolve(strict=True)
        if bundle not in artifact.parents or hashlib.sha256(artifact.read_bytes()).hexdigest() != expected:
            raise ValueError("Packaged file/hash mismatch: " + relative)
    if args.simulator and not re.fullmatch(r"[A-Fa-f0-9-]{36}", args.simulator):
        raise ValueError("An explicit simulator UUID is required")
    output = args.output.resolve()
    if output.exists():
        raise ValueError("Output already exists; choose a new directory")
    output.mkdir(parents=True)
    commands = []
    sources = []
    templates = [args.consumer or Path(__file__).with_name("Lifecycle.swift.in"), *args.support_source]
    for index, template_path in enumerate(templates):
        source = output / ("Lifecycle.swift" if index == 0 else f"Support{index}.swift")
        source.write_text(template_path.read_text().replace("@MODULE@", module).replace("@PREFIX@", "cilica_" + name + "_abi_v3")
                          .replace("@APPLE_PREFIX@", "cilica_" + name + "_apple"))
        sources.append(str(source))
    if args.adapter:
        adapter = output / "Runtime.swift"
        generator = Path(__file__).resolve().parents[2] / "adapters" / "Apple" / "generate.py"
        command = [sys.executable, "-B", str(generator), "--package", str(package), "--output", str(adapter)]
        if args.scene_host:
            command.append("--scene-host")
        commands.append(command)
        subprocess.run(command, check=True, timeout=30)
        sources.append(str(adapter))
    for sdk, folder, suffix in (("iphoneos", "ios-arm64", ""), ("iphonesimulator", "ios-arm64-simulator", "-simulator")):
        sdk_path = subprocess.check_output(["xcrun", "--sdk", sdk, "--show-sdk-path"], text=True, timeout=30).strip()
        if sdk_path != receipt["sdks"][sdk]["path"]:
            raise ValueError("SDK differs from the package build")
        library = bundle / folder
        command = ["xcrun", "--sdk", sdk, "swiftc", "-swift-version", "6", "-warnings-as-errors", "-parse-as-library",
                   "-target", "arm64-apple-ios" + receipt["toolchain"]["minimumIOS"] + suffix, "-sdk", sdk_path,
                   "-module-cache-path", str(output / "module-cache"), "-I", str(library / "Headers"),
                   *sources, str(library / ("lib" + module + ".a")), "-o", str(output / sdk)]
        commands.append(command)
        with (output / (sdk + "-build.log")).open("w") as log:
            subprocess.run(command, check=True, stdout=log, stderr=subprocess.STDOUT, timeout=120)
    if args.simulator:
        command = ["xcrun", "simctl", "spawn", args.simulator, str(output / "iphonesimulator")]
        commands.append(command)
        with (output / "simulator.txt").open("w") as log:
            subprocess.run(command, check=True, stdout=log, stderr=subprocess.STDOUT, timeout=60)
    (output / "smoke-receipt.json").write_text(json.dumps({"commands": commands,
        "sourceHashes": {Path(path).name: hashlib.sha256(Path(path).read_bytes()).hexdigest() for path in sources},
        "binaryHashes": {sdk: hashlib.sha256((output / sdk).read_bytes()).hexdigest() for sdk in ("iphoneos", "iphonesimulator")},
        "packageReceiptSha256": hashlib.sha256((package / "build-receipt.json").read_bytes()).hexdigest(),
        "swiftVersion": subprocess.check_output(["xcrun", "swiftc", "--version"], text=True, timeout=30).strip(),
        "simulator": args.simulator, "physicalDeviceExecuted": False}, indent=2) + "\n")
    print("Linked both Swift consumers." + (" Simulator lifecycle passed." if args.simulator else " No execution requested."))


if __name__ == "__main__":
    main()
