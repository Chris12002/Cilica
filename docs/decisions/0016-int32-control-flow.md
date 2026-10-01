# ADR 0016: Initialized Int32 locals and explicit control flow

Status: accepted for initialized Int32 control flow. Recorded 2026-10-01.

## Context

[ADR 0015](0015-int32-cpp-emission.md) proves native arithmetic for one return-only
block. Branches and loops need explicit stack transfers and mutable local storage.
Treating every block as a return, or turning IL stack entries into mutable global
temporaries, would lose the information needed to check joins and preserve copies.

## Decision

The `int32-control-flow-v1` profile supersedes the earlier straight-line IR shape.
Keep the same independent compiler package, metadata ownership, C entry naming,
wrapping arithmetic and source-only CLI boundary. This early public IR API evolves;
it is not a stable serialization contract. Existing arithmetic inputs retain their
observable behavior.

Snapshot absolute branch targets, local signature types and the local-initialization
flag in `MethodInput`. Require a static nongeneric default-convention method with
Int32 parameters/return and primitive Int32 locals. Local storage requires
`InitLocals`; initialize it to zero once per invocation. Reject Boolean, pinned,
non-Int32 and uninitialized local storage. An optimized authored fixture project
avoids Debug C# Boolean bookkeeping without changing existing inspection fixtures.

Partition IL at branch targets and after transfers/returns. Targets must be exact
instruction starts inside the method. Process blocks through an iterative worklist
starting with an empty stack at entry. Every accepted load/operation produces
Int32; predecessor stacks must have identical Int32 depth at a join/backedge.
Check stack underflow, parameter/local indices, the declared maximum, every return
and fallthrough. Reject unreachable blocks conservatively rather than leaving them
unverified. This is a verifier for the stated Int32 profile, not every CLI stack type.

IR blocks have typed inputs, ordered value definitions/local stores, and explicit
return, jump or conditional-branch endings. Temporary IDs are local to their block.
Incoming stack positions become immutable `StackInput` values; edge arguments use
those value IDs in bottom-to-top order. Stores stay in sequence with loads and
arithmetic, so a later mutation cannot change an earlier captured value. Multiple
returns and backedges are supported. The compiler does not prove loop termination.

Support all local load/store encodings, `dup`, `pop`, `ceq`, `clt[.un]`, `cgt[.un]`,
unconditional branches, zero/nonzero branches, and equality/signed/unsigned ordered
conditional branches in short/wide forms. Comparison values are Int32 0/1. Signed
relations use Int32 interpretation; unsigned relations compare UInt32 bits.
Arithmetic still wraps through unsigned 64-bit intermediates and narrowing.
Argument stores, switches, calls, checked operations, division, conversions,
initializers and exception regions remain unsupported.

Emit a C++ scope for each block, with its label outside the scope. A transfer leaves
the current scope and enters at the start of the destination; it cannot jump past
an unexecuted local initialization. Method locals and block-input storage precede
the initial jump. Capture block inputs into immutable temporaries before writing
edge storage. Sequential edge assignments therefore preserve parallel transfer,
including backedges and repeated targets. No mutable destination is its own source.
These primitive values do not allocate or require managed rooting.

`IrValidator` checks caller-constructed blocks, statement ordering, local/value
indices, expressions/comparisons, terminators, edge arity/types/targets and graph
reachability before source is produced. Invalid caller IR remains `ArgumentException`;
unsupported/malformed assembly input remains a diagnostic with no partial IR/source.

## Resource bounds

Keep existing method-byte/instruction/initializer and signature bounds. Add 4,096
blocks and aggregate limits of 65,536 statements, 65,536 typed block inputs and
65,536 edge arguments. Count synthetic stack inputs and both conditional edge
copies; a small body must not produce unbounded expanded IR/source. Bound parameters
and locals to 4,096. Translation exhaustion uses `CIL2003`; the emitter rejects
caller records outside the same limits. Traversal is iterative and each verified
block is translated once; discovering another equal incoming stack adds no work.

## Validation and review

Reduced IL tests cover every local and conditional encoding, initialized storage,
unsupported local types, stack effects, malformed destinations, incompatible joins,
live values across backedges, caller-forged IR and exact block/statement/copy limits.
Long budget fixtures encode branches to the next instruction directly; their
original label fixups produced malformed bytes and were removed from that generator.

The bounded native harness compares 17 authored C# methods and a reduced live-stack
loop with .NET references, plus a zero-local probe with an independent expected 0.
Each build checks 6,764 results at `-O0` and `-O2`; the latter uses undefined-behaviour
sanitization. Inputs include both branch outcomes, signed/unsigned boundaries,
compound stack joins, local mutations and zero/negative/bounded loop counts.
The case table records each method root, reference function and loop-input bound.

Final local validation passed all 460 tests in 9.1 seconds; the solution build
had zero warnings/errors. A pre-existing compiled-member test selected global
same-named references and collided with the new Choose probe. Its selection now
uses the exact scoped declaring type; the original overload expectations remain.

Review checks operand order, branch polarity/signedness, join invariants, local
initialization, copies across cycles, C++ scope rules, unsigned arithmetic,
aggregate bounds and the data-only compiler boundary. The recorded native target
remains macOS ARM64 with Apple Clang 17; other targets and the runtime are untested.

## Alternatives and next work

Retaining a separate branch-only backend was rejected because it would duplicate
lowering/verification and postpone a coherent IR. SSA and structured-loop recovery
are deferred; explicit blocks and copies make the baseline semantics reviewable.
Silently zeroing uninitialized IL locals was rejected because it changes the input
contract. Supporting Debug Boolean locals requires its own storage semantics.

Next is slice 2d: a bounded closure of direct static calls, with unsupported callees
diagnosed before program emission. The wider library/filter gate remains unresolved.
