# ADR AC0244: Independent cast-message text parts

Recorded 2026-10-09 for AC8.100 after 5bb3968.

All 15,254 frozen Alchemy bodies pass individual IR validation, but the whole
closure constructs a complete InvalidCastException message for every source and
target pair. This exceeds even the diagnostic 2,097,152 UTF-16-unit allowance.
The converted reference uses the actual types to format a failing cast at runtime:
Unity 6000.3.2f1 libil2cpp/codegen/il2cpp-codegen.cpp:765-769 (SHA256
`ee9c0f88bd10dfe2e652b8b0168d2b19a1698d943c29a315238a5dfce6c3ab7d`)
and utils/Exception.cpp:21-33 (SHA256
`fafbb146d6bba48d3f5cb2e74bbac9439f34fd2bb8ca80ee7363249aadc140b2`).
This guides resource composition; Cilica retains its own implementation and exact
.NET display-name rules rather than adopting Unity's shorter type names.

## Decision and review

Retain one immutable UTF-16 prefix/source/separator part per source descriptor
and one target/suffix part per target descriptor and nullable flag. At Message
access, lookup validates both identities and the flag, then the existing bounded
string allocator joins the two spans in one managed allocation. The exception
caches that result as before. This replaces product-sized storage and lookup with
storage and lookup proportional to the number of sources plus targets.

Source types, boxed descriptors, array names, nullable target names and copy-cast
target discovery are unchanged. The total UTF-16 resource budget still applies,
using subtraction before addition to avoid overflow. Unknown descriptors are only
compared, never dereferenced. Native spans survive collection without managed
intermediates; allocation failure leaves the exception, cached message and fault
origin unchanged. Ordinary global exception resources keep their existing cache
policy. Cached global resources reject a second span rather than returning a
resource with mismatched text. Review checked exact formatting, descriptor/flag
admission, rooting, one-allocation behavior, cleanup and native frame limits.

CIL124/object IR77/native87 and logical/scalar exception IR10 record the change.
Layouts, owned core, intrinsic grants and production budgets remain unchanged.
No outstanding findings within this slice.

## Evidence

47 distinct selected checks pass: five new checks, five generated-name/native
exception-runtime controls, and 37 nullable, array-copy, exception-translation and
type-initialization controls (81.097s for the latter group). The 64-target raw IL
fixture now emits bounded native source and passes 294 O0/O2 comparisons; its
larger-name counterpart still hits the UTF-16 budget. Authored native consumers
compare every message code unit against .NET for class and boxed-struct sources,
collect the failed-cast source before reading the exception, reject forged
source/target/nullable identities, force allocation failure and then allow exactly
one object allocation, check cached identity, and collect back to the emergency
exception. O0/O2, ASan/UBSan, forced GC and the 2 KiB native-frame warning pass.
The first new fixture used boxed host Int32, hitting the existing String/interface
admission restriction; an ordinary boxed struct isolates this resource test.
Receipts: artifacts/alchemy-cast-message-segments.

The frozen closure passes the former cast-message resource gate and next reaches
the 1,024 phase A root-slot limit in Alchemy.Engine.GameState::With. All 15,254
bodies still pass method IR validation; discovery remains 3,839,616 units. Engine
probe: 92,725ms under shared host load. StateLookup still passes logical/scalar/root
checks (15 methods, 39 blocks, 232 scalar statements, 152 root slots, maximum 24;
708ms/11,759 units). This is a diagnostic copy with the existing explicit capacity
allowances, not production admission, native engine execution or saved-game replay.
Frozen inputs/core/manifest retain the AC0243 pins. Its scalar exception profile
label is v9; only the label advances to v10 in the final source, recompiled locally.
Receipts: artifacts/alchemy-cast-segment-ir-inventory.
