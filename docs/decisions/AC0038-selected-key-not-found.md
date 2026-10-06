# ADR AC0038: Selected KeyNotFoundException

Accepted 2026-10-07 for bounded AC8.6.

The frozen engine formats a missing-player message and constructs the framework
KeyNotFoundException. Admit its exact selected public nongeneric definition as a
lazy resource exception directly derived from selected SystemException. Its layout
and two constructors each require an independent manifest grant. The compiler uses
existing exception payloads and roots; it does not execute framework initializers.
The pinned runtime source and resource establish default/null-message behavior.
No collection algorithms or game rules change. Inner constructors, HResult and
serialization remain outside this surface.

Add KeyNotFound at the end of the resource enum and validate its selected image,
fully qualified name and parent at both detached stages. Managed exception IR and
scalar exception IR become v4; native v42 and layout v17 are unchanged. Review
checked constructor admission, null/empty distinctions, resource identity, existing
rooted storage, and custom-subclass rejection where constructor behavior is unproven.

Four probes pass 1,176 repeated O0/O2 native/.NET comparisons with forced collection,
ASan/UBSan, strict cleanup and frame quotas. Detached name/parent/resource mutations
fail in logical and scalar IR. All three grant removals and the unsupported inner
constructor fail. Two focused runs pass 45 and 49 checks (overlapping selections).
The first run exposed a stale boxed-Guid negative expectation from earlier work;
a dedicated O0/O2 comparison now verifies that supported equality path (294 cases).
Guid.Parse still fails admission. Initial and corrected logs are retained.

Fresh frozen-input evidence in artifacts/alchemy-key-not-found advances EngineStep
to the missing List<DomainEvent>(IEnumerable<DomainEvent>) constructor in
FinalizeAccepted IL_0005, requested from Step IL_0780. StateLookup still reports
257/256 discovered identities, not a completed emitted catalog. No full engine or
saved-game replay execution is claimed. Historical audit evidence stays unchanged.
