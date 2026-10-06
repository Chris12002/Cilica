# ADR AC0070: Base-only rejection of non-enum candidates

Accepted 2026-10-07 for AC8.19.

An assembly-internal type name used by a static call reached enum classification
before its lexical access check. Classification decoded fields even when the type
was an ordinary class. EnumScalar now checks the exact selected Enum direct base
using the existing bounded declaration reader before requesting a full snapshot.
Actual enum candidates retain every field, layout, initializer and lexical shape
check. Only positive validated enums enter the enum cache; negative answers reuse
the bounded base cache, so repeated non-enum names cannot grow a second unbounded
cache. Runtime type, structural snapshot, field, metadata and native limits stay fixed.

The existing 257-static-owner regression now also covers assembly-internal owners
and a repeated uncached final owner, with malformed unused fields. Both new native
runs pass 294 repeated comparisons at O0/O2, forced GC, ASan/UBSan and exact cleanup.
All 76 selected checks pass: base and argument classification, access/ownership,
exact enum snapshot boundary, malformed/hidden/nested enum cases, scalar storage
and internal interfaces. Actual reachable enum/value storage still requires full
validation. Evidence: artifacts/alchemy-enum-classification/ac8s1-* (reproduction)
and ac8s2-* (validation). Review verified selected-base identity, unchanged positive
validation, cache bounds and metadata charging. No new grants or profiles.

Frozen EngineStep passes the snapshot blocker and reaches missing
String.Concat(String, String, String, String) in ShieldResponses.FormatOption at
IL_003a, reached through PhysicalDamagePayload.IsLegalSelection IL_010f.
StateLookup retains its 257/256 partial catalog failure. Full engine/replay is
unverified; the public baseline stays at 31 native controls/16 gaps.
Evidence: artifacts/alchemy-enum-classification/engine-and-state.json and
format-option-il.json. Core SHA-256 remains
9f0f67ed78737d3f7843fc4e20b3cc95b41e6e17379e3b5b04a3765e1ec0cef4;
manifest: be6c11d0f8679ee3819f359b28d15d21007de04976170541a25a7bc160002259.
