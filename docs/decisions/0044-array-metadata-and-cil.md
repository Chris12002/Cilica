# ADR 0044: Array metadata and bounded CIL translation

Status: Accepted for translation; native array execution remains planned.

## Context and decision

ADR 0043 established structural vector identities and validated array operations.
The next slice must carry those identities from metadata through method discovery,
fields, dispatch contracts and executable bodies without confusing arrays with
nominal Object layouts. It must also preserve bounded Int32 storage without
silently accepting arbitrary native-sized integers from CLI `ldlen`.

Use one nonrecursive storage grammar for model fields, executable signatures and
interface preparation: Int32, nominal class/interface references, void returns,
and SZARRAY of Int32 or nominal references. Reject nested vectors, multidimensional
arrays, other primitive/value elements, generics, pointers, byrefs and modifiers.
Arrays and nominal elements share the existing 256-type cap. Array roots from
signatures/operations and vectors discovered in field closure are explicit catalog
members. Reuse the existing type snapshot and metadata-member budgets. Arrays
remain structural identities, never candidate method owners or dispatch receivers.

Translate these instructions to the existing validated operations:

- `newarr` resolves its type operand through the metadata resolver. Only the exact
  Int32 definition from the explicitly selected runtime maps a named element to
  primitive storage. Cache both operand results and the optional runtime mapping.
  Reference element definitions enter the normal visibility/layout closure.
- `ldlen; conv.i4`, adjacent within one basic block, becomes a throwing Int32
  `ArrayLength` at the `ldlen` origin. Require both instructions in the same block
  so a branch cannot enter halfway through the normalized pair. Do not introduce
  a general native-integer IR type or accept other native-length consumers.
- `ldelem.i4`/`stelem.i4` require Int32 vector views; `ldelem.ref`/`stelem.ref`
  require reference vector views. Loads retain the static element type. Index,
  length and store-value validation and exception edges use the existing IR rules.
  A covariant store still requires a runtime allocation-element check later.

Exact array/null stack joins compose with the existing inference. An element
operation needs a known vector view during structural translation. When unequal
reference paths erase that view, issue an actionable diagnostic requesting an
explicitly typed array local. Do not guess an element type or add partially typed
operations to validated IR. Null-only element accesses use an opcode-selected
Int32[]/Object[] view; register these views only from the finished blocks, so
transient inference states cannot leave unused types or exhaust the catalog.

Token-form `ldelem`/`stelem`, `ldelema`, prefixes, array-target casts and general
native-sized values remain outside this slice. No IR/native profile number changes:
this slice produces operations already defined by `object-operations-ir-v7`.

## Validation and consequences

Authored C# fixtures compare translation with .NET via a bounded test-only evaluator:
Int32 allocation, zero initialization, aliasing, parameter/return flow, mutation,
length, negative-length/null/bounds faults and Object[] field/identity behavior.
Dispatch checks cover standalone interface metadata, interface calls and class
virtual overrides with exact array signatures. Raw tests cover supported/null/
unequal stack joins, typed-local recovery, combined exact/excess type limits,
unsupported signatures, opcode/storage mismatches and native-length boundaries.

The native emitter continues rejecting every array-bearing catalog. These tests
establish translation semantics, not native array storage, runtime covariance
checks, element tracing or collection. Native storage and ordered-failure
conformance are the next slice (3e-c).

Review completed with a warning-free solution build, 910 broader local checks
passing, and 156 final focused checks passing after the last test additions.
Review verified nominal/runtime identity separation, the combined type cap,
transient-view cleanup, length-conversion boundaries and the unchanged native gate.
Local main remains an ancestor; no push, hosted runs or additional agents ran.
