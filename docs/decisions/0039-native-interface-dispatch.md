# ADR 0039: Native interface membership and typed dispatch

Status: Accepted for the bounded retained-heap native profile.

## Context

ADR 0038 separates abstract interface signatures from executable class bodies and
validates their complete receiver maps. Native emission still required every
call target to be an executable method and every reference type to have a class
descriptor. An interface has neither a body nor an instance layout. It must also
retain the receiver's existing identity, fields and dynamic class type.

## Decision

`object-frames-native-v6` emits a descriptor-address membership table for each
included contract. Its compatible classes come from the validated catalog's
existing assignability rules, including inherited implementations and transitive
parent/diamond contracts. Interfaces receive no physical storage descriptor.
All reference values remain the original `Object*`; no wrappers or pointer
adjustment are introduced.

Reference checks first establish instance ownership without reading an untrusted
pointer. A non-null interface reference then checks its descriptor address against
the membership table. Class references retain their existing ancestry checks.
Membership helpers do not dereference an unknown descriptor. Parameters and
reference field stores use these same checks.

Interface call selection uses the declaration's exact signature and its validated
effective body map. Class and interface maps share the existing typed function
pointer table machinery; return categories are Int32, object reference or void.
There are no function-pointer casts or fake executable interface methods.
Arguments stay in the caller's arena packet and published roots. Depth/frame
reservation remains at callee entry, after argument evaluation and receiver
checks. The 64-call limit and all existing memory/root/IR bounds remain.

Null faults retain status 4, ownership/type or absent-table faults status 6, and
callee resource faults their existing status/origin. Receiver descriptor reads
follow ownership checks. Exact address lookup preserves contract identity across
images. Empty tables support null-only calls. Existing scope destruction removes
roots before releasing/poisoning frames, preserving the packed Int32 boundary
contract and typed retained-heap destruction.

The small helper policy remains: ownership/membership selection is isolated from
other statements, and checked call pointers remain in arena storage. Membership
and dispatch tables are static bounded data, not per-invocation C++ arrays.

## Evidence and consequences

Eleven independently authored C# entries compare 170 result/fault-origin outcomes
with native execution at each of O0/O2. They exercise inherited, hidden, overridden,
reimplemented and explicit methods, separate contracts, diamonds, metadata-only
base slots, class/interface composition, reference parameters/fields/results,
void calls, null-only maps and recursive/mutually recursive calls. A raw two-image
fixture proves same-name/equal-token contract calls select distinct private bodies.

Native diagnostics check roots and interface fields during later allocations,
exact/short arena budgets, all object-count failure stages, cleanup and reuse,
foreign/invalid/null/wrong-type receivers and copied/unreadable descriptor
addresses. ASan/UBSan, the 2 KiB warning and static stack reports remain active at
both optimization levels. Uninstrumented reports measured maxima of 896 bytes
at O0 and 848 bytes at O2 across this native test driver and generated code.
A constrained recursion thread checks the retained 64-call policy. The first O0
run caught a test driver with eleven cases' locals in one `main`; separating the
cases into diagnostic helpers retained the existing stack gate without changing
generated execution storage or its limits.

This proves only the bounded retained-heap profile on the recorded macOS ARM64
toolchain. Collection, interface casts/tests, inferred joins, default/static/generic
contracts, abstract classes and ordinary class virtual resolution on MethodImpl
owners require later work. Lookup optimization remains deferred until
representative application measurements justify it.
