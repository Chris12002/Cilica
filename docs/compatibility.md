# Compatibility manifest

Recorded 2026-10-01. Capabilities are stage-specific. A translated operation does
not establish native execution or platform compatibility.

| Component/profile | Implemented stage | Boundaries |
| --- | --- | --- |
| Metadata frontend | Inspection, IL structure, explicit binding and bounded audits | Depth-level validation and incomplete dependency reports; no execution guarantee |
| `int32-control-flow-v1` | Typed IR, C++20 emission and local native differential execution | Explicit static MethodDef; nongeneric owner/method; Int32 parameters/return; initialized Int32 locals; typed block inputs; no initializer obligations |
| C++ backend | One C-linkage function with hash/token identity, IL comments and scoped block labels | No managed objects, exceptions or calls; public IR is validated before emission |
| Managed runtime and library | Planned | Object, array, GC, exception and library execution support is unimplemented |

## Int32 control-flow profile

| IL | Translation contract |
| --- | --- |
| `nop` | No executable effect; other operations retain their original IL locations |
| `ldarg.0`–`ldarg.3`, `ldarg.s`, `ldarg` | Verified parameter index; Int32 load |
| `ldc.i4.m1`, `ldc.i4.0`–`ldc.i4.8`, `ldc.i4.s`, `ldc.i4` | Exact signed 32-bit constant |
| `add`, `sub`, `mul` | Ordered operands; explicit wrapping arithmetic modulo 2^32 |
| `ldloc.0`–`ldloc.3`, `ldloc.s`, `ldloc`; corresponding `stloc` forms | Verified Int32 storage index; zero initialization required when locals exist; stores retain order |
| `dup`, `pop` | Verified Int32 stack duplication/discard |
| `ceq`, `clt`, `clt.un`, `cgt`, `cgt.un` | Int32 result 0/1; explicit signed/unsigned comparisons |
| `br`, `br.s`; `brtrue`, `brfalse`; `beq`, `bne.un`, `blt`, `bgt`, `ble`, `bge` and applicable `.un`/`.s` forms | Explicit branch edges; exact instruction targets; matching Int32 stack inputs at each join/backedge |
| `ret` | One Int32 value and empty remaining stack at every reachable return |

Unsupported signatures/operations return diagnostics and no IR or C++ source. This includes
checked arithmetic, division, conversions, switches, calls, argument stores, references,
exception regions, required type/module initialization and synchronization.
Boolean, pinned, non-Int32 and uninitialized locals are unsupported. Unreachable
blocks are conservatively rejected. Loop termination is not proved by the compiler.
Reference-assembly and native/runtime bodies cannot establish execution support.
Bounds and ownership follow [ADR 0014](decisions/0014-straight-line-int32-ir.md)
and [ADR 0016](decisions/0016-int32-control-flow.md). The current profile supersedes
the straight-line IR data shape; this is an early evolving API, not a stable IR
serialization or embedding ABI. Prior arithmetic inputs retain their semantics.

Current native evidence: .NET SDK 10.0.400 / runtime 10.0.11 and Apple Clang
17.0.0 (`clang-1700.4.4.1`), target `arm64-apple-darwin24.6.0`. Seventeen authored
C# methods and two reduced IL probes produce 6,764 native results per build,
checked against authored .NET results and the independent zero-local expectation
at `-O0` and `-O2`; the latter also enables `-fsanitize=undefined` and
`-fno-sanitize-recover=all`. Cases include signed extremes, overflow/underflow,
multiplication truncation, compound operand order, negative constants and an
unused parameter, signed/unsigned comparisons, both branch outcomes, joins, loop
mutation and live stack values across backedges, using 100 boundary combinations
and 256 seeded input tuples per method. C# loop counts are bounded to -32..32;
the reduced stack-loop probe uses 0..31. The zero-local probe has expected result 0.
Process timeout/output-limit termination and cleanup also pass. Reduced tests
reject forged IR and verify CLI failures preserve existing output files.
See [ADR 0015](decisions/0015-int32-cpp-emission.md) and
[ADR 0016](decisions/0016-int32-control-flow.md).

These are local conformance observations for this subset and toolchain. Other
native platforms, WebAssembly and the later runtime/embedding ABI are untested.
