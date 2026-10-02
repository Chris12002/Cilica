# ADR 0027: Budgeted execution frames and bounded native helpers

Status: accepted for the bounded object backend. Recorded 2026-10-02.

Follow-up: [ADR 0028](0028-packed-boundaries-and-arena-diagnostics.md) supersedes
the individual-parameter boundary in decision 6, adds arena lifetime diagnostics
and records constrained whole-call-chain execution. Measurements below describe
the original checkpoint.

[ADR 0029](0029-measured-helper-batching.md) later measures and rejects a larger
ordinary-statement batch after native-stack checks catch a regression.

## Context

The 64-call limit controls invocation count, not the native stack consumed by
root arrays, locals, temporaries, block inputs or outgoing arguments. Finite
recursion through a wide method can exhaust the host stack before that limit.
Moving only roots leaves integer storage and compiler-generated spills unbounded
with respect to method size. The separate primitive backend is outside this slice.

## Decision

1. `object-frames-native-v1` extends the object control-flow profile. A separate
   `ObjectFrameLayout` assigns distinct integer slots alongside phase A reference
   slots. Include all parameters, locals, block inputs and values, reusable
   outgoing argument storage sized for the largest call (including constructor
   receivers), checked-arithmetic scratch, result storage and root metadata.
   Native C++ `sizeof`/`alignof` supply the target's complete padded byte layout;
   do not assume host pointer widths in the compiler.

2. Each `Context` owns a non-moving, lazily allocated execution arena. The
   default budget is 1 MiB, separate from the 1 MiB retained-object budget.
   Private diagnostic callers can choose its size at context construction;
   the experimental C boundary uses the default. A typed reservation checks
   capacity and alignment by subtraction before addition. It starts the exact
   C++ object's lifetime in place, without a frame-sized native temporary.
   Account for padding, empty-array representation, metadata and the boundary
   argument packet. Arena allocator overhead and native stack bytes are outside
   this execution-storage budget. Copying contexts/reservations is prohibited.

3. Keep the 64-active-call limit. Internal calls pass a context and a pointer to
   caller-owned argument storage. Evaluate arguments in IL order, perform any
   required callvirt null check, then check callee depth and reserve its frame
   before executing its managed body. Failed reservation does not alter cursor,
   depth or roots. Depth takes precedence over storage exhaustion at entry.
   Status 9 identifies execution-budget exhaustion or failure to allocate its
   backing arena; status 8 remains depth exhaustion, and 5 remains object
   allocation failure. Entry resource faults identify the callee and its first
   block's IL offset; boundary packet failures identify the exported method.
   Earlier arithmetic/null faults retain their origins and precedence.

4. A scope guard unlinks roots before a reservation destroys its typed storage
   and restores the cursor. Parameters copy into zero-initialized slots before
   any managed allocation. Reference arguments remain in caller roots throughout
   transfer. Returned references are still present in callee value roots, and
   the result is copied immediately into a caller root after native return.
   Reservation/release/dispatch are not GC safepoints; no allocation or observer
   can run during that handoff. New objects publish before constructor entry.
   Block input slots remain distinct from source values, preserving parallel
   transfers even when edge copies are split across helpers.

5. Emit small, non-inlined native step helpers and a method-local dispatch loop.
   Helpers contain at most two ordinary IR statements; argument/parameter/edge
   copies flush in batches of four (plus an initial constructor receiver), with
   separate checks and transitions. A step
   returns the next helper index or a completion sentinel; it never recursively
   calls the next step. Calls to another managed method still use native calls,
   so the depth limit remains necessary. Native helpers share the same arena
   frame and source/IL identity. This is direct generated C++ for each program,
   with no runtime IL reader or replacement managed runtime.

   This additional split is required by measurement: the initial arena-only
   prototype still produced a 2,166,080-byte native frame for 4,096 checked
   additions under Apple Clang 17 O0 with ASan/UBSan. Even sequential calls in
   a single large function accumulate O0 spill slots. `gnu::noinline` prevents
   optimization from combining bounded steps back into a giant native body.
   Small dispatch steps trade throughput and code size for a verified baseline;
   larger chunks, slot reuse and dispatch optimization need new measurements.

6. Keep the experimental Int32 C boundary and its output contract. It reserves
   its argument packet in the execution arena. Its fixed native parameter list
   remains governed by the existing parameter bound; large exported signatures
   need their own target stack measurements. Internal managed calls do not use
   that wide native convention. No universal host-stack guarantee is claimed.

## Validation and review

Local O0/O2 ASan/UBSan probes cover finite and mutual recursion, wide methods,
exact/one-byte-short/zero execution budgets, exact/excess depth, cleanup followed
by another call, alignment padding, overflow-safe accounting, many arguments,
reference roots during allocation and reservation failures, and arithmetic/null
precedence over callee resource failure. Existing native object/control-flow
oracles continue comparing results and fault origins with .NET.

The reduced wide regression uses 1,024 checked additions, 2,048 locals and up to
64 active calls, compared with an authored .NET DynamicMethod. Both instrumented
builds reject native frames exceeding 2 KiB. Separate non-instrumented `.su`
reports must identify static stack use within that threshold; ASan fake-stack
and realignment paths can mark otherwise fixed source frames as dynamic.
On macOS ARM64, Apple Clang 17 reports maxima of 272 bytes at O0 and 208 at O2
for that measured translation unit, including the small test adapters. These are
per-function measurements, not whole-call-chain or cross-target guarantees.

Review must include native helper/dispatcher chains, allocator/library calls,
export arity, sanitizer settings and host stack headroom before claiming another
target or raising depth. Compiler/root/IR work limits remain in place. Collection,
async suspension, exception handlers, moving roots and mobile/WebAssembly
compatibility remain unimplemented. The arena is runtime storage, not a managed
object allocation, and the retained heap still does not collect.

Final validation: build succeeds with zero warnings/errors. All 416 targeted
Native/Object/Cpp/Int32 cases pass in 39.6 seconds using the xUnit in-process
runner, including primitive emitter regressions after sharing slot-aware
arithmetic lowering. No hosted jobs, services or additional agents were used.
