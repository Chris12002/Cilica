# Milestone 2: First primitive executable

Status: in progress; slices 2a–2c implemented. Slice outline recorded 2026-10-01.

First checkpoint: independently translate a small authored static C# integer
method into C++, compile locally and compare its result with .NET. This checkpoint
now passes for the bounded integer profile. Broader runtime execution remains
unimplemented. Milestone 1's unresolved library and
array-helper findings remain tracked gates for later capabilities.

## Proposed slices

| Slice | Outcome | Status |
| --- | --- | --- |
| 2a | Minimal execution contract and typed IR for straight-line Int32 methods | Complete for bounded translation profile |
| 2b | C++ generation and bounded native compile/run harness for those methods | Complete for bounded native profile |
| 2c | Locals, branches, loops and verified control-flow joins | Complete for initialized Int32 profile |
| 2d | Direct static calls and a bounded method closure | Next |
| 2e | Additional integer operations and required exceptional results, with .NET differential cases | Planned |
| 2f | Integration review and documented supported subset | Planned |

The first executable passed in 2b. These are proposed reviewable boundaries;
implementation findings may require a smaller corrective slice.

## Slice 2a design intent

Start with explicit static method roots whose parameters and return use Int32.
Translate constants, argument loads, straight-line unchecked add/subtract/multiply
and return into a small immutable typed representation. Retain input identity,
method tokens and IL locations for diagnostics. Validate stack types and return
shape rather than relying on the frontend's depth-only checks. Report unsupported
reachable operations and signatures at their IL locations.

Specify managed wraparound before generating C++; signed native overflow must
never become an implementation shortcut. Preserve evaluation order. The contract
should leave room for later exceptional operations and explicit pending exceptions,
without pretending those operations are supported in this first subset.

Reduced independent fixtures establish accepted operations, mismatched types,
unsupported signatures, malformed operands and arithmetic boundaries. Authored
reference behavior runs separately from compiler analysis. No compiler input is
loaded or executed while parsing or translating it.

Slice 2a outcome ([ADR 0014](../decisions/0014-straight-line-int32-ir.md)):

- `Cilica.Compiler` translates a bounded MethodDef input to one typed block with
  ordered temporaries, source identity/locations and explicit wrapping operations.
- The metadata reader snapshots inputs with byte/instruction/initializer bounds.
  Unsupported signatures, operations and runtime obligations produce no partial IR.
- Independent fixtures validate ordering, constants, argument encodings, failure
  locations and ownership. Authored .NET arithmetic agrees with a test-only IR
  evaluator, including wraparound. Native evidence remains the next slice.
- See the [compatibility manifest](../compatibility.md) for exact stage boundaries.

## Slice 2b and evidence

Emit readable C++ with a simple explicit entry convention and a deterministic
method/source mapping. Reuse the installed native compiler for machine code.
Build and run only authored generated fixtures in a bounded disposable test
harness. Document the toolchain used and compare results with .NET for normal and
wraparound arithmetic. The first Add method must pass this complete path before
claiming executable support.

Use targeted local checks. No hosted CI, long-running service, broad framework
compatibility claim or game-rule changes are part of this milestone plan.

Slice 2b outcome ([ADR 0015](../decisions/0015-int32-cpp-emission.md)):

- The validated IR becomes readable C++20 with an explicit hash/token C entry
  point and IL offset comments. Wrapping arithmetic uses defined unsigned
  operations; parameter/result conversion uses bit patterns.
- `cilica emit-cpp` accepts one MethodDef root and explicit reference directories.
  Failures emit no source and preserve an existing output file.
- Eight authored C# methods, including Add, pass the complete native path. Each
  configuration compares 2,848 results against .NET; local Clang `-O0` and
  `-O2` with undefined-behaviour sanitizer pass. Owned subprocess timeout/output
  bounds and cleanup are tested. No arbitrary compiler input is executed.
- Review covered forged IR, symbol/source injection, signed overflow, promotions,
  deterministic output and process ownership. See the compatibility manifest
  for exact toolchain and profile limits; managed references and allocation
  rooting are outside this profile.

Validation record: the local full suite passed 408 tests in 11.3 seconds, with
zero build warnings. After adding compiler/seed failure logging, a focused rerun
passed the optimized native case but the unoptimized compiler subprocess hit its
30-second bound during a host load spike (one-minute load above 138). Process
inspection confirmed no native compiler/test children remained. Keep the bound
and stop retries under contention; this was a compile timeout, not a result or
sanitizer mismatch. The logging change does not alter lowering semantics.

## Slice 2c outcome

[ADR 0016](../decisions/0016-int32-control-flow.md) replaces return-only blocks
with ordered statements and explicit return/jump/branch terminators. Initialized
Int32 locals, stack duplication/discard and integer comparisons are supported.
Block inputs carry stack values across joins and backedges, with exact arity/type
checks. C++ uses scoped blocks and immutable temporaries before edge copies.

Reduced tests cover local encodings, initialization/types, stack effects, malformed
targets, incompatible joins, live stack backedges, all conditional encodings,
caller-forged IR and exact block/statement/edge-copy limits. Fixture budget chains
use explicit branches to the next instruction; label fixups produced malformed
bytes in the original long fixture and were removed from that construction.

The local native harness now covers 17 authored C# methods plus two reduced IL
probes. Each native build checks 6,764 results, including conditional selection,
signed/unsigned ordering, zero/negative/bounded loop counts, mutations, stack joins,
a live stack backedge and zero initialization. Other local types, uninitialized
storage, unreachable blocks, switches, calls and exceptions remain errors.

Final validation: zero build warnings/errors and all 460 local tests passed in
9.1 seconds, including both native configurations and sanitizer checks. Review
covered initialization, branch polarity/signedness, typed joins, block-local
identity, C++ scopes/copies, source provenance and aggregate limits. The new Choose
probe exposed an existing test's global name-selection assumption; that test now
selects its exact scoped declaring type without weakening overload expectations.

Next: slice 2d translates a bounded closure of direct static calls, preserving
argument order and method identity. Start with two authored integer helpers and
shared dependencies; diagnose unsupported callees before emitting any program.
Keep initialization and exceptional operations explicitly unsupported until their
own lowering is implemented.
