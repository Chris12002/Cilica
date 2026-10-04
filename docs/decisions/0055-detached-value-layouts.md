# ADR 0055: Detached inline value layouts

Status: Accepted for layout planning only. Recorded 2026-10-04.

## Decision

Reuse content-scoped TypeDef and declaring FieldDef identities. Add an explicit
`ObjectStorage.Value` kind, distinct from Int32 and managed references. Existing
object/executable validators reject it until their own value-lowering slice.
`ValueTypeCatalog` holds the inline closure and a separately validated existing
reference catalog. The selected ValueType base has an explicit identity; it is
not a concrete allocatable class. This detached API does not authenticate the
assertion that a declaration actually derives from that selected base.

Support sealed nongeneric structs with automatic or default sequential managed
layout. Fields hold Int32, a reference in the supplied object catalog, or another
included value. No inheritance, explicit offsets, packing, native-size promise,
static storage, byref fields, boxing or method execution is implied.

An iterative topological planner rejects recursive inline containment. Scalar
paths retain all nested FieldDef identities, including repeated embeddings of
the same struct. Copying a struct will copy these slots; a reference slot keeps
the same object identity. Reference paths are derived from storage kinds. Empty
values have distinct type identities and no scalar slots; an empty embedding
still consumes expansion space. Native sizes/offsets remain a later lowering.

The combined class/interface/vector/string/value identities plus the selected
ValueType base share 256 types. Declared fields share 4,096 entries and unique
image-scoped tokens. Across all produced plans, inline field expansion is limited
to 65,536, materialized path components to 262,144, and nesting to 64 fields.
Check before expanding each field/path. These separate limits prevent a small
declaration graph from requiring exponential compiler memory, including a graph
whose leaves are empty structs. No consumable partial plan escapes on failure.

## Review and validation

Review covers identity collisions, inline cycles, repeated fields, empty values,
integer overflow in budget arithmetic and rejection by the existing executable
profile. Targeted tests cover nested reference paths, malformed/default arrays,
field/token ownership, exact/excess depth/type/field limits, path storage and
exponential expansion. There is no generated C++ change and no native struct
execution claim. Metadata discovery and executable value semantics are subsequent
slices in milestone 5.
