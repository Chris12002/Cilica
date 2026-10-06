# Android arm64 native package

Build one emitted v3 module with the already-installed, pinned NDK:

```bash
python3 -B tools/Android/build_library.py \
  --source out/engine.cpp --header out/engine.h --manifest out/engine.json \
  --ndk /path/to/android-sdk/ndk/27.1.12297006 \
  --module CilicaEngine --output out/android-engine
```

The output must be new. Source/header/manifest validation is shared with the Apple
builder. NDK revision and Clang identity are checked before creating build products.
The script never downloads tools, runs adb/emulators, installs apps or executes
the resulting code. This is a native package, not an AAR, APK, JNI wrapper or Kotlin
adapter. Building does not establish Android execution compatibility.

The pin is NDK r27b (`27.1.12297006`), Clang 18.0.2, macOS host tools, arm64-v8a
and API 26. The NDK's `darwin-x86_64` folder is its official macOS host tag; it does
not imply an x86 Android output. API 26 is a build floor, not a tested OS version.
Other ABIs/hosts are outside this checkpoint. No Gradle or Java tools are required.

The package contains:

- `lib/arm64-v8a/lib<Module>.so`, with a matching SONAME.
- `include/cilica.h` and the unchanged `contract.json`.
- The pinned NDK's `NOTICE.toolchain`, retained with the native package.
- `build-receipt.json` with hashes, tool identity, target, commands, exact exports,
  dependencies and ELF load alignment.

Objects, `exports.map`, ELF inspection output, logs and `checks/lifecycle` remain
for inspection. The latter is a separate C11 consumer covering layout and six
create/destroy cycles, but the builder **only links it**. Its presence is not a
passing execution test. Future execution must arrange for Android's loader to
find the matching shared library.

Generated code uses C++20, O2, PIC, hidden visibility, no C++ exceptions/RTTI and
the existing 2 KiB generated-frame warning gate. libc++ is linked statically and
kept private by a version script exposing exactly the emitted C declarations.
Do not exchange C++ objects, exceptions or allocator ownership with other libraries.
The C ABI's host-owned buffers and explicit leases supply that isolation; runtime
allocations are freed inside the same library. Do not mix implementation symbols
or independently linked copies of the same module/registry.

The builder verifies little-endian ELF64 AArch64, SONAME, exact dynamic exports,
system-only dependencies, no text relocations, non-executable stack, no writable
executable load segments and 16 KB-compatible load alignment. Explicit maximum/
common page-size linker flags support the r27 pin. ELF alignment does not prove
16 KB device execution or final APK zip alignment. Each host app still needs
packaging and runtime validation.

Local checks:

```bash
python3 -B -m unittest discover -s tools/Android -p 'test_*.py' -v
python3 -B -m unittest discover -s tools/Apple -p 'test_*.py' -v
```

## Native emulator execution

The runner uses already-installed tools pinned in `emulator.json`: emulator
36.3.10, adb 36.0.2-14143358 and the Android 16/API-36 Google Play arm64 image,
revision 7. It creates a fresh temporary AVD and private adb server, uses only its
own emulator serial, and removes both processes and temporary device data after
success or failure. It does not use personal AVD data or shared adb services.
Logs, case output, device configuration and `run-receipt.json` remain in a new
output directory. Boot is bounded to approximately 180 seconds; guest execution
to 60 seconds per consumer. This harness owns disposable test subprocesses.

Run one or more existing package lifecycle consumers in one boot:

```bash
python3 -B tools/Android/run_emulator.py \
  --sdk /path/to/android-sdk --output out/android-run \
  --case out/android-engine lifecycle
```

To link a separate C11 consumer, include `cilica.h`. Optional template tokens
`@PREFIX@` and `@HASH@` expand to the package's C function prefix and contract-hash
macro. The explicit NDK must match the package and toolchain pin:

```bash
python3 -B tools/Android/build_consumer.py \
  --package out/android-engine --source tests/consumer.c.in \
  --ndk /path/to/android-sdk/ndk/27.1.12297006 --output out/android-consumer
python3 -B tools/Android/run_emulator.py \
  --sdk /path/to/android-sdk --output out/android-custom-run \
  --case out/android-engine lifecycle \
  --case out/android-engine out/android-consumer
```

The runner verifies package/binary hashes and custom consumer source/package
association before launch. Each consumer must exit zero and print a line beginning
with `PASS` followed by whitespace or end of line. Use assertions/checks in that
consumer for the actual expected values; a PASS marker alone is not an oracle.
Libraries are pushed beside their consumers and found through a case-local
`LD_LIBRARY_PATH`. Receipts link results to binaries and record guest page size.

Both public-session and real-kernel packages pass six lifecycle cycles each.
The unchanged kernel additionally passes 5,376 .NET transcript comparisons across
three normal and three forced-collection runs on arm64 API 36 with **4 KB pages**.
The original oracle and libraries were reused. This does not establish API-26 or
16-KB runtime compatibility, Android captured-fault copying, JNI/Kotlin ownership,
APK packaging, application lifecycle or physical-device behavior.

Tool choices follow Android guidance for [other build systems](https://developer.android.com/ndk/guides/other_build_systems),
[symbol visibility](https://developer.android.com/ndk/guides/symbol-visibility),
[C++ runtime linkage](https://developer.android.com/ndk/guides/cpp-support) and
[16 KB page alignment](https://developer.android.com/guide/practices/page-sizes).
