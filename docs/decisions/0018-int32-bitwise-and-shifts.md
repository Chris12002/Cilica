# ADR 0018: Int32 bitwise operations and defined shifts

Status: accepted for the Int32 program profile. Recorded 2026-10-01.

## Context

[ADR 0017](0017-bounded-static-call-closure.md) supports bounded integer programs
with calls, storage and control flow. Bit manipulation needs exact widths and
shift semantics before translation to native expressions. C# shift counts use
their low five bits for Int32 operands; raw CIL leaves oversized shift results
unspecified. Native code must never shift by an invalid count or overflow signed
arithmetic while reproducing the selected managed behaviour.

Sources: [C# bitwise/shift reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/bitwise-and-shift-operators)
and [CIL Partition III, shl/shr/shr.un](https://download.microsoft.com/download/7/3/3/733ad403-90b2-4064-a81e-01035a7fe13c/ms%20partition%20iii.pdf).

## Decision

Extend the existing Int32 binary operations with AND, OR, XOR, left shift,
signed-right shift and unsigned-right shift. Add a unary operation/expression for
bitwise complement. Append enum members without changing previous values. Retain
the early `int32-static-calls-v1` profile and C entry convention; this is an
additive capability, not a stable serialized IR/embedding ABI promise.

Translate `and`, `or`, `xor`, `shl`, `shr` and `shr.un` by popping the right/count
operand and then the left/value operand. `not` pops one operand. Every result is
Int32. Preserve ordered definitions and IL offsets, and apply the existing
stack, control-flow, call-closure and resource checks. Caller-constructed unary
IR must have a known operation and an earlier operand, just as binary operands
must refer to earlier definitions.

Define Int32 counts as their unsigned bit pattern AND 31, even when raw IL does
not include C#'s mask. This is Cilica's chosen behaviour for otherwise unspecified
CIL cases, consistent with C# and independently observed on the recorded .NET
runtime. It does not claim that every CLI implementation specifies this choice.
Left shift keeps the low 32 bits; unsigned right shift fills with zeros; signed
right shift repeats the original bit 31.

Emit bitwise operations/complement with unsigned 64-bit intermediates, narrowing
to UInt32. Left/logical-right shift operands are unsigned 64-bit values and their
counts are explicitly masked to 0..31. For signed-right shift, emit one private
helper per program that needs it. Mask the count, zero-fill the original bits and
OR in a sign fill derived by unsigned subtraction from zero. At count zero the
fill is empty; otherwise its unsigned shift count is 32 minus the masked count,
in 1..31. Every evaluated native shift stays below its 64-bit operand width.
No native signed shift/overflow behaviour supplies the managed result.

This slice adds no allocations, references, exceptions or ownership changes.
Division, checked arithmetic, conversions and argument stores remain errors.
The broader managed runtime and CoreLib/filter audit gates remain unresolved.

## Evidence and review

Reduced tests verify binary operand direction, the unary stack effect, source
offset/type preservation, missing operands for every new instruction, forged
enum/operand records and continued short/wide argument-store rejection.

Nine new C# entries cover each operation, composition through direct helpers and
bit manipulation of initialized locals in a loop. The initial loop fixture
mutated its parameter and correctly failed on unsupported `starg`; the same
calculation now uses a local within the declared storage profile. Argument-store
regressions preserve that boundary explicitly.

The native harness now checks 32 authored C# entries and five reduced IL probes.
Each configuration compares 16,252 results. In addition to the existing boundary
and seeded inputs, seven shift probes use ten values and 44 counts: 0..33,
negative/wrapping boundaries and Int32 extremes. Three raw IL probes deliberately
omit any explicit count mask. Independent .NET DynamicMethods emit only authored
load/load/shift/return instructions, giving a runtime oracle without executing or
loading the compiler's input images. Production assemblies remain data-only.

Both `-O0` and `-O2` run with exact result count/order and empty runtime stderr;
the optimized build retains undefined-behaviour sanitization. Review checks
zero-count sign fill, bit 31, unsigned promotions, masked counts, low-bit
truncation, operation/operand validation, helper linkage and existing resource
bounds. The compatibility manifest retains the exact local target/toolchain.
The solution build had zero warnings/errors; all 534 local tests passed in
14.0 seconds, including both native configurations and sanitizer checks.

## Alternatives and next work

Relying on the C# compiler to insert masks would leave raw IL unsafe. Native signed
shifts would obscure the explicit primitive contract. Treating complement as an
artificial binary operation would hide its stack arity. All were rejected.
Masking and sign extension remain explicit baseline semantics; optimize only with
preserved differential and sanitizer evidence.

This completes sub-slice 2e-a, not all of slice 2e. Next design a pending-exception
result path across method calls and the native entry before adding division and
checked arithmetic. Include zero divisors, signed overflow and ordered propagation
tests. Catch/filter/finally execution requires further lowering; it remains
unsupported until independently validated.
