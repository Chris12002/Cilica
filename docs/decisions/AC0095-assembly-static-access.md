# ADR AC0095: Same-assembly internal static access

Accepted 2026-10-07 for AC8.26.

The larger diagnostic-only frozen engine probe passes ToList and FindIndex, then
reaches GameStateScope constructor IL_0001 after 71,138 metadata-work units.
Metadata-only inspection of the frozen assembly proves UnitRoster (0200003e) is
internal/sealed/beforefieldinit and Empty (0400013a) is Assembly/Static/InitOnly.
The old static-field gate rejected this legal same-image reference.

Use one ObjectStaticFieldAccess rule in discovery and the shared logical/scalar
IR validator. Preserve same-owner access and visible public access. Add Assembly
and FamORAssem access only in the integer-object profile, with equal content hashes,
validated nonempty caller/target chains and assembly-visible or lexically visible
target ownership. Assembly-visible closed generic owners retain their exact closed
identities. Do not infer private, protected-only, family-and-assembly, foreign-image
or static-address access. Readonly stores retain the separate declaring-initializer
check. Missing/contradictory facts cannot widen the new permission.

CIL v29, object IR v32 and native v59 identify the admitted semantics. Physical
layout v22, interface plan v12, native algorithms, helper grants, 256-type and
65,536-metadata production bounds remain unchanged.

Eight native probes pass 2,352 repeated O0/O2 comparisons: internal scalar/wide
storage, references, reference-bearing values, closed generic storage, readonly
initialization, failed initialization, FamORAssem and private nested-type ownership.
Raw metadata rejects private/protected/private-scope, foreign-image access,
inaccessible nested ownership and readonly writes. Seven detached mutations reject
missing caller/target chains, disallowed flags and readonly stores in both stages.
All 33 authored and 78 adjacent static/initialization/public-baseline checks pass
(111 distinct checks). Public baseline remains 40 native controls/seven known gaps.

Initial a passed all 10 raw checks; the shared positive-fixture build encountered
ldsflda when Roslyn cleared a static value directly. The fixture now stores a
returned zero value, preserving the existing whole-value-copy contract. The
separate static-address negative remains checked. All 33 b checks pass in 20.357
seconds and all 78 c checks in 150.748 seconds. Forced GC, ASan/UBSan, exact quota
cleanup, 2,048-byte frame warnings and 30-second subprocess caps remain enabled.

Review checked equality of image identity, member flags versus type visibility,
closed-owner identity, shared access validation before cached-field reuse, readonly
ownership, retained lexical consistency and unchanged storage/root emission.
Frozen input hashes remain unchanged; no game rules or application binaries were
modified or executed. Production roots still hit their type bound; full replay is
unfinished. A subsequent larger diagnostic refresh is recorded separately.

Evidence: artifacts/alchemy-query-engine-refresh (pre-change reachability) and
artifacts/alchemy-static-access/ac8-26a-*, ac8-26b-*, ac8-26c-*, frozen-field.txt,
engine-and-state.json and core/library-build.json.
Core remains 38701367e51097b440925a9f76e6b7e16e68d72f87b10c8426aeacbfe73fef59.
Manifest: 11f413d4861be7925d803fb82230036db6a2656cf0547216dc6c91c61028ecac.
Fixture: 5b42dd23626a6a76045c2d3b42b9c9b98ea5b59674c9da6853d23b5f22aa26a3.
