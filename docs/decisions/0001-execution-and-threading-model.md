# ADR 0001: Execution, async and threading model

Status: proposed. Recorded 2026-09-28.

## Context

Cilica is intended as a general open-source compiler, not only a runtime for
one game. Modern .NET libraries, including those marked NativeAOT-compatible,
use `async`/`await`, `lock`, `Interlocked` and threads freely. A runtime that
cannot eventually support them would limit adoption.

Building a multithreaded runtime first would roughly double the correctness
surface before anything runs: multithreaded collection, the ECMA-335 memory
model, thread-safe type initialization and runtime tables, and concurrency
testing. On the web, shared-memory WebAssembly threads also require
`SharedArrayBuffer` and cross-origin isolation headers.

`async`/`await` does not require threads. The C# compiler lowers it to a state
machine whose continuations can run on a single-threaded event loop, the same
model JavaScript uses.

Cilica compiles a closed world ahead of time, so the execution model can be
chosen per build and each build can pay only for what it uses.

## Decision

### Staged execution profiles

The execution profile is a build-time option. A build compiles all its
assemblies for one profile, and a runtime instance runs only code built for
its profile.

| Stage | Profile | Scope | Planned timing |
| --- | --- | --- | --- |
| 1 | Single-threaded | One managed thread per instance | First runtime milestones |
| 2 | Single-threaded async | Stage 1 plus `Task`-based async on an event loop | Required for the first public release |
| 3 | Multithreaded | Managed threads sharing one heap | After the first public release |

Stages 1 and 2 remain permanent profiles, not transitional ones. They have
no atomics on ordinary field access and no thread coordination, and execution
is deterministic, which suits games and simulations.

### Stage 1: single-threaded

1. **One managed thread per runtime instance.** Managed code never runs
   concurrently within an instance. The embedding API serializes host calls
   onto the instance's worker (section 12).

2. **Parallelism uses independent instances.** In every profile, hosts may
   create several runtime instances on separate threads. Instances share no
   managed heap. Data crosses between them as serialized values, never as
   object references. A handle is valid only in its own instance; using it
   with another instance is rejected like a stale handle. On the web, each
   instance uses its own WebAssembly module instance in a Web Worker, so this
   does not require shared memory.

3. **Threading APIs have single-thread semantics.**
   - `lock`/`Monitor.Enter`/`Exit` track reentrant ownership and never contend.
   - `Interlocked`, `Volatile` and `volatile` fields compile to real atomic
     operations in every profile. On ARM64 an uncontended atomic operation is
     cheap, and the generated code stays correct when stage 3 arrives.
   - `[ThreadStatic]` fields have one value per thread; in stage 1 there is one
     thread.
   - `Lazy<T>` thread-safety modes are satisfied trivially.
   - `Monitor.Wait`/`Pulse`, thread creation, `ThreadPool`, `Task.Run` and
     `Parallel` produce build diagnostics stating that they require the
     multithreaded profile.

### Stage 2: single-threaded async

4. **Async runs on a per-instance event loop.** `Task`, `Task<T>`,
   `ValueTask`, `TaskCompletionSource`, the awaiter pattern and cancellation
   run on a continuation queue drained by the instance's worker, which is the
   instance's `SynchronizationContext`.
5. **Timers come from the host.** `Task.Delay` and timers ask the host to
   schedule a wake-up through the embedding API. The runtime never blocks its
   worker to wait.
6. **Exported async methods become host promises.** An exported method
   returning `Task` or `Task<T>` returns a host promise that settles when the
   task completes, with managed exceptions delivered as structured errors.
7. **Blocking on an incomplete task raises a documented exception** in
   single-threaded profiles (`Wait`, `Result`, `GetAwaiter().GetResult()`).
   No other thread could complete the task, so waiting would hang forever. This
   is a documented difference from .NET, where a thread-pool thread may complete
   the task. The behaviour of `ConfigureAwait(false)` and custom task
   schedulers will be decided when stage 2 is designed.

### Stage 3 readiness: commitments made now

Stage 3 is not implemented in the first release. To keep it an addition rather
than a rewrite, stages 1 and 2 follow these rules from the start:

8. **Per-thread execution context.** Every generated managed function takes an
   execution-context pointer as its first parameter. The context represents a
   managed thread and points to its runtime instance. It locates the shadow
   stack, allocation state, pending exception and thread-static storage. In
   stages 1 and 2 each instance has one thread context.

9. **Thread statics are reached through the thread context**, not stored with
   ordinary statics, so stage 3 needs no change to generated access.

10. **Explicit IR nodes for thread coordination** (ADR 0002 gives the collector
    details):
    - Poll points on loop back-edges and method entries, where a thread checks
      whether the collector needs it to stop.
    - Transitions around blocking native calls and host callbacks, where the
      thread tells the collector it is running native code.
    - Stores of managed references into the heap, where write barriers can
      later be inserted.

    In single-threaded profiles, polls and transitions compile to nothing.

11. **The object header reserves a synchronization word** for identity hash
    codes and lock state. Stage 1 uses it for reentrant lock ownership;
    stage 3 extends it to contended locks without changing object layout.

12. **Type initialization records the initializing thread.** The static
    constructor state (not started, running with owner thread, complete,
    failed) follows ECMA-335 rules. It is designed so that stage 3 can make
    other threads wait for completion and apply the CLI rules for circular
    initialization.

13. **Field-access lowering is chosen per profile.** In .NET, unsynchronized
    concurrent access to a field is legal, although it can observe stale
    values. In C++, the same race is undefined behaviour. The code generator
    therefore lowers ordinary field and array-element access through one
    point:
    - Single-threaded profiles use ordinary loads and stores.
    - The multithreaded profile uses relaxed atomic access (`std::atomic_ref`
      with `memory_order_relaxed`). On ARM64 this compiles to ordinary loads
      and stores but restricts some optimizations.

14. **Runtime services are separated by scope.**
    - Process-wide native services, such as the page allocator, are
      thread-safe from the start.
    - Instance-wide runtime structures, such as the handle table, interned
      strings and type-initialization state, are reached through functions
      that can take a lock in stage 3.
    - The runtime keeps no process-global mutable managed state. Immutable
      compiled data, such as type descriptors and method code, may be shared
      across instances. Managed objects, including interned strings and
      `System.Type` instances, belong to one instance.

## Consequences

- The first runtime needs no multithreaded collection, memory-model
  fences on ordinary fields or concurrency testing.
- The first public release supports `async`/`await`, which many libraries and
  host integrations need. Libraries that start threads fail compilation with a
  diagnostic naming the multithreaded profile, not a vague unsupported error.
- Single-threaded builds stay the fastest and deterministic option
  permanently. The multithreaded profile will be slower on single-threaded
  work because of polls, atomic field access and barriers.
- The context parameter costs one argument per call and avoids thread-local
  storage lookups.
- The readiness commitments add a small amount of design work to the IR, object
  header and type initialization before stage 3 is needed.
- Stage 3 remains a large milestone: stop-the-world coordination, contended
  locks, thread pool, `Monitor.Wait`/`Pulse`, memory-model conformance and
  concurrency stress testing. On the web it requires cross-origin isolation, so
  web hosts without it use the single-threaded profiles.

## Alternatives considered

- **Build the multithreaded runtime first.** Rejected: it delays every other
  milestone and makes early correctness much harder to establish.
- **Single-threaded only, revisiting if a workload needs threads.** Rejected:
  retrofitting per-thread contexts, poll points, header layout and field
  lowering later would touch every part of the compiler and runtime.
- **One profile with atomic field access everywhere.** Rejected: it taxes
  single-threaded users for a capability they do not use.
- **Mapping `Task.Run` onto the single worker in single-threaded profiles.**
  Rejected: code expecting parallel progress or blocking on the result could
  deadlock or change behaviour silently.
- **Pumping continuations inside blocking waits.** Rejected: continuations
  would run in the middle of the caller's frame, causing ordering bugs.

## Validation and revisit triggers

- Differential tests cover `lock` reentrancy, `Interlocked` return values,
  `[ThreadStatic]` behaviour and, in stage 2, async continuation ordering and
  exception propagation against .NET.
- Readiness check: before the first public release, a design review confirms
  commitments 8 to 14 are implemented and that stage 3 needs no changes to
  generated function signatures, object layout or IR structure.
- Stage 3 validation adds concurrency stress tests, ThreadSanitizer runs on
  generated C++ and differential results for race-free programs.
- The dependency audit confirms which threading and async members the kernel
  and Alchemy reach, to order stage 2 library work.
