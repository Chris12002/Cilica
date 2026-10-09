# ADR AC0247: Bounded C++ translation units for validated object closures

Recorded 2026-10-09 for AC8.103 after a7d5a46.

## Problem and reference

The complete frozen EngineStep closure emits 467,018,483 bytes in one C++ file.
A cached local Clang 18.1.3 ARM64 compiler reaches its 180-second syntax-check
bound at 4 GiB without a diagnostic or an OOM kill. Raising the allowance does
not address the layout. The converted Unity reference separates Alchemy.Engine
across nine C++ files (for example Alchemy.Engine__2.cpp and __4.cpp include
pch-cpp.hpp). Its organization guides this change; Cilica still generates every
method and uses its own runtime. The converted capture has a different Alchemy
commit and is not full replay evidence.

## Decision

Add ObjectCppEmitter.EmitExceptionModule for an already validated scalar exception
closure. Existing single-file APIs and their output remain unchanged. The new
object-exceptions-module-v1 artifact provides one compiler-named header plus all
source files. Compile all sources with identical C++20/runtime/sanitizer settings,
then link their objects. A precompiled header is optional build machinery, not
part of the managed semantics or a required compiler dependency.

The shared header contains runtime definitions, descriptors, dispatch/delegate
registries and method declarations. Each method's frame, step functions and
exception routing stay together in one source file. All ordinary method bodies
have external linkage within a closure namespace with hidden visibility; the
existing experimental C boundary remains outside that namespace. This is a
compiler-library API, not public embedding SDK or multi-root discovery support.

Namespace-scope common definitions use C++ inline linkage so descriptors, mutable
token counters, predicates, delegate tables and runtime function-local static
resources each retain one shared identity. A small lexical pass processes only
Cilica-generated common definitions: it skips comments/quoted literals and tracks
braces, including declarations immediately following a closing brace. It leaves
method-local statics intact. Unexpected nested namespaces, raw string literals
or unbalanced closure structure fail emission. No arbitrary input C++ is parsed.
Method prototypes are emitted with their intended linkage directly.

A unit holds 1–256 complete methods (default 256) and at most 8 MiB of UTF-8
source including framing. The header is bounded at 64 MiB; at most 1,024 source
files include the boundary. An individually oversized method is rejected with
its managed name; methods are never cut mid-body. Existing semantic, discovery,
aggregate, root, heap, call-depth and native-stack limits are unchanged.

## Review and focused evidence

Reviewed ordering, root publication, allocation boundaries, exception origins,
borrow lifetime, frame storage, and shared descriptor/handle identity. Moving a
frame beside its method changes no generated execution step or cleanup. The
namespace visibility hides implementation symbols while preserving cross-file
calls. The inline runtime's token counters remain unique across instances and
translation units; no per-file registries are introduced. Validation still runs
through ObjectRootAssignment.AssignExceptions before any artifact is returned.
No outstanding findings in this slice.

The new integer fixture composes constructors, Int64 values, references, a frame
borrow, an instance delegate, static initialization and a caught exception across
one-method files. The legacy control wraps the established Composed exception
fixture; static-field and broader ArgumentException operations remain outside
that earlier profile. Initial attempts to use those operations were rejected,
and no legacy admission rules were changed. Together the two roots pass 1,176
repeated O0/O2 native/.NET comparisons with both normal and forced collection,
ASan/UBSan, a 2 KiB native-frame warning, exact/one-short execution quotas and
complete frame/borrow cleanup. Separate consumer files compare descriptor addresses
and interleave handle/borrow token issuance. Invalid partition arguments and
malformed detached IR are rejected. Seven new checks pass (37.743s).

Seventeen adjacent existing NativeExceptionTranslation, WideRootFrame and
TypeInitializationException checks also pass. The combined run initially had two
legacy-fixture failures; the corrected focused run clears both. No other checks
were rerun without a code/input change. Receipts: artifacts/alchemy-native-modules.

## Full closure evidence and remaining boundary

The initial layout prototype passes StateLookup O0/O2 with sanitizers and forced
collection. Its shared EngineStep header compiles in 9.591s; the partition containing
ResolveChain's largest root frame compiles at O0 in 10.187s, each within the same
2-CPU/4-GiB local container budget. These are engineering receipts, not a speed
comparison. The previous monolithic timeout, compiler preflight setup errors and
all disposable-container cleanup receipts are retained.

The fresh Alchemy run with the production module emitter reaches the existing
300-second bound during scalar validation, before entering module emission.
It reports no new semantic diagnostic. That receipt is retained at
artifacts/alchemy-module-emission; validation cost is the next investigation.
Full production module emission and the native compile/run remain pending.
The Alchemy compiler copy still uses diagnostic aggregate allowances. Production
application-capacity selection, the real command facade and complete saved-game
replay remain outstanding. A null-boundary probe cannot establish game equivalence.
