# ADR 0014: Typed IR for straight-line Int32 methods

Status: accepted for translation slice 2a. Recorded 2026-10-01.

## Context

The metadata frontend currently validates stack depth and reports dependency
uncertainty. Native generation needs explicit typed operands, evaluation order
and arithmetic semantics. The first executable checkpoint needs a small profile
that can be reviewed and compared with authored .NET fixtures independently of
the remaining array/library analysis.

## Decision

Create `Cilica.Compiler` as a separate package referencing `Cilica.Metadata`.
Metadata remains responsible for owning readers, decoding and binding. The compiler
owns typed translation. Both inherit the banned-assembly-execution analyzer;
the binary-level regression also checks the new compiler package.

`AssemblyResolver.ReadMethodInput` accepts an explicitly owned input image and
MethodDef token. It snapshots method provenance, signature and flags, decoded
instructions, declared stack maximum, local/region counts, and initializer
obligations. Signatures retain resolver-owned definitions; finish translation
before disposing the session. The resulting `IrMethod` is detached: its source
is assembly identity, SHA-256, scoped method token and display name, with no reader
or absolute path. Display names are not symbol identities.

The `int32-straight-line-v1` profile requires a static nongeneric method on a
nongeneric owner, default managed calling convention, primitive Int32 parameters
and primitive Int32 return. Accept `nop`, every `ldarg` encoding, every `ldc.i4`
encoding, unchecked `add`, `sub`, `mul`, and one final `ret`. All accepted pushes
have the verified Int32 type. Pop the right operand before the left, then retain
both IDs in their original order. Require sufficient operands, valid argument
indices, the declared maximum stack and exactly one value at return. Falling
through or trailing instructions is rejected. This establishes typing within the
small profile, not a general CLI type verifier.

Reject other signatures/instructions, locals, exception regions, instance/generic
methods, runtime/native/PInvoke/abstract/synchronized implementations, reference
assembly bodies and required type/module initialization. Do not treat discarded
operations or unlowered initializers as harmless. Unsupported operations retain
the opcode and IL location; signature/profile failures retain method identity.
No failure returns consumable partial IR.

IR contains one explicit block with ordered typed temporary values and a return.
Expressions are Int32 constants, parameter loads and binary operations whose IDs
refer to earlier values. Source offsets survive translation. No constant folding,
code motion, dead-code removal or execution occurs. Public records are immutable
data; later consumers must validate caller-constructed records rather than assume
every arbitrary IR instance originated from the translator.

## Arithmetic contract

Each Int32 operation retains the low 32 bits and interprets the result as signed
Int32. Encode `AddWrapping`, `SubtractWrapping` and `MultiplyWrapping` explicitly.
The [CLI add semantics](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.add?view=net-10.0)
and [mul semantics](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.mul?view=net-10.0)
distinguish these unchecked operations from overflow-checking variants. Native
lowering must use defined bit-pattern arithmetic; signed C++ overflow cannot
implement this contract. Checked arithmetic, division and their exceptions require
later profiles and do not lower to wrapping operations.

## Bounds and diagnostics

Default per-method limits: one MiB of IL, 65,536 instructions and 65,536 inspected
initializer member rows across the owner and module. Limits must be positive.
Instruction decoding stops at its bound rather than constructing an unrestricted
list. Existing bounded signature parsing and explicit resolution still apply.
No native process is started by parsing or translation.

- `CIL2001`: unsupported execution instruction or runtime obligation.
- `CIL2002`: signature outside the execution profile.
- `CIL2003`: method-input translation limit.
- Preserve existing malformed body/signature, invalid operand, stack and control
  transfer diagnostic IDs. Damaged body headers are method-body errors, not
  signature errors. Reader-session misuse remains a caller API error.

## Validation, review and next work

Reduced metadata fixtures establish operand ordering, source offsets, constant
bit patterns, implicit/short/wide argument indices, invalid stack/return shapes,
unsupported instructions/signatures, locals, exception regions, initialization,
reference/runtime bodies, malformed headers/opcodes, exact limits and disposal.
An authored compiler-produced arithmetic method runs as the .NET reference.
A test-only evaluator of its translated IR compares normal and wraparound cases;
it never evaluates arbitrary compiler input and is not a production interpreter.
The native differential harness remains necessary in slice 2b.

Review checked scoped provenance, reader ownership, the no-partial-IR boundary,
right/left operand order, explicit wraparound, return/max-stack validation,
initializer obligations and decoder bounds. Local full-suite and focused checks
pass. C++ generation, native execution and the managed runtime remain planned;
see the [compatibility manifest](../compatibility.md).

Next: slice 2b emits C++ for this profile and compiles/runs authored fixtures in a
bounded disposable harness. Broadening the type/opcode set before proving this
complete path was rejected because it would postpone the first execution evidence.
A production interpreter was rejected because native generation is Cilica's
implementation goal. The unresolved library/filter gate remains tracked for later
milestones.
