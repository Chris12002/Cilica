# ADR AC0212: Single-candidate variance for boxed value interfaces

Recorded 2026-10-08 for AC8.68 after AC7a.8/AC8.67.

## Evidence and design

The frozen engine reaches PhysicalDamagePayload construction from environmental
entries. List<PhysicalDamage>.CopyEnumerable calls IEnumerator<PhysicalDamage>.Current
on an accepted boxed List<EnvironmentalEntryDamage>.Enumerator. Boxed assignability
already recognizes variance, but discovery asks the exact interface resolver for the
requested base contract instead of its declared derived contract. Diagnostic-only
receiver labeling and source inspection identify the mismatch.

The independent four-root audit proves exact boxed dispatch already succeeds and a
single boxed covariant source fails with CIL3010. Ordinary list/read-only-list copies
also expose an earlier, independent missing return-interface catalog diagnostic.
Preserve those negative probes; prepared-copy probes explicitly introduce the typed
cursor first. They are bounded composition controls, not a workaround in game code or
a claim that arbitrary List<Base>(List<Derived>) now compiles. Audit evidence:
artifacts/alchemy-boxed-variant-audit (3625157; capture pass 0.964s is not native evidence).

Extend the shared nominal-interface ancestry helper to named non-scalar values.
Extract existing class contract selection and detached validation into shared helpers.
Exact implemented contracts win; otherwise exactly one declared compatible contract
and a nongeneric member are required. The selected contract preserves the open member
identity, visibility and reference/scalar packet compatibility. Retain that declaration
on boxed targets and validate it before the existing exact boxed body/receiver checks.
Logical and scalar plans enforce the same rule. No value/byref adaptation, boxing copy,
primitive interface expansion, multiple-candidate policy or new native operation.
The actual boxed payload stays borrowed, including across allocating callbacks.

CIL100 and interface-plan19 record the newly admitted packet. Object IR60/native79,
core bodies, grants and all production allowances are unchanged.

## Validation and review

Two initial exact/covariant native checks pass in 5.334s. Expanded translation validates
all ten supported probes at both stages in 1.264s. The 55-check run passes 48 in 63.393s;
seven detached mutations initially stop in test JSON serialization because default
EnumConstants cannot be enumerated. Omit that default-only test property so deserialization
preserves its default value; all seven mutations then pass in 1.247s. Thus 55 distinct
checks pass, not 62. No production serializer change or full-suite claim is made.

Ten positive probes x49 pairs x3 repeats xO0/O2 give 2,940 new native/.NET comparisons.
Coverage: exact/covariant/contravariant calls, inherited and explicit implementations,
exact-match precedence, mutable boxes, reference-bearing payloads, allocating callbacks,
managed faults/nulls and prepared List/read-only-list copies. Existing class variance,
boxed-interface validation and native boxed controls pass unchanged. JSON-detached
logical/scalar plans reject missing or forged owner/token/result/parameter/visibility/
name evidence. Ambiguous/generic variants and unprepared return-catalog cases retain
precise negatives. ASan/UBSan, forced collection, exact root/borrow cleanup, exact and
one-short frame budgets, 2 KiB frame warnings and bounded subprocesses remain enabled.

Review checked shared exact-precedence policy, all declared interface ancestors,
selected method identity, packet direction for covariance/contravariance, unchanged
boxed payload ownership and scalar lowering, complete receiver maps and budget accounting.
All 94 pinned upstream hashes and six frozen assembly hashes verify; no game source or
input is rebuilt. Main 48ecf14 remains an ancestor. Other lane ArraySegment work stays
separate. Evidence: artifacts/alchemy-boxed-variant{,-refresh,-engine}.

## Frozen follow-up

With existing diagnostic allowances, the engine finishes its first interface-planning
pass, then hits the combined method/declaration/mapping limit: 10,797/8,192; 7,621 bodies,
14 abstract declarations, 710 interface declarations, 3,533 mapping facts and 50 exception
declarations (identities overlap). Types 2,398; instructions 196,709; parameters 15,529;
locals 8,971. Scan 2,500,677 aggregate metadata units/10,371ms. StateLookup remains
15 methods/11,616 units/466ms. This is not completion of later dispatch/discovery passes,
IR validation, emission or execution. Core
bb90c5bc200ea0868b736424616921d033d190079dc444933d369f4b5acd3409;
manifest d9a1ab0108ff3f6fef6e01a6b3c1256fd124d17451dba531bc9666b9b657046c.
Full engine, unchanged fingerprints and saved-game replay remain unfinished.
