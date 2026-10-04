# ADR 0073: Bounded boxed-value CIL

Status: accepted for named nongeneric structs in the compiler-library profile.
Recorded 2026-10-04.

`ValueProgramDiscovery.ReadBoxes` and `ValueTranslator.TranslateBoxes` select
`boxed-values-cil-v1`. They compose `box`, `unbox` and value-form `unbox.any` with
the existing static value/class profile. Metadata type operands retain exact
selected-library identities and visibility checks. Boxing requires the exact
inline value operand; unboxing requires a reference or null. `unbox` produces a
synchronous borrowed location; `unbox.any` produces an independent value copy.
The same source verifier, scalar expansion and native helpers enforce execution.

This profile admits named nongeneric structs already covered by the layout
catalog. Primitive, enum, nullable and reference-form boxing/unboxing, generic
TypeSpec operands, ValueType reference views and boxed interface dispatch remain
diagnostics. Older value/class profiles keep their boxing rejection; CLI entry
points are unchanged.

Real C# empty structs exposed a pre-existing metadata gap: their sequential layout
can carry a one-byte size record. The value catalog now permits size one only for
fieldless sequential structs with default packing, while deriving zero logical
payload slots. Nonempty sized types, other explicit sizes, explicit layout and
packing requests remain unsupported. Regressions test the compiler-produced
metadata and the narrowly allowed/rejected reduced cases.

Local O0/O2 ASan/UBSan probes translate real C# boxing of reference-bearing structs
containing allocated class graphs. Independent boxes, copies, mutation of shared
reference targets and struct-return packets agree with .NET. Collection before
allocation preserves the graph; final root removal permits reclamation. Null and
wrong-type unboxes retain the original callee/IL fault location and leave packed
output unchanged. Box allocation failure reports the boxing instruction, fully
unwinds and permits reuse of the context.

A reduced CIL fixture uses `unbox` to lend mutable payload storage to a static
helper that allocates another box before mutation. Negative fixtures reject bad
operand kinds, hidden value types, readonly mutation and class-form boxing or
unboxing. Both native configurations retain the 2 KiB stack gate. Review checked
type/access authentication, exact copy versus borrow selection, old-profile
isolation, fault origins and the empty-layout exception. All 113 selected local
checks pass; no blocking findings remain. No hosted work ran.
