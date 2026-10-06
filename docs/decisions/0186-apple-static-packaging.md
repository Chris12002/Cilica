# ADR 0186: Pinned Apple static packaging

Accepted 2026-10-06; milestone 7d.1. Simulator execution and device cross-linking
only; physical-device and full application lifecycle gates remain open.

## Context

The C ABI has desktop kernel evidence but no Apple package. Shipping C++ internals
or a binary Swift implementation would add interfaces beyond the validated C
contract. Device and simulator binaries must remain distinct even when both use
arm64. The local machine has Xcode and an already booted simulator, but no connected
physical device was found.

## Decision

Package an existing v3 emitted source/header/manifest with a small Python standard-
library tool. It compiles two static archives and uses Apple's XCFramework builder
with separate device and simulator slices. Pin Xcode 26.1.1 (17B100), Clang 17.0.0
(clang-1700.4.4.1), both SDKs 26.1 (23B77), arm64 and an iOS 17.0 deployment build
floor. The floor is a build setting, not an earlier-OS execution guarantee.

Verify contract fingerprints and matching artifacts before invoking native builds;
reject mismatched toolchains and existing output directories. Keep partial logs
on failures. Record exact commands, input hashes, selected toolchain and resulting
bundle hashes. Verify platform/architecture metadata and byte-identical copies of
the archives and headers in the bundle. Stable archive timestamps improve local
repeatability without promising cross-machine byte identity.

Use C++20/libc++, O2, hidden implementation symbols, disabled exceptions/RTTI,
warnings as errors and the existing 2 KiB frame gate. Distributable libraries are
not sanitizer-instrumented. The unchanged C header remains the ABI; a module map
imports it for Swift and requests libc++ linkage. Static linking puts registry
ownership in the host binary. Do not duplicate a module in multiple images and
assume tokens can cross those images.

Swift does not import the header's nested UINT32_C/UINT64_C macros. Add a separate
Apple-only C header with typed constant aliases and inline default-config/hash
accessors. Names are scoped by module or contract prefix. This is an import bridge,
not an ABI change, a C++ interface or a managed object ownership wrapper.

Provide a Swift lifecycle smoke template and bounded checker. It verifies package
hashes, links device/simulator executables with explicit SDK selection and only
executes when an explicit already-booted simulator UUID is supplied. It does not
download runtimes, start/stop simulators, install/sign apps or publish packages.
A complete Swift application adapter and dedicated worker scheduling remain later
work; the current consumer runs synchronous lifecycle calls.

## Evidence and review

The unchanged scripted kernel module cross-compiles and packages for both slices.
Mach-O metadata identifies IOS/IOSSIMULATOR, min OS 17.0 and SDK 26.1. A separately
compiled C11 consumer links the packaged simulator archive and passes 5,376 exact
.NET numeric/UTF-16 comparisons on the iPhone 17 Pro iOS 26.1 simulator: three
recreation cycles each with normal and forced collection. Desktop sanitizer
coverage remains the 7c.6 evidence; simulator tests used the O2 package itself.

The final Swift 6.2.1 consumer links against each final slice without build
warnings and passes six simulator lifecycle cycles, C layout checks, negotiation,
unknown-export and stale-runtime rejection. The initial Swift importer failure
motivated the typed bridge; a linker sysroot warning was fixed by explicit
per-platform xcrun SDK selection. Initial/final native archive hashes match.
Three local Python checks cover mismatched artifacts/schema, protected outputs,
invalid module names and toolchain failure before output creation.

Review covered argument/path handling, fingerprint checks, stable C ownership,
separate SDK targets, libc++ linkage, typed macro widths, bounded subprocesses,
bundle contents and evidence limits. No unresolved issue remains in this slice.
Private evidence is `artifacts/milestone7-ios/evidence-7d1.json`. No application
rules, compiler/runtime semantics, hosted automation, remote history or spending
configuration changed. Physical devices, UI/background lifecycle, Intel simulators,
earlier iOS execution and a full native app adapter remain unverified.

References: [Apple XCFramework packaging](https://developer.apple.com/documentation/xcode/creating-a-multi-platform-binary-framework-bundle),
[Swift module maps](https://docs.swift.org/main/documentation/packagemanagerdocs/modulemaps/).
