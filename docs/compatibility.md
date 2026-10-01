# Compatibility manifest

Recorded 2026-10-01. Capabilities are stage-specific. A translated operation does
not establish native execution or platform compatibility.

| Component/profile | Implemented stage | Boundaries |
| --- | --- | --- |
| Metadata frontend | Inspection, IL structure, explicit binding and bounded audits | Depth-level validation and incomplete dependency reports; no execution guarantee |
| `int32-static-calls-v1` | Typed IR, C++20 emission and local native differential execution | Explicit static MethodDef and complete direct-call closure; nongeneric owner/method; Int32 parameters/return; initialized Int32 locals; typed block inputs; no initializer obligations |
| C++ backend | One C-linkage entry with private helper definitions, hash/token identity, IL comments and scoped block labels | No managed objects or exceptions; public IR is validated before emission |
| Managed runtime and library | Planned | Object, array, GC, exception and library execution support is unimplemented |

## Int32 static-call profile

| IL | Translation contract |
| --- | --- |
| `nop` | No executable effect; other operations retain their original IL locations |
| `ldarg.0`–`ldarg.3`, `ldarg.s`, `ldarg` | Verified parameter index; Int32 load |
| `ldc.i4.m1`, `ldc.i4.0`–`ldc.i4.8`, `ldc.i4.s`, `ldc.i4` | Exact signed 32-bit constant |
| `add`, `sub`, `mul` | Ordered operands; explicit wrapping arithmetic modulo 2^32 |
| `and`, `or`, `xor`, `not` | Exact 32-bit bitwise operations; `not` consumes one operand |
| `shl`, `shr`, `shr.un` | Int32 value/count; count masked with 31; low 32-bit left shift, explicit sign extension or zero-fill right shift |
| `ldloc.0`–`ldloc.3`, `ldloc.s`, `ldloc`; corresponding `stloc` forms | Verified Int32 storage index; zero initialization required when locals exist; stores retain order |
| `dup`, `pop` | Verified Int32 stack duplication/discard |
| `ceq`, `clt`, `clt.un`, `cgt`, `cgt.un` | Int32 result 0/1; explicit signed/unsigned comparisons |
| `br`, `br.s`; `brtrue`, `brfalse`; `beq`, `bne.un`, `blt`, `bgt`, `ble`, `bge` and applicable `.un`/`.s` forms | Explicit branch edges; exact instruction targets; matching Int32 stack inputs at each join/backedge |
| `call` | Exact scoped declaration; nongeneric static Int32 signature; already-evaluated arguments in signature order; verified complete closure |
| `ret` | One Int32 value and empty remaining stack at every reachable return |

Unsupported signatures/operations return diagnostics and no IR or C++ source. This includes
checked arithmetic, division, conversions, switches, virtual/indirect calls,
argument stores, references,
exception regions, required type/module initialization and synchronization.
Boolean, pinned, non-Int32 and uninitialized locals are unsupported. Unreachable
blocks are conservatively rejected. Loop/recursion termination is not proved;
managed stack exhaustion is unimplemented.
Reference-assembly and native/runtime bodies cannot establish execution support.
Bounds and ownership follow [ADR 0014](decisions/0014-straight-line-int32-ir.md)
and [ADR 0016](decisions/0016-int32-control-flow.md), extended by
[ADR 0017](decisions/0017-bounded-static-call-closure.md). The current program profile extends
the control-flow IR data shape; this is an early evolving API, not a stable IR
serialization or embedding ABI. Prior arithmetic inputs retain their semantics.

Calls accept members of the same declaring type (excluding compiler-controlled
PrivateScope), or public methods on externally visible types. Internal, family,
friend-assembly and inherited MemberRef owner rules are conservatively unsupported.
Reference the exact declaring type; required type/module initialization still fails.
Program traversal is iterative, includes each content-hash/MethodDef identity once,
and limits closures to 256 methods, 1 MiB IL and 65,536 instructions. Existing IR
limits also apply across the program: 4,096 blocks/parameters/locals and 65,536
statements/block inputs/edge arguments. Call argument copies have their own 65,536
bound. Declaring-type snapshots are bounded to 64 levels by default.
`Translate` retains the call-free method API; `TranslateProgram` and `EmitProgram`
handle complete closures. Caller IR with missing targets, invalid arity/operands,
duplicate identities, unreachable methods or excess resources is rejected.

Bitwise/shift semantics extend the same program profile; see
[ADR 0018](decisions/0018-int32-bitwise-and-shifts.md). Counts are masked to five
bits even for raw IL without a C# mask. This is Cilica's defined choice consistent
with C# and the tested .NET runtime; CIL leaves oversized counts unspecified.
All native shifts use unsigned 64-bit operands with bounded counts; signed right
shift supplies sign bits explicitly. Unary complement uses a new validated IR
expression. No new throwing operations or exception ABI are implemented.

Current native evidence: .NET SDK 10.0.400 / runtime 10.0.11 and Apple Clang
17.0.0 (`clang-1700.4.4.1`), target `arm64-apple-darwin24.6.0`. Thirty-two authored
C# entry methods and five reduced IL probes produce 16,252 native results per build,
checked against authored .NET results and the independent zero-local expectation
at `-O0` and `-O2`; the latter also enables `-fsanitize=undefined` and
`-fno-sanitize-recover=all`. Cases include signed extremes, overflow/underflow,
multiplication truncation, compound operand order, negative constants and an
unused parameter, signed/unsigned comparisons, both branch outcomes, joins, loop
mutation, live stack values across backedges, shared helpers, cross-assembly calls,
argument capture before local mutation, bounded direct/mutual recursion, bitwise
composition and shifts in calls/loops, using 100 boundary combinations and 256
seeded input tuples per method. Seven shift probes additionally use ten boundary
values and 44 counts (0..33, negative/wrap boundaries and Int32 extremes).
C# loop/recursion counts are bounded to -32..32;
the reduced stack-loop probe uses 0..31. Raw shift references run independently
authored .NET DynamicMethods with no explicit count mask; no input bytes are
loaded or executed by the compiler. The zero-local probe has expected result 0.
Process timeout/output-limit termination and cleanup also pass. Reduced tests
reject forged IR and verify CLI failures preserve existing output files.
See [ADR 0015](decisions/0015-int32-cpp-emission.md) and
[ADR 0016](decisions/0016-int32-control-flow.md).

These are local conformance observations for this subset and toolchain. Other
native platforms, WebAssembly and the later runtime/embedding ABI are untested.
