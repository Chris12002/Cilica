# ADR 0015: First Int32 C++ emission and native evidence

Status: accepted for slice 2b. Recorded 2026-10-01.

## Context

[ADR 0014](0014-straight-line-int32-ir.md) establishes typed operands and explicit
wrapping arithmetic. A test-only evaluator cannot prove native code generation.
The next checkpoint must run an authored C# method through our own backend and
an existing native compiler, while keeping compiler inputs inert.

## Decision

`CppEmitter.Emit` validates detached public IR before returning a `CppArtifact`.
Require one block zero, Int32 signature, consecutive value IDs, strictly increasing
nonnegative IL offsets, valid parameter indices and earlier binary operands,
known expression/operation kinds and a final return of an earlier value. Bound
values to 65,536 and parameters to 4,096. Reject default/missing arrays and nodes.
Invalid caller IR throws `ArgumentException`; it is an API contract violation,
not an unsupported-assembly diagnostic. Unexpected translator/emitter disagreement
remains a compiler defect, reported by the CLI's internal-error boundary.

Emit one C++20 `extern "C"` function returning `std::int32_t`, with Int32
parameters and `noexcept`. Its symbol is `cilica_m_<full-sha256>_<MethodDef-token>`.
Validate hexadecimal provenance before forming the symbol. Retain detached source
identity in the artifact; include only hash/token and numeric IL locations in
source comments. Never insert input names, identities or paths into C++ syntax.
Render numbers invariantly and use LF line endings. This is a primitive entry
convention, not the later runtime's exported embedding ABI.

Temporaries hold `std::uint32_t` bits. Constants use fixed hexadecimal literals;
parameters and return values use `std::bit_cast` between signed/unsigned 32-bit
types. Widen both operands to `std::uint64_t` before addition, subtraction or
multiplication, then convert to UInt32 to retain low bits. Products of two UInt32
values fit UInt64; subtraction wraps unsigned before narrowing. Widening prevents
native integer promotions from introducing signed overflow. No signed native
arithmetic or out-of-range signed numeric conversion implements managed wrapping.
Ordered temporaries preserve the IR evaluation order.

`cilica emit-cpp` selects exactly one MethodDef token and uses explicit reference
directories. It emits source to stdout or a requested file. Unsupported/malformed
input produces stderr diagnostics and no source; existing output is preserved.
Emission never starts a compiler or executes an input assembly.

The disposable native harness lives only in tests. It compiles/runs authored
fixtures through argument-list subprocesses, with time/output bounds and cleanup
of its own process handles. `CILICA_CXX` selects an executable, not shell flags.
Compile twice: `-O0`, and `-O2` with undefined-behaviour sanitizer. Keep builds
local and fail visibly for missing prerequisites; hosted CI stays disabled.

## Evidence and review

Eight authored C# methods cover Add, Subtract, Multiply, a compound expression,
minimum/maximum/negative constants and unused parameters. For each build, 100
boundary combinations plus 256 seeded tuples produce 2,848 native results,
compared directly with their .NET reference calls. The local SDK/runtime and
Apple Clang version/ARM64 target are recorded in the compatibility manifest.
Both builds pass, including sanitizer checks. Timeout and captured-output overflow
terminate owned processes and allow directory cleanup. Reduced tests reject forged
IR, check culture/injection boundaries, and preserve CLI output on failure.

Review covered operand order, arithmetic promotion/overflow/conversion, source
identity and deterministic rendering, no partial source, and subprocess ownership.
This profile has no allocations, managed references or native ownership transfer;
GC rooting and embedding ownership are not yet implemented or validated.

## Consequences and alternatives

The first C# → IR → C++ → native path now runs. Locals, branches, calls, exceptions,
the managed runtime and other platforms remain later work. Add typed control flow
in slice 2c without broadening arithmetic semantics silently.

Rejected signed C++ arithmetic because native overflow is undefined. Rejected
metadata-derived symbols/comments because input names are untrusted and are not
identities. Rejected automatically executing arbitrary CLI inputs because parsing
and generation must remain data-only. The production backend contains no fallback
interpreter or assembly loader.
