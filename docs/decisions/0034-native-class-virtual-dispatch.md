# ADR 0034: Bounded native class virtual dispatch

Status: accepted for the concrete class profile. Recorded 2026-10-02.

## Context

ADR 0033 resolves one exact class implementation but does not discover its body
or execute a virtual call. Closure expansion must account for overrides that
allocate new receiver classes or introduce new virtual sites. Native lowering
must also preserve the budgeted heap-frame design: a wrapper that remains on the
native stack while invoking its callee would increase every recursive call chain.

## Decision

`object-operations-ir-v4` adds explicit `VirtualDispatch` facts to value and void
calls, canonical slot identities to virtual bindings, and per-declaration maps
from concrete receiver identities to implementation MethodDefs. Identities retain
image content hashes and tokens. `callvirt` on a virtual declaration dispatches;
`call` on the same declaration invokes its body directly. Nonvirtual `callvirt`
retains its existing null-check contract.

After draining ordinary body discovery, build the class catalog and resolve each
virtual declaration against every compatible concrete catalog class. Discover and
verify the selected bodies, then repeat until no new methods remain. Override
bodies may introduce classes through allocations, signatures, fields or type
tests, and may add virtual sites. Previously resolved pairs are cached. Exact
resolution and type/member snapshots share the existing 65,536-unit metadata
ledger; the 256-method/type limits and aggregate IR/inference budgets remain.
Dispatch maps have at most 256 declarations and receivers per declaration, with
65,536 aggregate entries. No partial program survives an unsupported body.

Detached validation checks virtual flags, public ordinary instance bindings,
canonical slot/owner ancestry, newslot identity, same-slot names/signatures and
one implementation per slot owner. Maps must exactly match virtual call operands,
cover every compatible receiver once and select the nearest included body of the
canonical slot. Replacing a final implementation is rejected. Map bodies join
the existing iterative reachability check. Both value and void virtual calls
require explicit receiver null checks and method-exit fault edges. These facts
are structural assertions by the detached API caller, not metadata authentication.

`object-frames-native-v5` emits typed per-declaration tables keyed by exact native
descriptor pointers. A small non-inlined lookup returns a function pointer;
the caller stores it in its budgeted arena frame before invoking the selected
body through the existing fixed packet signature. All targets have the same
return type and non-receiver signature. There are no function-pointer casts or
derived-to-base C++ layout casts. Keep lookup separate from invocation so its
native frame is no longer live during managed recursion. Linear lookup is bounded
by the small catalog; indexing and allocation/slot reuse are later optimizations.

All arguments are evaluated before null, ownership and ancestry checks. Invalid
or foreign receivers and missing exact table entries report status 6; null reports
status 4. Faults identify the original call site. The selected callee reserves its
own frame under existing depth/storage limits, reporting its own origin on entry
failure. Reference arguments/results use the existing phase A root slots; void,
reference and Int32 calls retain immediate pending-fault propagation. Cleanup
pops roots before releasing arena storage on every return/fault path. The packed
Int32 boundary and resource statuses remain unchanged.

The scope is public nongeneric class virtual methods on concrete nongeneric
hierarchies. Interfaces, abstract classes, generics, return covariance, explicit
MethodImpl mappings and broader member-access rules remain unsupported. Assemblies
remain data in compiler packages. The heap still retains objects until teardown;
this adds no collector or stable reference ABI.

## Validation and review

Four authored C# entries supply 172 .NET/native result and exact fault-origin
cases per O0/O2 build. They cover overridden/inherited/sealed methods, overloads,
property getters, independent hidden virtual slots, nonvirtual hiding, direct
base calls, Int32 edges, argument faults before null checks, reference returns,
void overrides and allocating bodies. A class discovered only in an override
body verifies the repeated closure pass. Different classes on the evaluation
stack still join conservatively to Object; fixture branches store into declared
base locals separately rather than expanding that independent inference contract.

Native probes observe a returned reference in the caller's roots during later
allocations, force each allocation failure stage, test exact/one-byte-short arena
budgets, ownership/null/invalid receivers and missing descriptor entries, then
check complete root/storage/depth cleanup. Direct calls on a null receiver remain
valid for bodies that do not dereference it. Equal-name/equal-token declarations
and overriding bodies in different assemblies dispatch independently. Finite and
depth-limited virtual recursion requests a 256 KiB pthread stack and verifies
the actual macOS stack stays within a 320 KiB ceiling. Builds pass ASan/UBSan and
`-Wframe-larger-than=2048` on the recorded macOS ARM64 toolchain. Uninstrumented
`-fstack-usage` reports verify static usage at or below 2 KiB for every emitted
native function and the test harness at O0/O2.

Malformed detached maps/bindings and missing call/fault facts fail before source
is returned. A reachable unsupported override reports its method and virtual
inclusion path. Review checked fixed-point completion, bounded pair caching,
slot-chain separation, typed indirect-call signatures, rooting, receiver/fault
order and native wrapper lifetime. Review replaced repeated aggregate-map counting
with a running counter. Sanitizers caught a test-owned copied descriptor whose
lifetime ended before heap teardown; the fixture descriptor now has static lifetime.
An older test that rejected all virtual calls now verifies their explicit maps.

Final local validation: zero build warnings/errors; all 604 relevant native,
object, reference-IR, C++ emission, Int32 and exact-class-target tests pass in
55.1 seconds. Both native dispatch cases also pass after adding the stack-report
and actual-thread-size assertions. Local main and cached origin/main remain
ancestors. No push, hosted execution, additional agent or service was started.

Performance tuning remains deferred under ADR 0029. The 2 KiB warning is a tested
baseline, not a permanent production cap. Other native targets require their own
stack measurements. Interface mapping and native interface calls are the next
separate slice; precise reclamation remains milestone 4.
