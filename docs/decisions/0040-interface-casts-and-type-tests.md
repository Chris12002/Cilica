# ADR 0040: Interface casts and type tests

Status: Accepted for the bounded retained-heap native profile.

## Context

ADR 0039 supplies exact descriptor membership for interface references and calls.
Class casts already use rooted arena slots and explicit fault edges, but interface
cast targets were rejected. Interface casts need the same object identity and
ownership checks without allocating wrappers or inventing interface layouts.

## Decision

`object-operations-ir-v6` permits named TypeDef/TypeRef targets from the validated
public nongeneric interface catalog. Targets used only by casts/tests enter the
same catalog and shared metadata/type budgets. Contract parents and implementing
classes use the existing assignability rules; no interface dispatch plan or method
body is needed merely to test a contract. Generic, nonpublic, value-type, array
and TypeSpec targets retain their diagnostics without partial IR.

`object-frames-native-v7` reuses exact descriptor-address membership for interface
`castclass` and `isinst`; class targets retain ancestry checks. Ownership is
established before reading the object descriptor. Membership compares addresses
without dereferencing unknown descriptors, including empty tables. Successful
operations preserve the original pointer and dynamic type; null remains null.
A mismatch reports invalid-cast status 7 for `castclass` and returns null for
`isinst`. Invalid/foreign pointers report ownership status 6 first. Faults retain
the originating method and IL offset, and argument evaluation order is unchanged.

Results use existing phase A slots, safepoints and arena cleanup. An unfinished
constructor receiver cannot escape through casts/tests before its immediate-base
call. No object allocation, exception object, boxing, RTTI, wrapper or adjusted
pointer is introduced. The packed Int32 boundary, arena/depth/root/metadata limits
and small native helper policy remain unchanged. Optimization remains deferred.

## Evidence and consequences

Three authored C# entries compare 144 .NET/native results and exact fault origins
at each of O0/O2. They cover null, compatible/incompatible classes, inherited and
parent/diamond contracts, explicit implementations, target-only empty contracts,
class/interface composition, identity, loops and interface calls after casting.
Cast-before-division and division-before-cast cases verify exception order,
including null receivers with failing arguments.

Allocation observers check both cast and type-test result slots across later
allocations. Exact/one-byte-short execution budgets, every object-count failure
stage, cleanup/reuse and null/foreign/invalid references are checked. Copied and
unreadable descriptor addresses cannot acquire membership. Raw fixtures keep
same-name/equal-token contracts in different images distinct and verify null
against target-only empty tables. Translation regressions reject malformed
targets, retagged/forged IR, missing fault edges and unfinished constructors.

ASan/UBSan, the 2 KiB native-frame warning and uninstrumented static stack reports
pass at O0/O2 on the recorded macOS ARM64 toolchain. Maximum reported stack sizes
across this test driver and generated code are 704 bytes at O0 and 640 bytes at O2.
These checks prove the bounded retained-heap behavior, not reclamation, a collector
or compatibility with untested targets. Inferred interface joins, default/static/
generic contracts, abstract classes and ordinary class virtual resolution on
MethodImpl owners remain separate work.
