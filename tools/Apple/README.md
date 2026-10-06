# Local Apple packaging

`build_xcframework.py` packages one already-emitted v3 module into a static
XCFramework. It does not translate assemblies, download SDKs, run hosted jobs,
sign/publish an app or execute a simulator. Python 3's standard library and the
Apple tools pinned in `toolchain.json` are required.

From the repository root, pass the `Text`, `Header` and `ManifestJson` files from
`EmbeddingObjectModuleEmitter`:

```bash
python3 -B tools/Apple/build_xcframework.py \
  --source out/engine.cpp --header out/engine.h --manifest out/engine.json \
  --module CilicaEngine --output out/apple-engine
```

Choose a new output directory. Existing directories are rejected, including
partial builds; inspect the logs and choose a new directory after fixing a failed
build. Do not mutate input files during packaging. `--module` names the Clang/Swift
module, static library and bundle; it must be an ASCII identifier starting with an
uppercase letter. It does not change the managed contract or C symbol prefix.

The checked-in pin selects Xcode 26.1.1 (17B100), Apple Clang 17.0.0
(clang-1700.4.4.1), iOS/iOS Simulator SDK 26.1 (23B77), and arm64. The deployment
build floor is iOS 17.0; this is not evidence of execution on iOS 17. Intel simulator
slices are not included. A different `--toolchain` file is an explicit build
configuration change that needs its own validation, not a compatibility guarantee.
The selected Xcode/Clang/SDK versions and builds must match before compilation.

The build checks the canonical contract hash, matching header fingerprint and
header prefix embedded in the generated source. It compiles separate device and
simulator objects at O2 with C++20/libc++, no C++ exceptions/RTTI, hidden internal
symbols, warnings as errors and the existing 2 KiB frame warning gate. No sanitizer
is linked into these distributable static archives. `ZERO_AR_DATE=1` removes archive
timestamps; complete cross-machine bit-for-bit reproducibility is not promised.

Outputs include the XCFramework, unchanged contract manifest, intermediate
objects/archives, per-stage logs and `build-receipt.json`. The receipt records
input/bundle hashes, exact commands, toolchain details and target triples. Bundle
platform/architecture metadata, copied archives and headers are verified. Creation
of a receipt proves packaging only; runtime evidence is separate.

Each slice's Headers directory contains:

- `cilica.h`: the original generated C ABI, byte-for-byte.
- `cilica_apple.h`: typed aliases for numeric macros plus inline default-config
  and contract-hash accessors. Swift's importer does not admit the nested
  `UINT32_C`/`UINT64_C` expressions directly. Alias names are
  `<Module>_<OriginalMacro>`; accessors are `cilica_<contract-name>_apple_*`.
- `module.modulemap`: imports the C interface and requests libc++ linkage. The
  C++ runtime implementation remains behind C functions.

The static library becomes part of the host binary. Use exactly one selected
slice for the host platform; do not link both archives or duplicate the same module
in multiple host images and share tokens between them. The module's registry and
lease scope remain the compiled-image boundary described in the ABI contract.

Focused input/error-order checks (no Apple builds):

```bash
python3 -B -m unittest discover -s tools/Apple -p 'test_*.py' -v
```

For Swift compile/link and optional simulator execution, see
[the Apple smoke consumer](../../examples/Apple/README.md). See also
[Apple's XCFramework guidance](https://developer.apple.com/documentation/xcode/creating-a-multi-platform-binary-framework-bundle)
and [Swift module maps](https://docs.swift.org/main/documentation/packagemanagerdocs/modulemaps/).
