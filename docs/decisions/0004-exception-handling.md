# ADR 0004: Exception handling

Status: proposed. Recorded 2026-09-28.

Implementation note (2026-10-01): [ADR 0019](0019-primitive-pending-faults.md)
implements the bounded primitive foundation: a per-invocation pending-fault
context, explicit method-exit edges, may-fault analysis and a native status entry
for Int32 division. [ADR 0020](0020-checked-int32-arithmetic.md) adds checked
Int32 arithmetic on this same path. It has no managed exception objects/messages,
handlers or GC roots. This does not accept the full design or clear either filter stage's gate.

## Context

Managed exceptions must keep .NET's observable behaviour: exception identity,
handler selection, the order of `finally` blocks and filters, and the exception
types raised by runtime checks (specification section 9). Generated C++ must
also keep shadow-stack frames correct (ADR 0002), work identically on native
and WebAssembly targets, and meet the performance priorities in section 15.

Alchemy's offline scope already depends on exception behaviour:

- Three engine methods use exception filters, each a pure type test:
  `catch (Exception e) when (e is InvalidOperationException ||
  e is EffectiveSpellCompilationException || e is OverflowException)`.
- Exceptions occur during normal play, in spell validation, so throwing cannot
  be treated as rare.
- One handler copies `exception.Message` into a rejection returned to the
  player, so message text is part of game output.
- `OverflowException` is caught, so checked arithmetic must raise it exactly
  where .NET does.

There are two broad ways to propagate an exception through generated C++:

- **C++ exceptions.** No instructions on the normal path on native targets,
  but throwing is comparatively expensive. It needs unwind tables and cleanup
  landing pads for every frame guard. On WebAssembly, Emscripten's
  JavaScript-based exception mode adds overhead to ordinary calls. Native
  WebAssembly exception handling avoids that, but its proposal has been revised
  and toolchain support is still settling.
- **Explicit propagation.** A pending exception is recorded in the execution
  context, and generated code checks for it after each call that can throw.
  Each such call costs a load and a well-predicted branch, but throwing is
  cheap. It needs no unwind tables and behaves the same on every target.

ECMA-335 defines two-pass handling. The first pass searches outward for a
handler, running filters, before any `finally` block runs. The second pass then
unwinds to that handler, running `finally` and `fault` blocks. The ordering is
observable only when a filter runs code with side effects or reads state that a
`finally` block changes.

## Decision

### 1. Explicit propagation on every target

- Throwing stores the managed exception in the execution context's pending
  exception slot, which the collector traces, and control leaves through the
  IR's exceptional edges.
- After each call that can throw, generated code tests the pending slot and
  branches to the enclosing handler, or to the method's exit when there is
  none. Runtime checks, such as null, bounds, casts, overflow and division,
  branch directly to a cold throw helper and then to the handler.
- Generated functions return their values normally. The exception status lives
  in the context, not in the return value.
- The compiler, runtime and generated code build without C++ exceptions or
  RTTI, which also avoids their unwind tables and binary size.
- A whole-program "may throw" analysis omits checks after calls that provably
  cannot throw.

### 2. Exceptional control flow is explicit in the IR

Each instruction that can throw has an exceptional successor. How that edge is
lowered is confined to one code-generation point. Using C++ exceptions or
native WebAssembly exception handling on some target later would change that
point, not the IR or the managed semantics.

### 3. Protected regions and frames

- A method's `try` regions become labelled blocks. `finally` and `fault`
  blocks run on both normal and exceptional exits, following ECMA-335 rules for
  `leave`.
- Shadow-stack frames (ADR 0002) are popped at the method's single exit, so no
  unwinding machinery is involved.
- `throw` of a null reference raises `NullReferenceException`. `rethrow`
  preserves exception identity.

### 4. Exception filters, in stages

**Stage A: type-test filters.** A filter qualifies when its IL does only the
following: loads the exception object, tests its type (`isinst`), combines the
results with comparisons, constants and branches inside the filter, and
returns. The explorer conservatively classifies filters with control-flow
cycles as general: a nonterminating filter prevents unwinding, so moving it
after inner `finally` blocks would change observable behaviour. An acyclic
filter may branch backward; branch direction alone does not establish a cycle.
It calls nothing, reads no other state and cannot throw. Such a filter
has no side effects and depends only on the exception's runtime type, which
nothing can change. Evaluating it after inner `finally` blocks is therefore
unobservable. It is lowered as a catch that tests the condition and continues
propagation with the same exception when the test fails. Stage A lands with
basic exception support, because Alchemy's filters need it.

**Stage B: general filters with full two-pass semantics.** Required for the
first public release, or before milestone 6 if the dependency audit finds
general filters in the ported library closure required by Alchemy:

- Frames record their active protected region in a cheap per-frame state
  value, updated when entering and leaving `try` blocks.
- A method containing general filters keeps the locals its filters read in its
  frame. Each filter is compiled as a separate function that receives that
  frame.
- When an exception is thrown while any general-filter region is active, the
  runtime first walks the frames outward and evaluates filters in order,
  without running `finally` blocks. It then propagates to the chosen handler,
  running `finally` and `fault` blocks on the way.
- An exception thrown inside a filter is discarded, and the filter is treated
  as returning false, as ECMA-335 requires.
- Code with no active general filter never pays for the first pass.

Until stage B, a filter that does not qualify for stage A produces a diagnostic
naming the filter and its reachability chain.

### 5. Runtime-raised exceptions

- Null dereference, bounds, invalid cast, array store type mismatch, checked
  overflow, division by zero and type-initialization failure raise the same
  exception types as .NET, at the same IL instructions.
- Messages come from the ported resource strings (ADR 0003), so message text
  matches the pinned .NET release. Builds validating game output must not
  enable the option that replaces messages with resource keys.
- Each runtime instance preallocates an `OutOfMemoryException`, so an
  allocation failure can still be reported.
- Stack trace text is limited initially and excluded from differential
  comparison. Its IL-offset mapping follows the specification's
  method-mapping requirement.

### 6. Export boundaries and fatal errors

- Every export wrapper is a catch-all handler. A managed exception becomes a
  structured error containing the exception type, message and error code, and
  the pending exception is cleared. Because every call has this handler, every
  `finally` block runs before the error reaches the host. This defines
  behaviour that .NET leaves to process termination for unhandled exceptions.
- An error returned by a host callback is raised in managed code as a
  documented Cilica exception type.
- Stack overflow is fatal in .NET. Cilica does not terminate the host process.
  Functions in recursive call cycles, and functions with large frames, compare
  the stack position against a limit held in the context on entry. Exceeding
  it, like detected heap corruption, marks the runtime instance as failed: the
  current call returns a fatal error and later calls to that instance are
  rejected.

## Consequences

- Native and WebAssembly builds use the same mechanism and generated code, so
  exception behaviour is tested once for every target. No WebAssembly
  exception-handling proposal or browser feature is required.
- Throwing is cheap, which suits Alchemy's validation paths.
- Every call that can throw gains a load and a branch. This affects
  steady-state throughput and code size, priorities 5 and 3. The "may throw"
  analysis reduces it but does not remove it.
- Building without C++ exceptions and RTTI removes unwind tables and
  simplifies frame handling. Runtime C++ code must avoid throwing library
  facilities and report allocation failure explicitly.
- Alchemy's filters are supported at the same milestone as basic exceptions.
  General filters are a larger piece of work before the first public release.
- The ported library may reach general filters itself. The milestone 1 audit
  reports filters in the reachable library closure, which may bring stage B
  forward.
- Exception messages become part of observable output and must track the
  pinned release's resources.

## Alternatives considered

- **C++ exceptions on every target.** No cost on the normal path natively, but
  expensive throws, unwind tables and landing pads for every frame guard. On
  WebAssembly it depends on JavaScript-based exceptions, with overhead on
  ordinary calls, or on native WebAssembly exception handling, whose proposal
  and toolchain support are still settling. Kept possible through decision 2
  if measurements favour it on a target.
- **`setjmp`/`longjmp`.** Rejected: cost on every protected region, poor
  optimisation around it, and awkward WebAssembly support.
- **Returning a status with every value** in a two-register result. It avoids
  reloading the context, but complicates signatures and depends on each ABI's
  multi-value returns. A candidate for a later measured optimisation.
- **Rejecting all filters until two-pass support exists.** Rejected: it blocks
  the offline Alchemy slice.
- **Lowering every filter as catch-and-rethrow.** Rejected: filters with side
  effects, or that read state changed by `finally` blocks, would observe the
  wrong order.

## Validation

- Differential tests against .NET cover `finally` ordering, nested and
  rethrown exceptions, exception identity, runtime-raised exception types and
  messages, type-test filters and, in stage B, filters with side effects
  interleaved with `finally` blocks.
- GC stress runs with exceptions thrown across frames that hold roots.
- Before acceptance, the ADR 0002 validation experiment also measures explicit
  propagation against C++ exceptions natively and native WebAssembly exception
  handling: normal-path throughput on a call-heavy example, throw cost and
  binary size.
