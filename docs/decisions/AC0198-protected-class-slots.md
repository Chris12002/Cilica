# ADR AC0198: Same-assembly protected class slots

Recorded 2026-10-08 for AC8.63, following AC3.33.

Frozen GameOccurrence.WriteCanonical calls its own protected abstract WriteContext
at IL_0078. Its lexical caller already has access. Extend the closed class resolver
and integer-object dispatch validators to Family slots, preserving independent
caller authorization. Legacy metadata/class profiles and interface mappings do not
change. Require exact protected access on reuse, same slot/implementation/receiver
assembly, ordinary managed signatures, valid bodyless abstract declarations,
nearest implementation, newslot separation and final override checks. Protected
internal/private-protected and cross-assembly slots remain unsupported. Inherited
protected-call authorization remains a separate extension; no broad access bypass.

The shared detached dispatch rules enforce the same restrictions after lowering.
Closed class target v5, CIL89 and object IR58 record the admission extension. No
new IR operation, native implementation, grant or limit change; native78 remains.

Validation: 107 protected/internal/closed/legacy slot checks pass in 21.499s;
69 adjacent abstract/value/interface and public compatibility checks pass in
110.503s (176 distinct checks). Eight new execution probes supply 3024 repeated
O0/O2 native/.NET comparisons for scalar/Int64/reference/void results, generic
owners, inheritance, hidden slots, re-abstraction/final implementations, null and
argument faults, and allocation while retaining inputs. Both IR stages reject
malformed abstract declarations, flags/access/maps and calls missing required null
checks/dispatch. Metadata regressions cover all six access forms, exact budgets,
legacy rejection and foreign callers/receivers. ASan/UBSan, forced collection,
frame exhaustion, exact root cleanup and process bounds remain enabled.

Review checked Family admission is closed-profile-only, access checks are unchanged,
strict override flags remain internal-only, signature and dispatch coverage checks
remain mandatory, and tests use wrappers owned by protected declaration classes.
Local main 48ecf14 remains an ancestor. Snapshot b2983b7 plus owned changes excludes
concurrent candidate edits. Evidence: artifacts/alchemy-protected-slot{,-refresh,-engine}.
Core 0949180ad3a6d266ca2eae2fe5cde347c1e4eff72beae1bfb50cbb3ca44aca8f.
Manifest 1bf430dc5d35b89b62021177ebfb4fe6da098803cf1e8fdb8d5914e1795181c3.
All six frozen input hashes verify. Engine discovery advances to ComparerCache<T>
default factory through explicit ordinal ThenBy in ReconcileControlOpportunity
0x06000021 IL_00a2, charging 1240076 metadata units/4809ms. StateLookup translates
15 methods/11616 units/461ms. No engine code is emitted/executed. Full engine,
unchanged fingerprint and saved-game replay remain unfinished.
