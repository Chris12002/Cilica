# ADR AC0067: Declaration-only delegate and exception classification

Accepted 2026-10-07 for AC8.17.

Ordinary method owners were consuming structural snapshot capacity merely to
answer whether they were delegates or exceptions. ReadTypeBase now reads the owned
TypeDef's direct base signature, charges the existing metadata work ledger, and
uses the existing bounded signature decoder. It does not inspect fields, interfaces,
initializers or layout and conveys no execution permission.

Delegate classification combines direct-base and declaration-name facts; positive
candidates still require the exact selected MulticastDelegate identity and the
complete closed structural snapshot and signature checks. Exception classification
walks base-only declarations until it reaches a known exception or selected resource.
Positive ancestry then receives the unchanged full shape/member/constructor checks.
Cycles and the 64-level ancestry bound remain enforced. Runtime resources retain
independent exact grants. Inline/enum layout validation is unchanged.

Structural snapshots are reused where available. A separate base-fact cache stores
at most 256 entries; additional reads are not retained and charge metadata work
again. The existing 256 structural/runtime-type, field, metadata-work, signature and
native resource limits are unchanged. This follows AC8.11's declaration-only access
cache rather than increasing runtime capacity or dropping required layouts.

The first reduced static-owner test showed that changing delegate classification
alone left the exception ancestry path reading the same unused fields. Both now
use the declaration reader. A 257-owner fixture plus a repeated last owner passes
294 repeated O0/O2 native comparisons, forced collection, ASan/UBSan and exact
cleanup. Each unused owner contains a malformed field; direct structural reads and
actual delegate/exception candidates still reject malformed/nonempty storage.
Tests also cover exact row budgets, generic bases, malformed/deep signatures,
resolver ownership/disposal, foreign marker identity, ancestry cycles/depth and the
unchanged 241-enum/15-runtime-root snapshot boundary. All 25 focused checks pass.

All 61 adjacent structural/delegate/exception checks and 19 public-baseline checks
pass (105 distinct selected checks). A stale GenericDelegateTests diagnostic
assertion used pre-inline-delegate wording; it now checks the current explicit
wide/narrow/byref rejection. No unsupported signature was admitted. Logs:
artifacts/alchemy-delegate-classification/ac8q2-*, ac8q3-*, ac8q4-*.
Review checked ownership before budget consumption, bounded caches and signature
reads, preserved positive validation and error behavior. No IR/native profile change.

Frozen EngineStep advances past the metadata snapshot bound to CIL3010:
Enumerable.Contains<String> in BorrowedShieldPayload.IsLegalSelection's callback
at IL_001c, reached through IsLegalEntrySelection. StateLookup remains at 257/256
discovered identities; full native engine/replay is unverified. The public baseline
remains 31 native controls/16 gaps. Core SHA-256 remains
ddb0ab1d2678883100366287e4fa109bcb740e28cee6856f81302c19cdcc9c31;
manifest 2b7c550b0f066144e3859be316f90485340c4f2ba23c8b3d847e8970855662a3.
