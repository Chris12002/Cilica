# ADR 0026: Reference control flow and bounded stack inference

Status: accepted for the bounded direct-Object class profile. Recorded 2026-10-02.

The distinct-reference-to-Object inference rule below is superseded by
[ADR 0042](0042-reference-join-inference.md), which selects unique common class or
interface types after catalog discovery and rejects ambiguous intersections.

## Context

ADR 0025 runs straight-line object methods with phase A roots. Ordinary C# object
code also selects references in branches, carries them through loops and may
allocate while those values are on the evaluation stack. Stack depth alone does
not establish the types at joins, and first-predecessor types are insufficient
when a backedge later widens a null or class reference.

## Decision

1. Share the supported branch vocabulary and instruction-boundary partitioning
   with Int32 translation through `CilControlFlow`. Object translation infers
   stack types using an iterative work queue. Start with an empty entry stack;
   enqueue reached blocks and revisit them whenever an incoming slot widens.
   Retain the final translated blocks after convergence, then validate the full
   method/type/operation closure. Unreachable blocks and unsupported transfers
   remain errors. No input assembly executes in the compiler process.

2. The join lattice is deliberately small. Equal types retain their type; null
   plus a concrete reference yields that reference. Different concrete classes
   widen to the selected Object base, because the current layout profile permits
   only classes derived directly from that base. Null plus Int32, reference plus
   Int32 and different incoming depths fail. There is no implicit narrowing from
   Object to a class. A consumer requiring a specific class must still receive
   an assignable type after convergence; provisional null types cannot approve
   a later invalid call or field operation.

3. Bound inference separately from input/output sizes: 262,144 work units across
   a translated closure count instruction visits, incoming input materialization,
   argument copies and outgoing edge slot visits (plus one per edge). Existing
   per-method and aggregate IR/metadata limits still apply. Cache resolved calls
   by caller/IL site so revisits charge the call-copy ledger once per instruction.
   Index cached field snapshots by token so revisits do not repeatedly scan fields.
   Large valid methods may exceed this conservative budget and receive a located
   error; they never return partial IR.

4. Ordinary methods support the existing Int32 branch family, plus reference
   truth/null tests and identity equality/inequality branches. Ordered reference
   comparisons, switches, leave/handlers, argument stores and casts remain
   unsupported. Constructors retain ADR 0024's straight-line first-base-call
   rule, including readonly/access checks. Conditional constructor initialization
   needs its own analysis and is not implicitly authorized by ordinary CFG support.

5. Expand the separate root plan to include reference/null block inputs and
   temporaries identified by `(block, value)`, alongside parameters and locals.
   Safepoints also retain block identity. Every input gets different storage from
   every source temporary. StackInput definitions copy into their own temporary
   slots, so sequential edge assignments preserve a parallel transfer even on
   backedges that swap references. The 1,024-root bound covers the whole method,
   not each block independently. No slot reuse/liveness analysis is introduced.

6. `ObjectCppEmitter` emits `object-control-flow-native-v1`: method-scope locals,
   input storage and root frame precede the entry jump; each labelled block has
   its own temporary scope. Reference inputs alias root slots. Jump/branch edges
   copy stack arguments before jumping, without allocation between copies.
   Reference truth tests compare with nullptr, never integer ordering. Frame
   teardown, instance checks, pending faults, Int32 boundary and heap/call limits
   remain those of ADR 0025. Static temporary slots are overwritten when their
   blocks run again; phase A may retain old values until that overwrite or return.

## Validation and review

Synthetic IL checks null/class joins, reference backedges/swaps, class-to-Object
widening, revisited calls, invalid depths/kinds and a callee that becomes invalid
only after a later widening. Large stack forwarding can exceed inference work
while remaining within input/final-IR limits; paired fixtures exercise that bound.
A near-limit loop carries 65,504 call arguments across revisits and verifies that
the call-copy ledger charges each IL site once.
Constructor branches, argument stores and ordered reference branches retain
located rejection diagnostics. Root keys/slots are distinct across inputs and
block-local value IDs, and existing exact/excess native frame checks remain.

Authored C# selects nullable references, builds/walks linked objects, compares
identity, widens different classes to Object and propagates arithmetic failures
from loops. Authored raw IL carries/swaps two references on a stack backedge while
allocating. A separate DynamicMethod .NET oracle emits those finite authored
instructions; compiler input bytes remain data. Seven entries compare 2,093
result/fault cases per native O0/O2 configuration under ASan/UBSan, with exceptions
and RTTI disabled, through private helpers and the C boundary. Allocation observers
inspect original references in frames as loop storage changes; loop allocation
failure and arithmetic origin checks verify fault exits and popped frames.

Self-review specifically checks convergence after self/backedge widening, strict
validation after convergence, aggregate root/input/edge bounds, simultaneous edge
transfer, scoped C++ jumps, roots across allocations and pending-fault origins.
Revisit accounting includes argument work and avoids repeated field scans. The
retained heap still cannot prove correctness under reclamation; precise collection,
collection stress, phase B, inheritance/casts/dispatch, arrays/strings, handlers,
library and broader platform compatibility remain separate gates.
