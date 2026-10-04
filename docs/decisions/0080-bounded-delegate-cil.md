# ADR 0080: Bounded delegate CIL binding

Status: accepted for the compiler-library profile. Recorded 2026-10-04.

`ValueProgramDiscovery.ReadDelegates` and `ValueTranslator.TranslateDelegates`
select `delegate-values-cil-v1`, composing the existing closed generic/value/box
profile with public sealed nongeneric delegates. Intrinsic declarations must derive
directly from the selected runtime MulticastDelegate, have no fields, interfaces,
initializers or native layout, and declare exact runtime `.ctor(Object, IntPtr)`
and Int32-parameter `Invoke` with Int32 or void return. A bounded metadata reader
snapshots those declarations without reading their bodies. Catalog builders retain
the intrinsic delegate identity even when reached through a class/value field.

An adjacent `ldftn`/delegate `newobj` pair within one basic block becomes one checked
allocation. No method pointer enters ordinary value flow. Target discovery includes
exact accessible static or nonvirtual instance method bodies; instance bodies retain
the scalar/reference storage restriction. `callvirt Invoke` becomes checked integer
or void invocation. Construction retains the newobj fault offset and target faults
retain their original origin. Argument, declaration and metadata work share existing
bounds. Other executable profiles continue to reject delegate input.

Public named delegates can be stored in fields, returned, passed as closed generic
type arguments and embedded in boxed reference-bearing values. Compiler-generated
private closure visibility, generic delegate declarations (including Func/Action),
ldvirtftn, multicast operations, variance and broader signatures remain diagnostic;
this gate does not claim arbitrary C# lambda compatibility or a public delegate ABI.

Review checked metadata ownership, exact flags/signatures, scope identity, shared
budgets, method-pointer adjacency and branch boundaries, target reachability and
native rooting. All 126 selected review checks pass. Real C# O0/O2 ASan/UBSan probes
compare static/instance/void calls, null captures/invocations and target faults with
.NET while retaining a boxed generic delegate and reclaiming receiver/delegate
cycles. Reduced metadata rejects malformed declarations, signatures, private types,
virtual pointers and interrupted construction. Native zero-target tables are also
exercised through a null-only invocation. No hosted work ran.
