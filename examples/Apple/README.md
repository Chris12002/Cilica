# Apple package smoke consumer

This small Swift 6 consumer imports a packaged v3 C module, verifies value/config
sizes, creates and destroys six runtimes, checks an unknown export and rejects
repeated destruction. It is a lifecycle smoke test, not an application SDK,
retained-handle wrapper or complete native phone adapter.

First [build an XCFramework](../../tools/Apple/README.md). Then, from the repository
root, link the same Swift source separately for the arm64 device and simulator:

```bash
python3 -B examples/Apple/check_package.py \
  --package out/apple-engine --output out/apple-smoke
```

The package and output paths may contain spaces. The output must not already
exist. The checker validates packaged file hashes and the copied manifest, renders
`Lifecycle.swift.in` using the emitted module/prefix, selects each SDK explicitly,
and links the corresponding static archive. Swift imports only the C interface;
no C++ interoperability mode or binary Swift module is needed. Generated Swift,
per-target build logs and `smoke-receipt.json` remain in the output directory.

To also execute on an **already booted** simulator, supply its explicit UUID:

```bash
python3 -B examples/Apple/check_package.py \
  --package out/apple-engine --output out/apple-smoke-run \
  --simulator YOUR-BOOTED-SIMULATOR-UUID
```

The checker never chooses/boots/stops a simulator, installs an app, signs a device
build or downloads a runtime. Execution uses `simctl spawn` for this bounded
command-line consumer; a UI application lifecycle is not exercised. The device
executable is linked only and is not physical-device evidence. The simulator must
be able to run the configured minimum iOS target. SDK paths must match the package
receipt; use the pinned Xcode selection for both commands.

Recorded local evidence: Xcode 26.1.1, SDK 26.1, Apple Swift 6.2.1, arm64 iPhone 17
Pro simulator on iOS 26.1 (23B86). The Swift lifecycle smoke passes. Separately, the private
scripted kernel C11 consumer links the same archive and passes 5,376 exact .NET
numeric/UTF-16 comparisons across normal/forced collection and three recreation
cycles. Neither result establishes physical-device, UIKit/background lifecycle,
Intel simulator or earlier-iOS compatibility. Full desktop sanitizer evidence is
recorded in [milestone 7](../../docs/milestones/07-embedding-and-phone-libraries.md).
