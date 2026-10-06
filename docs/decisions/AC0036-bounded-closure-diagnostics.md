# ADR AC0036: Preserve bounded closure failure evidence

Accepted 2026-10-07 for AC8.5 diagnostics/audit.

Nested member-resolution failures now retain the inner instruction's method,
token, specialization and IL offset, the original dependency-resolution location,
and the requesting caller. Resolver wrappers append missing outer context rather
than replacing the inner location. Existing diagnostic IDs/messages remain.

The combined type-limit diagnostic reports the observed identity count, distinct
nominal declarations, closed generic identities, vector identities, cached type
snapshots and fields read. It includes a deterministic, bounded per-image summary
and the failing identity before the existing inclusion path. These are partial
discovery counts, not emitted layouts or proof of the full required budget. The
256-type limit, metadata budgets and native limits are unchanged.

85 focused metadata/resolver checks pass after preserving both inner and outer
resolution context; all 21 public baseline checks passed during the preceding
run. New regressions require the actual inner IL offset and requesting call, and
pin observed 257/256 identity counts while preserving exact-limit success. Review
checked bounded diagnostic work, deterministic ordering and unchanged admission.
Private evidence and logs are in artifacts/alchemy-closure-diagnostics.

Frozen EngineStep reaches GameState.GetPlayer token 0x06000136 IL_0020, which calls
String.Format(string, object, object) with PlayerId and MatchId boxes. Step IL_05a8
is the requesting call, not the format call itself. The frozen state catalog stops
at 257 identities: 143 distinct nominal declarations, 122 closed generic identities,
zero vectors, 245 cached snapshots and 541 read field declarations. By image: owned
core 145, Alchemy.Engine 77, and the four other dependencies 14/13/6/2. A catalog
accounting audit is needed before any capacity decision. No full replay ran.
