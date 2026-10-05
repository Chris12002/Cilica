# ADR 0095: Checked and narrow integer conversion semantics

Status: accepted for slice 6c.4. Recorded 2026-10-05.

## Decision

Extend the explicit integer profile with conversion targets Int8/UInt8/Int16/
UInt16/Int32/UInt32/Int64/UInt64. The IR records checked overflow and unsigned
source interpretation independently of the target. Narrow signed results are
sign-extended to the Int32 evaluation stack; unsigned results are zero-extended.
This adds conversion semantics, not narrow parameter/local/heap storage.

The native helper represents the source as unsigned bits and computes an
unsigned magnitude before range checks. It never negates a signed minimum,
shifts by the type width, or relies on an out-of-range signed C++ cast. Checked
conversions use the existing overflow fault path before result publication,
including transitive may-fault analysis. Unchecked conversions truncate and
extend explicitly. Native integers and floating-point conversions remain errors.
Legacy Int32/object/value profiles do not admit the new conversion expressions.

## Validation and review

Sixty-four authored C# casts cover four source types and eight targets in checked
and unchecked contexts. Forty-eight exact authored CIL probes additionally cover
all 24 integer conversion instructions at both source widths, including variants
C# can optimize away. Their .NET oracle executes only the explicitly authored
instructions, never compiler input images. Forty-two boundary values cover both
sides of each signed/unsigned range; all 9,408 O0/O2 UBSan results match .NET.

Six malformed IR cases check operation/result/operand validation, exceptional
edges and legacy isolation. The focused run passes 75 tests, including prior
integer, emitter and native-fault regressions. Review checked unsigned mask
construction, minimum magnitudes, narrow sign extension, .un interpretation,
exception effects and unchanged result storage on fault. No actionable findings
remain. No hosted work was used. Heap/value storage and library integration remain
later slices.
