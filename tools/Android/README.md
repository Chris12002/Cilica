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

The public session and unchanged real-kernel artifacts both cross-build and link
their C11 consumers. Android execution, repeated calls, JNI/Kotlin ownership and
physical-device tests remain open.

Tool choices follow Android guidance for [other build systems](https://developer.android.com/ndk/guides/other_build_systems),
[symbol visibility](https://developer.android.com/ndk/guides/symbol-visibility),
[C++ runtime linkage](https://developer.android.com/ndk/guides/cpp-support) and
[16 KB page alignment](https://developer.android.com/guide/practices/page-sizes).
