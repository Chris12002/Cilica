# Compatibility manifest

Recorded 2026-10-01. Capabilities are stage-specific. A translated operation does
not establish native execution or platform compatibility.

| Component/profile | Implemented stage | Boundaries |
| --- | --- | --- |
| Metadata frontend | Inspection, IL structure, explicit binding and bounded audits | Depth-level validation and incomplete dependency reports; no execution guarantee |
| `int32-straight-line-v1` | Typed IR, C++20 emission and local native differential execution | Explicit static MethodDef; nongeneric owner/method; Int32 parameters/return; no initializer obligations |
| C++ backend | One C-linkage function with hash/token identity and IL offset comments | No managed objects, exceptions, calls, locals or control flow; public IR is validated before emission |
| Managed runtime and library | Planned | Object, array, GC, exception and library execution support is unimplemented |

## Int32 translation profile

| IL | Translation contract |
| --- | --- |
| `nop` | No executable effect; other operations retain their original IL locations |
| `ldarg.0`–`ldarg.3`, `ldarg.s`, `ldarg` | Verified parameter index; Int32 load |
| `ldc.i4.m1`, `ldc.i4.0`–`ldc.i4.8`, `ldc.i4.s`, `ldc.i4` | Exact signed 32-bit constant |
| `add`, `sub`, `mul` | Ordered operands; explicit wrapping arithmetic modulo 2^32 |
| `ret` | One Int32 value, empty remaining stack, final instruction |

Unsupported signatures/operations return diagnostics and no IR or C++ source. This includes
checked arithmetic, division, conversions, branches, calls, locals, references,
exception regions, required type/module initialization and synchronization.
Reference-assembly and native/runtime bodies cannot establish execution support.
Bounds and ownership follow [ADR 0014](decisions/0014-straight-line-int32-ir.md).

Current native evidence: .NET SDK 10.0.400 / runtime 10.0.11 and Apple Clang
17.0.0 (`clang-1700.4.4.1`), target `arm64-apple-darwin24.6.0`. Eight authored
C# methods produce 2,848 native results per build, compared directly with .NET
at `-O0` and `-O2`; the latter also enables `-fsanitize=undefined` and
`-fno-sanitize-recover=all`. Cases include signed extremes, overflow/underflow,
multiplication truncation, compound operand order, negative constants and an
unused parameter, using 100 boundary combinations and 256 seeded input tuples.
Process timeout/output-limit termination and cleanup also pass. Reduced tests
reject forged IR and verify CLI failures preserve existing output files.
See [ADR 0015](decisions/0015-int32-cpp-emission.md).

These are local conformance observations for this subset and toolchain. Other
native platforms, WebAssembly and the later runtime/embedding ABI are untested.
