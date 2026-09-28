# ADR 0002: Garbage-collection root tracking

Status: proposed. Recorded 2026-09-28.

## Context

The baseline collector is precise and non-moving, and collects at controlled
safepoints (section 8). The collector must find every managed reference held by
executing code. How those references are recorded largely sets the performance
ceiling of generated code.

- NativeAOT's code generator emits stack maps: tables describing which
  registers and stack slots hold references at each safepoint. They cost almost
  nothing at run time and let references stay in registers.
- Portable C++ cannot express stack maps. LLVM's statepoint mechanism exists
  below the C++ level and would bind Cilica to one compiler's internals.
- WebAssembly's execution stack cannot be inspected, so references held in
  WebAssembly locals are invisible to a stack scanner.
- A shadow stack stores references in memory frames the collector can walk.
  It works everywhere, but forces stores and reloads around calls, blocks
  register allocation for those values and adds a frame push and pop.
- C++ leaves argument evaluation order unspecified, so values held only inside
  a nested C++ expression cannot be rooted reliably.

## Decision

1. **Precise roots via an explicit shadow stack on every target.** The
   execution context (ADR 0001) holds the shadow-stack top, so no thread-local
   storage lookup is needed. Each generated function that needs roots declares
   a frame on the C++ stack containing a fixed array of root slots, a slot
   count or descriptor and a link to the previous frame. A scope guard pops the
   frame on every exit path, so unwinding stays correct under whichever
   exception lowering is chosen.

2. **Code generation materializes values in named temporaries.** Each IL
   operation that produces a managed reference writes it to a named temporary,
   never leaving it only inside a nested C++ expression. This also preserves
   managed evaluation and exception order.

3. **Phase A, correctness baseline.** Every reference-typed local, IL
   temporary and argument the callee uses is stored in a root slot. The callee
   roots its own arguments on entry. Reference-bearing struct locals are
   registered with their type's reference map. Phase A must pass the
   conformance and GC stress suites before Phase B begins.

4. **Phase B, liveness-based rooting.** Only references live across a
   safepoint receive slots; slots are reused between non-overlapping live
   ranges, and functions with no such references get no frame. Safepoints are
   allocations and calls that may collect. In the closed world, an
   interprocedural "may collect" analysis classifies calls; unknown targets are
   treated as safepoints.

5. **The IR is built for Phase B from the start.** Every IR value keeps its
   reference kind (object reference, interior reference, native pointer, value
   type with references). Safepoints are explicit IR nodes. Root-slot assignment
   is a separate IR pass, not a code-generator side effect, and can be switched
   between Phase A and Phase B for differential testing.

6. **Interior references use a reserved slot kind.** How an interior
   reference resolves to its owning object is left to a separate ADR. The frame
   format reserves a slot kind for interior references so that decision does
   not change the frame layout.

7. **Rooting is ready for the multithreaded profile.** ADR 0001 stages
   multithreading for later, so root tracking follows these rules from the
   start:
   - Each thread context owns its shadow stack. A future stop-the-world
     collection walks the shadow stacks of every thread in the instance.
   - Poll points and native transitions are explicit IR nodes. In the
     multithreaded profile they are safepoints, so Phase B must keep references
     live across them in slots. In single-threaded profiles they compile to
     nothing and are not safepoints. Root assignment therefore depends on the
     profile.
   - Before a transition into native code, every reference the thread will
     need afterwards is already in a slot. The collector can then run without
     waiting for that thread.
   - Reference stores into the heap use the explicit store node from ADR 0001,
     where write barriers can be inserted for threaded, generational or
     concurrent collection.

## Consequences

- Correctness is straightforward to verify: the root set is explicit and
  auditable, and one mechanism covers native and WebAssembly targets.
- Phase A will be measurably slower than code using stack maps, particularly
  for pointer-heavy and call-heavy code. No performance claim is made for it.
- Phase B adds a liveness pass and a whole-program "may collect" analysis.
  Errors there cause missed roots, so Phase B relies on the stress modes below.
- Explicit safepoint nodes keep later moving or generational collection
  possible. Roots stay precise, so the collector can update them.

## Diagnostics and measurement

- **Stress mode:** collect at every eligible safepoint.
- **Poisoning:** in diagnostic builds, overwrite reclaimed objects so a stale
  unrooted reference fails deterministically.
- **Heap verification** after each collection, and a mode reporting each
  function's root slots.
- **Phase comparison:** Phase A and Phase B run against the same differential
  and stress corpus and must produce identical managed results.
- **Overhead estimate:** benchmark Phase A, Phase B, and an experimental build
  with rooting removed and collection disabled for inputs that fit in memory.
  The difference bounds the cost of rooting. Record inputs, hardware and
  toolchain versions (section 15).

## Alternatives considered

- **Conservative stack scanning with a precise heap.** Some production
  runtimes use it; references stay in registers and native speed improves.
  Not adopted: it conflicts with the precise-collection principle, can retain
  garbage unpredictably, pins stack-referenced objects against future moving
  collection and does not work on WebAssembly without an extra
  pointer-spilling pass. It may be evaluated as a native-only option if Phase B
  measurements show rooting remains a dominant cost, and would require its own
  ADR.
- **LLVM statepoints or stack maps.** Rejected: not expressible in portable
  C++, tied to one compiler and unavailable on WebAssembly.
- **Rooting only at the call site.** Rejected: a callee can hold an argument
  across its own safepoint after the caller no longer needs it.

## Validation

Before acceptance, a small experiment should compile a hand-written
pointer-heavy example in the Phase A frame style with Clang on macOS and with
Emscripten. The experiment confirms that frames pop correctly under the chosen
exception lowering and measures a first overhead estimate against a version
with rooting removed.
