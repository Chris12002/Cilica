# ADR 0017: Bounded direct static-call programs

Status: accepted for the Int32 program profile. Recorded 2026-10-01.

## Context

[ADR 0016](0016-int32-control-flow.md) proves initialized integer storage and
control flow within one method. Ordinary helpers need declaration binding,
argument stack effects and a complete executable closure. Emitting a caller
before checking its helper could leave a seemingly successful artifact that
contains unsupported semantics or unresolved native symbols.

## Decision

Add `IrProgram`, with one content-scoped entry and immutable methods, plus
`IrExpression.Call` with an exact target and ordered block-local argument IDs.
Detached method identity is image SHA-256 plus MethodDef token; binding uses the
resolver's scoped metadata identities. Display names remain diagnostic data.
The `int32-static-calls-v1` program profile extends the existing Int32 block IR.
The early API is evolving, not a stable serialization or embedding ABI.
Retain `Translate`/`Emit` for call-free methods. `TranslateProgram`/`EmitProgram`
handle complete closures; the CLI uses the program path.

Reuse structural binding and the existing method translator. An iterative queue
includes each exact method once, allowing diamonds, direct recursion and mutual
recursion without repeated translation. Resolve MethodDef/MemberRef declarations
from the input and explicit reference directories. Generic calls and owners,
instance/virtual/indirect calls, non-Int32 signatures and unsupported callee bodies
fail the whole program. All current initialization, exception and storage gates
apply to every callee. Return no partial IR/source on failure. Diagnostics retain
the failing location and deterministic first inclusion path from the root.

Call parameters are popped in reverse stack order and stored in signature order.
The result is a new Int32 value. Argument expressions have already executed as
ordered IR statements, including calls and local mutations. Generated calls use
only immutable temporaries, so C++ argument evaluation order cannot reorder IL
effects or change a captured argument.

Declaration binding is not access verification. Snapshot each method's scoped
declaring TypeDef and whether its whole enclosing chain is externally visible.
Accept members of the same declaring type, except PrivateScope, or public methods
on externally visible types. Conservatively reject broader internal, family,
friend-assembly and nested-access rules. The executable binder requires a member
declared on its referenced owner; inherited MemberRef owners need further access
and initialization analysis. General metadata/audit binding retains inherited
lookup. An executable diagnostic recommends referencing the declaring type.

Validate caller-constructed program IR before building source: exact entry/target
membership, unique content identities/native symbols, Int32 signatures, argument
arity and earlier operands, each method's block invariants, aggregate limits and
reachability of every supplied method from the entry. Missing closure data is an
API error (`ArgumentException`), not an assumed external native implementation.

Emit all declarations before bodies. Every program gets a namespace derived from
its root hash/token and private static helper functions; one external C wrapper
keeps the existing entry naming/Int32 convention. Separately generated roots can
share managed helpers without native definition collisions, including when the
test harness concatenates programs into one translation unit. No metadata display
text enters native syntax. Existing unsigned wrapping, signed bit conversion and
scoped control-flow lowering remain unchanged.

## Bounds and limitations

Limit a closure to 256 methods, 1 MiB IL and 65,536 instructions. Cap each method
read by the remaining program budget, even when API callers request larger reader
limits. Existing IR limits also bound program totals: 4,096 blocks, parameters and
locals; 65,536 statements, block inputs and edge arguments. Call argument copies
have a separate 65,536 limit, including repeated references to the same value.
Use shared accounting for translator and emitter. Declaring-type snapshots detect
cycles and default to at most 64 enclosing levels.

Recursion uses the native call stack. Termination and managed stack exhaustion are
not implemented; tests deliberately bound recursion depth. Primitive calls do
not allocate or carry references, so this slice introduces no GC roots or native
object ownership. This does not establish class-library or Alchemy execution,
and the wider CoreLib/filter audit gate remains unresolved.

## Evidence and review

`Int32CallTests` covers shared/cyclic closures, reader detachment, deterministic
ordering, cross-assembly scoped binding, explicit-directory failures, malformed
calls, unsupported callees, transitive paths, preserved output, bounded access,
inherited-owner rejection and caller-forged programs. Paired exact limit tests
cover methods, instructions (including oversized requested reader limits), total
statements, repeated call arguments and enclosing-owner snapshots.

The native harness covers 23 authored C# entry methods and two reduced IL probes.
Each `-O0`/`-O2` build compares 8,900 results; optimized execution uses undefined
behaviour sanitization. New probes cover shared helpers, calls in loops,
noncommutative argument order, capture before mutation, cross-assembly calls and
bounded direct/mutual recursion. Shared roots compile in one translation unit.
Only authored test references execute; production inputs remain metadata data.

Review checks binding/access boundaries, identity/deduplication, call stack
effects and ordering, transitive diagnostics, fixed aggregate budgets, recursive
declarations, private linkage and unchanged arithmetic/ownership rules. The
compatibility manifest records the local toolchain and untested targets. The
final solution build had zero warnings/errors; all 506 local tests passed in
11.8 seconds, including both native configurations.

## Alternatives and next work

Inlining every helper would duplicate semantics and obscure call identity and
budgeting. Depending on external native definitions would allow an incomplete
successful artifact. A new call-only translator would duplicate the established
block verifier. All three were rejected. Private per-program helpers may duplicate
code between separately emitted roots; whole-program sharing can be added later
with explicit multiple-root semantics and preserved conformance.

Slice 2e adds integer operations and their required exceptional outcomes. Start
with a bounded sub-slice for defined Int32 bitwise/shift semantics; division and
checked arithmetic need an explicit exception result path before native support.
