# ADR 0191: Android shared packaging

Accepted 2026-10-06; milestone 7e.1. Cross-build/layout/link evidence only.

## Context

iOS has a bounded packaging and simulator checkpoint. Android needs an equally
explicit toolchain and library boundary before introducing JNI/Kotlin ownership
or making device claims. NDK r27b is already installed locally; no tool download
is required. The emitted v3 C boundary already keeps buffers and retained object
ownership explicit.

## Decision

Pin NDK `27.1.12297006`, its Clang 18.0.2 identity and macOS tool directory. Build
arm64-v8a with target `aarch64-linux-android26`. API 26 is a chosen build floor,
not tested OS compatibility. Keep other ABIs/hosts out of the initial profile.
Validate source/header/manifest consistency before building, sharing the existing
Apple validator without changing its checks. Reject existing output directories
and toolchain mismatches before creating build products.

Produce one shared library with a module-specific SONAME and its unchanged header
and manifest. Compile generated code as C++20/O2/PIC, with exceptions/RTTI disabled,
hidden visibility and the existing 2 KiB generated-frame warning gate. Statically
link libc++ behind an anonymous export map containing only the emitted C functions;
verify exact dynamic exports afterward. This avoids introducing a shared libc++
version into a future host app. C++ values, exceptions and allocation ownership
must never cross the boundary; all runtime allocations are freed internally and
host buffers remain caller-owned. Include the NDK's toolchain notice file.

Require ELF64 little-endian AArch64, the expected SONAME, system-only dynamic
dependencies, no text relocations, a non-executable stack and no writable
executable load segment. Pass r27's explicit 16 KB maximum/common page-size flags
and inspect every load segment's alignment and offset/address congruence. ELF
alignment is independent of APK zip alignment and actual 16 KB runtime behavior.

Compile a separate C11 lifecycle consumer and link it against the shared library.
The builder never executes it, invokes adb, launches an emulator, downloads tools
or installs/signs an app. Receipts retain hashes, commands and inspection evidence.
This profile is a native package, not an AAR or Java/Kotlin interface. Public JNI
exports and managed host lifetime need a later design rather than being inferred
from successful linkage.

## Evidence and review

Both the public session and unchanged real-kernel artifacts cross-build with the
pin and link their separate C11 consumers. All generated-code frame gates pass.
The session exposes exactly 12 C symbols, the kernel 10; both have four 16 KB-aligned
load segments and depend only on `libm.so`, `libdl.so` and `libc.so`. Neither has a
shared libc++ dependency or exposed C++ implementation symbol. No execution is
claimed from these checks.

Six focused Python tests pass: three Android checks for ELF alignment/permissions,
export scoping and preflight/output protection; three existing Apple checks after
extracting the shared artifact validator. Review covered C ownership, private
static-runtime isolation, linker exports/dependencies, page alignment, tool pins,
bounded subprocesses, provenance and preservation of Apple validation. No
unresolved defect remains in this packaging scope. Private evidence is
`artifacts/milestone7-android/evidence-7e1.json`.

No native compiler semantics or kernel rules changed. No Android code ran, no
emulator/device was contacted and no hosted work, signing, download or push ran.
Android loading/repeated calls, JNI/Kotlin lifetime, physical devices and final
application packaging remain open.

The build follows Android guidance for [external build systems](https://developer.android.com/ndk/guides/other_build_systems),
[private symbols](https://developer.android.com/ndk/guides/symbol-visibility),
[C++ runtime choices](https://developer.android.com/ndk/guides/cpp-support) and
[16 KB alignment flags](https://developer.android.com/guide/practices/page-sizes).
