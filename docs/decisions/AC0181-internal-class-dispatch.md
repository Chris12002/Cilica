# ADR AC0181: Same-assembly internal class dispatch

Recorded 2026-10-08 for AC8.55.

The frozen engine calls internal abstract GameOccurrence.Permits(PlayerId) from
ShieldReactionDiscovery. Extend only the closed class resolver and integer-object
contract to internal slots. C# emits CheckAccessOnOverride on these methods; admit
that flag only on internal virtual instance declarations, retaining it in IR.
Bodyless abstract flags, ordinary managed bodies, exact signatures, newslot/final
rules, complete receiver coverage and existing call access checks remain required.

Internal slot receivers and reuse-slot implementations must share the canonical
slot assembly. Reuse cannot widen/narrow internal access. Detached IR checks the
same assembly/access/receiver constraints independently. Legacy class resolution,
interface mapping, protected/private virtual slots and foreign internal callers
remain restricted. Public behavior is unchanged. Metadata slot selection does not
itself authorize calling an inaccessible declaration.

Eight scalar/reference/void probes supply 3,024 repeated O0/O2 native/.NET comparisons
for ordinary inheritance, generic owners, hidden slots, re-abstraction, retained
references, null receivers and fault ordering. Both IR stages reject malformed
abstract declarations/maps and altered visibility/strict flags. Metadata tests
check six access forms, legacy rejection, exact budget boundaries, foreign receivers
and foreign callers. All 33 new and 90 existing class/interface/abstract/public-
baseline checks pass (123 distinct checks) with forced GC, ASan/UBSan and exact
frame/cleanup checks. Initial reduced metadata passed while compiled C# failed;
raw metadata inspection identified CheckAccessOnOverride, now explicitly tested.

Review checked unchanged caller authorization, canonical same-assembly identity,
slot visibility, exact final/newslot resolution, bodyless declarations and complete
maps. Closed class target v4, CIL v77 and object IR v50 record the extension. Native
v74, interface profiles, grants and all limits remain unchanged. Main 48ecf14 is an
ancestor. Concurrent candidate/Trim files are owned by the other compatibility
chat and excluded from this commit. The frozen probe reuses AC5a.8's exact archived
core to isolate this compiler change. Evidence: artifacts/alchemy-internal-virtual{,-engine}.

Frozen discovery advances past visibility to the separately retained inline-value/
borrow abstract-signature restriction at Discover IL_00d4: 998,255 metadata units
in 4,031 ms. StateLookup remains 15 methods/11,547 units (440 ms). Full engine,
unchanged fingerprint and saved-game replay execution remain unfinished.
