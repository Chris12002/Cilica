# Compatibility manifest

Recorded 2026-10-01. Capabilities are stage-specific. A translated operation does
not establish native execution or platform compatibility.

| Component/profile | Implemented stage | Boundaries |
| --- | --- | --- |
| Metadata frontend | Inspection, IL structure, explicit binding and bounded audits | Depth-level validation and incomplete dependency reports; no execution guarantee |
| `int32-straight-line-v1` | Typed IR translation | Explicit static MethodDef; nongeneric owner/method; Int32 parameters/return; no initializer obligations |
| C++ backend | Planned for slice 2b | No emitted native program yet |
| Managed runtime and library | Planned | Object, array, GC, exception and library execution support is unimplemented |

## Int32 translation profile

| IL | Translation contract |
| --- | --- |
| `nop` | No executable effect; other operations retain their original IL locations |
| `ldarg.0`–`ldarg.3`, `ldarg.s`, `ldarg` | Verified parameter index; Int32 load |
| `ldc.i4.m1`, `ldc.i4.0`–`ldc.i4.8`, `ldc.i4.s`, `ldc.i4` | Exact signed 32-bit constant |
| `add`, `sub`, `mul` | Ordered operands; explicit wrapping arithmetic modulo 2^32 |
| `ret` | One Int32 value, empty remaining stack, final instruction |

Unsupported signatures/operations return diagnostics and no IR. This includes
checked arithmetic, division, conversions, branches, calls, locals, references,
exception regions, required type/module initialization and synchronization.
Reference-assembly and native/runtime bodies cannot establish execution support.
Bounds and ownership follow [ADR 0014](decisions/0014-straight-line-int32-ir.md).

Current evidence: independent reduced fixtures and authored .NET arithmetic
compared with a test-only IR evaluator. Native and WebAssembly behavior has not
been tested; no portability or optimization-level guarantee is claimed.
