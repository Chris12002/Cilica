# ADR 0024: Straight-line object operations

Status: accepted for bounded CIL translation and detached IR validation; native object execution/rooting remain planned. Recorded 2026-10-01.

## Context

Concrete reference value flow exists, but objects also require declaration access,
constructor initialization, field mutation and lifetime. These must be explicit
before native allocation is introduced. The first class execution checkpoint can
use straight-line methods; reference stack inference through branches/backedges
is a separate expansion. Unsupported control flow receives a diagnostic.

## Decision

1. `ObjectTranslator.Translate` uses the existing resolver and bounded method
   snapshots, an explicit entry and selected runtime library. It produces a
   detached `ObjectIrProgram` containing a complete call closure, multi-root
   catalog and method bindings. The entry accepts/returns Int32 only. Assemblies
   remain data; only owned test oracles execute outside compiler packages.

2. Instance methods have an explicit parameter-zero receiver of the exact
   declaring class. Bindings retain content identity, static/instance/constructor
   kind, declaration flags and externally visible owner status. Void is a return
   signature, never storage or a value. Separate invoke/void-return nodes preserve
   evaluation order without inventing a dummy integer result.

3. Allocation, field loads and field stores are explicit operations. Allocation
   identifies an exact constructor and carries already-evaluated arguments in
   declaration order; its result has the constructor owner's class type. Fields
   retain exact declaring owner/FieldDef identity. Receiver/storage assignments
   follow ADR 0023. Calls, allocations and field operations retain method-exit
   fault edges. These are structural effects, not implemented native faults.

4. Direct static/instance calls and nonvirtual `callvirt` are supported. The
   latter explicitly checks its receiver after argument evaluation. A direct
   instance call has no implicit null check: a constant-return method can be
   called on null without dereferencing it. Virtual/interface dispatch, indirect
   calls and prefixes remain unsupported. Reference `ceq` means identity;
   reference `cgt.un` against null maps to inequality, not pointer ordering.

5. A constructor must have an exact `.ctor` name, instance/void signature and
   both constructor flags. Its first effective operations load `this` and invoke
   the selected Object constructor. That base method must have the exact mapped
   owner, a parameterless ordinary managed signature and only `nop`/`ret`, without
   locals, initialization or handlers. It remains an included verified method,
   not a name-only intrinsic. Constructor chaining, work before base initialization
   and repeated base calls are conservatively rejected.

6. Methods permit same-owner access or public members on externally visible
   owners. Fields permit same-owner access excluding PrivateScope, or public
   fields on top-level public classes. Other access rules and inherited MemberRef
   owners need subsequent analysis. Readonly stores require the declaring
   constructor's direct parameter-zero receiver; local aliases are rejected.
   The layout profile still excludes static/native/value-type/array/string storage.

7. Share structural IR validation and resource accounting with primitive/reference
   profiles. Validate binding/signature shape before consuming operations, then
   complete method/block/type closure. Include allocation and void-call copies
   in the existing argument budget. The primitive emitter rejects these nodes
   and references; no object native entry is exposed. Caller-forged bindings are
   checked structurally, not authenticated against arbitrary source files.

8. Compose class catalogs once, using cached type snapshots under aggregate
   field/type limits. Member binding, initializer scans and snapshots share a
   65,536-unit ledger; repeated image/token bindings are cached. Candidate scans
   and budgeted member-owner traversal consume that ledger. Owner formatting
   has a hard 64-level ceiling. Method snapshots also count generic enclosing
   owners, preventing a nongeneric nested declaration from bypassing rejection.

## Alternatives and consequences

Treating every instance call as null-checking would change direct-call semantics.
Treating `callvirt` as virtual dispatch would unnecessarily reject ordinary C#
calls to nonvirtual methods. An explicit receiver-check flag preserves both.

Inferring native byte offsets or adding allocation immediately would obscure
unproven lifetime requirements. This slice ends at validated operation IR. Slice
3c must publish argument/local/temporary roots before allocation or possibly
allocating calls, enforce instance ownership, propagate faults and tear down a
bounded experimental heap. Native C++ and sanitizer evidence are required.

Reference control-flow inference is deferred rather than approximated. The
shared IR can validate declared joins, but this translator rejects branches,
loops and trailing unreachable instructions. Adding inference must preserve the
existing primitive control-flow corpus and receive its own conformance review.

## Validation

Independent synthetic IL covers valid and invalid constructors, readonly/access
rules, receiver/type mismatches, virtual/reference/initializer rejection,
nontrivial base constructors and operation fault edges. Caller-forged programs
cannot substitute constructors, retag allocations or bypass void/field rules.
Reduced cases guard aggregate fields with cached snapshots, allocation/void
argument copies, bounded member scans and excessive/generic owner chains.

An authored C# class probe creates, mutates and connects objects and agrees with
.NET through a bounded test-only IR evaluator, including wrapping arithmetic and
a null reference field fault. Authored raw IL verifies direct null calls versus
nonvirtual `callvirt` and null identity comparisons. The native Int32 differential
corpus remains required. This experiment establishes translation, not an object
heap, rooting, GC, full exception handling, library or platform compatibility.
