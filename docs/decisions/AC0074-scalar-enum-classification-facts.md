# ADR AC0074: Validated scalar-enum facts without retained layouts

Accepted 2026-10-07 for AC8.21.

An unboxed Int32 enum is represented by Int32 storage. Discovery and catalog
classification previously retained its complete metadata snapshot; catalog reads
also charged its nominal identity to the executable type closure. This made 257
otherwise simple enum locals or fields fail the 256-type bound despite needing no
enum runtime layouts. AC0056's original enum-locals diagnostic is historical; its
fixture is now the positive regression for this change.

Full structural enum validation still runs after exact selected-Enum direct-base
classification. Discovery retains at most 256 positive classification facts, not
field/interface snapshots. Later classifications beyond that cache are revalidated
and charged to the same metadata-work ledger. Lexical operand visibility uses the
existing bounded declaration facts. Catalog classification likewise reads enum
metadata transiently, retains bounded positive facts, and excludes scalar-only
aliases from layout identity and snapshot storage. Negative classifications use
base facts. Boxed enums still enter ordinary validated executable layouts.

No runtime-type, cached-layout, field, signature, native frame or subprocess bound
is increased. Transient reads charge field and shared metadata work limits; full
layout reads retain all prior validation and budget accounting. The selected Enum
identity and exact value__/constant/visibility/initialization shape checks are
unchanged. Unsupported wide enum and enum-array cases remain diagnostic.

The local/field reductions each pass 294 repeated O0/O2 native comparisons (588
combined), with forced GC, ASan/UBSan and exact cleanup. Late malformed enums fail
after the fact cache fills. Explicit metadata-work and field exhaustion checks
remain diagnostic. Adjacent checks include boxed enums, private/internal access,
closed generic classification and actual reference-layout bounds.

Review checked cache saturation, shared work charging, field limits, no retention
through the catalog observer, operand access, exact selected base and boxed-enum
layout admission. No IR schema, native runtime algorithm, library grant or compiler
profile change is required.

All 99 distinct selected checks pass (70 in ac8u2 plus 36 in ac8u3, seven overlap).
The public baseline remains 31 native controls and 16 diagnostic gaps. Logs and
unchanged-input refresh: artifacts/alchemy-enum-facts/.

Frozen EngineStep clears UnitRoster and reaches missing Enum.IsDefined at
PlayerState::.ctor IL_0017 via PrepareForfeit/PlayerState.Forfeit. StateLookup reaches
a later catalog inclusion before 257/256 identities (133 nominal declarations,
139 closed identities, 224 cached snapshots). It still cannot execute. Full engine
and saved-game replay remain unverified. Core SHA-256 remains
69356718f55f03402865ad808087b2e78e0bbe321fed5ff6efca2a8f180a2b31;
manifest: 95f38b054efa16a5d00a89c94b1317f547e6b0d41af1574709d39b777f32fb3b.
