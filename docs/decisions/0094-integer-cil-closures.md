# ADR 0094: Mixed-width integer CIL closures

Status: accepted for slice 6c.3. Recorded 2026-10-05.

## Decision

`IntegerTranslator.TranslateProgram` selects `integer-arithmetic-cil-v1` and
returns the explicit integer IR profile from ADR 0093. It accepts static,
nongeneric Int32/UInt32/Int64/UInt64 closures. Reuse the existing scalar closure
discovery, resource bounds, access checks and block translation instead of
maintaining a second control-flow compiler. Existing Int32 entry points keep
their narrower signature and opcode admission.

Each stack value and incoming block slot carries an integer width. Arithmetic,
comparison, locals, call arguments, returns and joins require the appropriate
width; only explicit conversion instructions change it. Signedness comes from
the CIL operation, while signatures retain their structural metadata identity.
Admit `ldc.i8`, wide arithmetic, comparisons and branches, `not`, wrapping `neg`,
and `conv.i8/u8/i4/u4`. Widening uses signed or unsigned input interpretation as
specified by the instruction; narrowing retains the low 32 bits. These are
scalar compiler-library APIs, not heap support or the public embedding ABI.

## Validation and review

Thirty-one authored C# methods cover all wide arithmetic operations, conversion,
constants, branches, loops, recursion and faulting calls. Their translated C++
matches direct .NET execution for 288 operand pairs at O0/O2 UBSan: 17,856 results.
The focused scalar translation/native regression run passes 243 tests. Review
added a separate invalid-call-width regression; malformed arithmetic, comparison,
return, local, shift and join widths already diagnose. The solution builds cleanly.

Review checked stack-width propagation through loops and calls, detached output,
resource bounds, unsigned bit preservation and unchanged legacy profile admission.
No actionable findings remain. Checked conversions, narrow storage, heap/array/
borrow integration, generic dispatch and library/kernel work remain separate gates.
No hosted work was used.
