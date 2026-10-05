# ADR 0157: Proven ordinal string comparison

Status: Accepted for a direct Ordinal mode constant

## Decision

Retain the pinned StringComparison enum declaration and map the selected three-
argument String.Equals overload. Require its exact public static/internal-call
signature, selected String/enum identities, Int32 enum storage and a dedicated
StringOrdinalEqual build grant. Do not import enum instances into the value layout.

Body translation consumes the already evaluated mode operand and proves its IR
value is the Ordinal constant (4). It then emits the existing StringEquals operation
on the two evaluated string operands. This preserves ordinal UTF-16/null behavior,
roots and faults without introducing a new runtime operation. Other comparison
modes and aliases remain compile errors; cultural/ignore-case semantics and general
enum storage are separate work.

## Evidence and review

One probe provides 294 repeated O0/O2 native/.NET comparisons with sanitizers and
collection before allocation: null/empty, distinct equal strings, case differences,
embedded NUL and unpaired surrogates. Tests verify all six pinned enum constants,
reject unsupported/invalid/runtime modes and require the exact grant. The complete
solution builds cleanly. Fifty distinct related checks pass after the separately
recorded lexical interface agreement correction. Review checked exact declaration
shapes, operand proof, enum storage, evaluation order and source provenance.

The concrete kernel advances to the bounded metadata work limit. Its native traces
and final implementation/filter audit remain outstanding.
