# ADR 0078: Detached single-target delegate IR

Status: accepted for authored delegate contracts and root planning; native emission
and CIL discovery remain subsequent gates. Recorded 2026-10-04.

`DelegateValueIrProgram` selects `delegate-values-ir-v1` over the existing composed
value/boxing flow. The reference catalog gains intrinsic delegate declarations with
exact nominal/closed identity, an Int32 parameter count and Int32 or void return.
Declarations share the combined type bound and have a 4096-parameter aggregate
signature budget. Each signature reserves capacity for a possible captured receiver.
Delegate references have their own identity and the Object view; broader Delegate
base/interface views and variance are not part of this gate.

`NewDelegate` names one exact static or instance method and a managed receiver
operand. Static targets require a null operand; instance captures require an
assignable reference or null. Validation checks accessibility, complete signature,
return kind and target binding. Constructors, virtual slots, open instance targets,
closed static first-argument binding and value/byref signatures remain unsupported.
The target enters the reachable closure even when it is called only through a
delegate. No unmanaged function pointer appears in value flow.

`CallDelegate` and void `InvokeDelegate` check the exact delegate view and Int32
arguments. Creation and invocation require explicit fault exits. Allocation and
call safepoints retain the delegate and captured receiver roots; outgoing argument
accounting includes space for the hidden receiver. Constructor pre-initialization
rules continue to prevent capture/escape of an uninitialized receiver.

Delegate catalogs are accepted by the explicit source wrapper. Earlier boxed and
native profiles reject them, so this checkpoint cannot accidentally emit unsupported
native storage. Runtime receiver authentication, null behavior, allocation, invocation
and fault mapping require the next slice's native tests. Multicast operations and
broader delegate signatures remain outside this initial contract.

Review checked identity collisions, target visibility/reachability, scalar signatures,
void calls, forward operands, explicit fault edges and phase A root expansion.
All 248 selected local verifier/catalog checks pass, including private-target,
malformed-operation and 1024-root regressions. The compiler builds cleanly. No
native delegate execution or hosted work is claimed by this slice.
