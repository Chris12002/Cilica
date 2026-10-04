# ADR 0085: Bounded managed exception CIL

Status: accepted for the compiler-library profile. Recorded 2026-10-04.

`ValueProgramDiscovery.ReadExceptions` and `ValueTranslator.TranslateExceptions`
compose the existing closed-generic value/box/delegate pipeline with validated
exception metadata. Region boundaries partition blocks, and handler/filter entry
stacks are seeded explicitly. Throw, rethrow, leave, endfinally and endfilter keep
IL origins through scalar lowering. Reference joins use a bounded monotone worklist
and require a unique most-specific common type. Earlier profiles stay opt-in.

Selected runtime Exception, SystemException, ArithmeticException and the eight
runtime fault types use authenticated intrinsic shells, not framework private
layouts or constructor IL. Parameterless/single-string construction is supported
for Exception and the eight concrete fault types. Custom exceptions must be public,
top-level, nongeneric, fieldless and free of initialization/virtual members; their
constructors must only forward unchanged arguments to the direct base constructor.
Unsupported constructors and Message overrides are diagnosed instead of skipped.
Exception types reached through generic fields receive the same validation.

Selected Exception.Message, String.Length, static string equality, UTF-16 literals
and nominal reference type tests/casts are lowered explicitly. Runtime cast/unbox
failures retain source/target descriptor identities so lazy messages include their
runtime type names. Closed type snapshots carry bounded metadata-only display names;
no input assembly is loaded into the compiler. Message resources and display names
have explicit size limits. Unusual escaped names and interface-target cast messages
remain diagnostics. The current resources are invariant English, checked against
local .NET 10; localization and broader framework constructors are not implemented.

General filters remain rejected before translation. Only pure acyclic type-test
filters with definite current-evaluation local assignment qualify. OOM reserve
allocation is charged to the heap quota; fatal ownership/depth/arena failures are
not catchable managed exceptions. Packed entries still expose status and original
image/MethodDef/IL, not a public structured exception ABI.

Validation: 1,685 managed regressions passed before four final budget/profile tests
were added; the final focused 49-test run passed those additions and shared delegate/
IR checks. Native O0/O2 ASan/UBSan probes compare 12 C# entry programs over eight
inputs, plus uncaught cross-frame origin and emergency OOM recovery. They exercise
nested cleanup, replacement exceptions, ordered catches, accepted/rejected filters,
messages, generic boxes and delegates, collection before allocation, repeated context
reuse and exact/one-byte-short arena limits. Separate generic/exception storage
regressions also pass. The 2 KiB native stack gate remains enabled.

Review corrected null/reference joins, field-only exception discovery, contextual
cast messages, declaration/method budgets and validation ordering. General filters,
private compiler-generated closure layouts, arbitrary exception APIs, public exports
and untested platforms remain outside this bounded milestone profile.
