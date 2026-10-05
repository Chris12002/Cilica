# ADR 0178: Persistent scalar C ABI foundation

Accepted 2026-10-06 for milestone 7a. Mobile/device compatibility remains unproven.

## Decision

Add a separate compiler-library `EmbeddingCppEmitter` that wraps an already
validated object or managed-exception scalar program. Emit a C header and C++
implementation with versioned create/invoke/destroy/version functions. Limit the
initial boundary to one Int32 export; retain the existing backend and experimental
per-call entry unchanged. Keep the versioned contract in
[embedding-abi.md](../embedding-abi.md).

Each runtime owns the existing `Instance`, so static fields, initializer outcomes,
literal caches and their collector roots persist across calls. Each call owns a
fresh `Context` and bounded argument packet. Release the admission guard only
after context/arena cleanup. No C++ runtime layout, managed pointer or borrowed
fault text is exposed to C. Fault origins are copied into caller storage.

Use a bounded 64-slot registry with monotonically issued nonzero UInt64 tokens.
Do not wrap tokens or reinterpret caller integers as native pointers. A small
atomic registry lock protects admission/removal; per-slot busy state excludes
overlapping managed calls and destruction. Allocate, execute managed code and
destroy heaps outside that lock, allowing independent instances to progress.
Tokens are scoped to the emitted closure/library and must not cross artifacts.

## Why this slice

Directly exposing the internal `Instance*` would couple mobile adapters to C++
layout and permit stale pointers to become dangling references. Reusing the
experimental per-call entry would discard state on each invocation. A copied
scalar boundary establishes lifetime and fault behavior before adding object and
serialization ownership. Reusing the existing emitter/collector avoids a second
runtime implementation. A common multi-export registry is a subsequent design
step; independently emitted roots currently have separate static/descriptor worlds.

The header uses fixed-width integer fields/status macros and C linkage with explicit
symbol visibility. Consumers use their platform C layout, never packed wire layout.
Compilation with hidden default visibility keeps the older experimental symbols
and runtime internals out of the shared library's public surface.

## Validation and limits

Separate C11 consumer/shared-library linkage at O0/O2 uses ASan/UBSan and disabled
C++ exceptions/RTTI. Stateful .NET comparisons cover normally collected and forced-GC
instances, retained statics, uncaught/caught faults and later recovery. Lifecycle
tests cover invalid versions/flags/arguments/tokens, capacity, slot reuse and copied
origin lifetime; concurrent callers and invoke/destroy races exercise admission.

No export manifest, cross-export instance, external object handle, buffer release,
structured value transfer, exception message API, mobile build, Swift/Kotlin adapter
or physical-device evidence is supplied by this slice. iOS is the first subsequent
phone target. Hosted work and publication remain disabled.
