# ADR 0042: Common reference types at stack joins

Status: Accepted for bounded CIL translation and retained-heap execution.
Recorded 2026-10-03.

## Context

The structural stack pass in ADR 0026 widens distinct reference types to Object.
With class hierarchies and interfaces implemented, that loses the shared base or
contract needed by a subsequent field access, call, return or local store. Pairwise
selection is insufficient for interfaces: two inputs can have several incomparable
common contracts, while a third input leaves one. Selecting Object early loses
information and makes acceptance depend on predecessor order.

## Decision

Keep structural inference and method discovery separate from precise reference
selection. The existing stack pass checks depths and integer/reference kinds and
constructs provisional blocks. Its coarse Object inputs are internal; they are
never returned as an approved program. After executable class/interface discovery
has reached its fixed point, use the complete validated catalog to refine those
blocks, before final operation/edge validation.

For every reference input, retain the set of common supertypes, including identity.
Explicit producers (parameters, locals, fields, allocations, calls and casts/tests)
use their static types' ancestor sets. Stack-input forwarding retains the entire
set. Null contributes no constraint. Intersect sets from all incoming edges and
revisit successors when a set shrinks, including self/backedges. Sets are finite
and only shrink after the first concrete contribution; the result is independent
of queue/predecessor order. Integer and null-only inputs keep their structural type.

Only after convergence, select a unique most-specific common type. Because the
sets are ancestor-closed, that type's ancestor set equals the intersection; a
linear scan over candidates suffices. Sibling classes can retain their shared
base, classes can retain an implemented contract, and interface references can
retain their shared parent. A real Object source still forces Object. Identity
uses image hash and token, never names or token alone.

If several incomparable minima remain, fail at the join with guidance to store
each branch into an explicitly typed local or cast before the join. This bounded
profile does not introduce intersection types or guess a preferred interface.
This rejection also applies when a class base and a separate interface are
incomparable common choices, or the consumer would only discard the value.
Unrelated references whose only common type is Object remain supported.

Rewrite only block-input types and their StackInput values. Explicit producers
retain their declared types; local stores and calls do not narrow their inputs.
Final IR validation checks every consumer and edge, including uses that were
provisionally null or Object. A unique selected source is assignable to a selected
successor type because the successor's common set is a subset of the source's.
No emitter, ABI, allocation, root-storage, dispatch-map or native profile change
is needed. Methods without coarse Object inputs need no refinement walk.

The refinement shares the existing 262,144-unit closure inference ledger: block
materialization, edges/slots, set copies/intersections, candidate selection and
rewriting are charged. Catalog identities/ancestors remain under the existing
256-type bound. Excess work returns a located diagnostic and no partial program.
No additional metadata is read by refinement.

## Validation and review

Authored C# covers sibling class calls/fields, unrelated class implementations,
parent interfaces, null selection and a joined reference live across allocation.
Raw IL tests all six predecessor orders with three inputs, including intermediate
ambiguity resolved by a third input, stack forwarding and backedges. Additional
fixtures check ambiguous joins, Object-to-interface narrowing, equal names/tokens
across images, and acceptable/excess reference-set work within the shared budget.

The native harness compares authored C# and an independently emitted DynamicMethod
loop oracle with generated C++ at O0/O2. It checks exact result/fault origins, argument
faults before null checks, packed outputs on failure, specific inferred input root
slots across allocations, exact/short arena budgets, every allocation-failure stage
and cleanup/reuse. ASan/UBSan and the existing 2 KiB static native-frame gate apply.
Compiler input assemblies remain data and are never executed by the translator.

Review completed with a warning-free solution build and 824 selected local
object/reference/dispatch/Int32/C++/control-flow regressions passing in 64.5 seconds.
The new native harness compares 210 outcomes per optimization level. Review covered
convergence, preserved static producer types, image-scoped identity, shared work
accounting, post-refinement edge validation, root-slot observations and fault cleanup.
Local main remains an ancestor. No push, hosted work or additional agents ran.

Arrays are the next planned milestone slice. Ambiguous inferred intersections,
abstract/generic/default interface semantics, collection and broader platform
compatibility remain outside this checkpoint.
