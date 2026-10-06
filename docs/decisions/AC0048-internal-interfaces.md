# ADR AC0048: Top-level internal interfaces

Accepted 2026-10-07 for bounded AC8.8.

Alchemy's internal GameStateScope implements internal IEffectScope, including an
explicit RefreshRoster mapping. Resolving its virtual CurrentRound setter validates
that interface hierarchy before selecting the class slot. The prior public-only
metadata gate rejected it even though both types belong to the same input image.

The explicit closed metadata resolver now validates top-level internal interface
owners with ordinary public abstract instance declarations. Exact mappings, method
flags, inherited slots, MethodImpl restrictions, generic constraints and bounded
owner/member traversal remain unchanged. The older nongeneric resolver remains
public-only. Metadata selection remains evidence, not execution authority.

In the compiler, only the integer-object catalog admits NotPublic top-level
interfaces. Their existing ExternallyVisible flag must be false; public/nested-public
contracts retain their public visibility requirement. Interface declaration facts
must match catalog visibility. Call and type-test validation checks the caller's
content-hash identity against internal contract ownership at both IR stages.
Explicit foreign CIL names still fail the existing lexical-origin check. Generic
value signatures use the same admitted typed dispatch path. No access to nonpublic
nested interfaces, friend assemblies, default/static bodies or cross-image internal
calls is added. No field, runtime object or library intrinsic is introduced.

Seven probes supply 2,058 repeated O0/O2 native/.NET comparisons on macOS ARM64 under
ASan/UBSan, forced GC, exact cleanup and frame bounds. They cover implicit/explicit
mapping, inherited internal contracts, overridden public class slots, generic value
payloads, null calls, casts and interface arrays. The closed metadata target agrees
with the authored .NET interface map; exact budget succeeds and one less fails.
The old resolver rejects the same internal contract.

Detached logical/scalar checks reject a caller moved to a different image, while
a matching public-interface call remains valid. Visibility promotion, private-nested
flags, older catalogs and contradictory interface declaration facts are rejected.
A metadata-only foreign CIL fixture cannot name the internal contract. 119 selected
checks pass in the regression run, then the corrected diagnostic assertion and
foreign-CIL test pass separately. Initial cross-image fixture accidentally used the
selected Object owner (rejected by an unrelated intrinsic-owner rule); it was fixed
to a separate static owner, with the successful public control proving the fixture.
Initial and corrected logs remain in artifacts/alchemy-internal-interfaces.

Review covered class-slot resolution versus interface authorization, exact source
identity, public control behavior, visibility propagation into the scalar catalog,
casts/arrays, owner flags, signature matching and unchanged metadata work limits.
Profiles advance to object-layout-v19, object-operations-ir-v20, interface-dispatch-
plan-v8, integer-managed-objects-cil-v10 and native v47. Closed interface/class and
constrained metadata target profiles are v2; physical native layouts are unchanged.

The frozen engine now stops at protected DomainEvent::.ctor from
PriorityPassedEvent::.ctor IL_0001 (CIL2001). State discovery still reports 257/256
identities. Complete engine execution and saved-game replay remain unverified.
