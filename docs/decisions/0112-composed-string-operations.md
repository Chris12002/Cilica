# ADR 0112: Composed UTF-16 indexing and concatenation

Status: Accepted

## Decision

The explicit integer-object CIL profile recognizes the selected runtime's exact
String character indexer, two-string Concat and inequality declarations. Existing
intrinsic authorization checks remain in force. These calls use the shared
StringCodeUnit, StringConcat and StringEquals IR/runtime operations, preserving
argument order and rooting through scalar value and exception lowering.

Indexing returns one zero-extended UTF-16 code unit and checks null before bounds.
Concat treats null as empty and preserves the runtime's operand-reuse rules.
Inequality negates ordinal equality with a pure comparison; it does not introduce
another fault edge. Other overloads and legacy CIL profiles remain unchanged.

## Evidence and review

All 101 focused string/exception checks pass. Four composed probes match .NET in
1,176 repeated native O0/O2 sanitizer results, covering embedded zero, unpaired
surrogates, null/bounds/argument fault ordering, concat identity, boxed/wide
values and collection across allocating dispatch. The first run detected an
incorrect fault edge on inequality's pure comparison; it was fixed before the
successful rerun. Review found no remaining actionable issue.

The private kernel probe gets beyond indexing and reaches an unsupported
exception-constructor form inside the handler registry. Native transcript
comparison remains pending.
