# Milestone 2: First primitive executable

Status: planned. Slice outline recorded 2026-10-01.

First checkpoint: independently translate a small authored static C# integer
method into C++, compile locally and compare its result with .NET. The backend
and runtime are currently unimplemented. Milestone 1's unresolved library and
array-helper findings remain tracked gates for later capabilities.

## Proposed slices

| Slice | Outcome | Status |
| --- | --- | --- |
| 2a | Minimal execution contract and typed IR for straight-line Int32 methods | Next |
| 2b | C++ generation and bounded native compile/run harness for those methods | Planned |
| 2c | Locals, branches, loops and verified control-flow joins | Planned |
| 2d | Direct static calls and a bounded method closure | Planned |
| 2e | Additional integer operations and required exceptional results, with .NET differential cases | Planned |
| 2f | Integration review and documented supported subset | Planned |

The first executable is targeted for 2b. These are proposed reviewable boundaries;
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

## Slice 2b and evidence

Emit readable C++ with a simple explicit entry convention and a deterministic
method/source mapping. Reuse the installed native compiler for machine code.
Build and run only authored generated fixtures in a bounded disposable test
harness. Document the toolchain used and compare results with .NET for normal and
wraparound arithmetic. The first Add method must pass this complete path before
claiming executable support.

Use targeted local checks. No hosted CI, long-running service, broad framework
compatibility claim or game-rule changes are part of this milestone plan.
