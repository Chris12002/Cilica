# ADR AC0034: Detached closed generic identity evidence

Accepted 2026-10-06 for AC8.3 metadata foundation.

ClosedGenericEvidence snapshots supported closed argument syntax into immutable
primitive, named, generic and vector shapes. It carries image hashes and TypeDef
tokens without retaining readers. Its bounded key calculation reproduces the
existing ClosedGenericKey byte representation; the existing binder and key
implementation remain unchanged. Capture still checks generic declaration arity
and rejects open arguments. Detached validation checks shape, token/hash form,
initialized lists and the shared 4,096-node/64-depth bound before hashing.

This is identity evidence, not independent access authority. The following array
slice must match both owner and method specialization keys and the exact original
variable/instruction before using it. A matching generic argument alone must not
grant all private-type access within a method. No native behavior or profile is
changed by this foundation.

28 selected evidence and existing generic/origin checks pass. Tests compare keys
against the existing metadata implementation, use the snapshot after disposing
all metadata, distinguish ordered argument lists, reject open/invalid arities and
malformed detached identities, and exercise exact node/depth boundaries. The
initial expected node count was corrected from eleven to ten; no limit changed.
Review checked immutable reader-free fields, bounded recursion/allocation, empty
context identity and byte-compatible ordered encoding. Logs are under
artifacts/alchemy-generic-array-evidence/validation. Full replay is unfinished.
