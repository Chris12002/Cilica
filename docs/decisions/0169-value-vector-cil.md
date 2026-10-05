# ADR 0169: Closed value-vector CIL admission

Accepted 2026-10-06; milestone 6c.54.

Admit named and closed generic inline value elements through the integer-object
compiler profile. `newarr`, typed `ldelem`/`stelem`, and value `ldelema` use exact
closed nominal identities. Class and value field signatures share the same vector
grammar and include the element's value layout in the bounded closure. Legacy
profiles retain their existing limits.

CIL lowering selects logical value reads/writes and synchronous element borrows.
The existing declaration-specific `Array.Empty<T>` grant now also admits these
exact value identities and reuses the per-instance descriptor cache. No additional
library intrinsic or provenance input is needed. Empty values remain distinct.

Do not turn selected primitive TypeDef operands into user structs: explicit
Boolean/Char/byte/short/UInt32/Int64/UInt64 vectors remain diagnostic until their
width-specific identities and storage are implemented. Enum and nested vectors
remain outside this admission. `ldelema` of reference/primitive elements and
readonly-prefixed element addressing still require later lowering.

Validation: 79 focused/regression checks pass with a clean solution build. Four
compiled C# probes cover copies, borrowed mutation, mixed Int32/Int64/reference
fields, class/struct array fields, closed generic elements, empty values and caught
null/bounds/negative-length faults (1,176 repeated O0/O2 .NET/native outcomes).
A ported-core empty-value-array probe adds 294 outcomes with cache identity and
bounds checks. Sanitizers, collection at allocations, frame budgets and cleanup
remain enabled. Review added allocation-only primitive rejection regressions.

The real kernel passes its former PlayerId newarr gate and reaches metadata work
exhaustion in PriorityWindowRequest::.ctor IL_009d. Evidence is
`artifacts/milestone6-harness/gate-mapped-6c54.json`. Array interface integration,
the rest of the kernel closure and native transcript comparison remain pending.
