# ADR 0142: Array interface relationships and managed readers

Status: Accepted for the bounded integer-object profile

## Decision

Record selected-runtime array interface contracts explicitly in detached catalogs.
Authenticate definitions against the selected library, never application names.
Vectors implement the selected nongeneric IEnumerable and generic IEnumerable,
ICollection, IList, IReadOnlyCollection and IReadOnlyList contracts when their
element types are compatible. Reference vectors use runtime array covariance,
including invariant generic list contracts. Int32 vectors admit Int32 and UInt32
interface arguments, preserving bits. Enum element equivalence remains diagnostic;
other unmodeled nongeneric Array contracts retain the previous rejection.

For Count and indexed reads, specialize the owned ArrayInterface<T> managed helper
on the actual vector element, then use existing descriptor-based interface dispatch.
Reference returns can widen to the requested interface argument without changing
object identity or storage. Each helper needs an independently granted exact member.
Array facts are separate from class slot facts; detached validation checks selected
identity, operation, vector receiver, signature, complete coverage and body closure.
Static helper bodies are translated normally, including rooting and checked loads.

The read helper retains the pinned SZArrayHelper unsigned index check and
IndexMustBeLess resource with parameter name index. No unsafe receiver alias is
needed. Unsupported enumeration or mutation calls fail compilation. This slice
does not provide Array.Empty, value/wide vectors, enum storage or public collections.

Object layout v7, interface dispatch plan v2 and native profile v24 describe these
assertions. Catalog comparison includes the array-contract evidence. The reference
source and license are retained in provenance.json.

## Evidence and review

Six probes exercise casts, Count/index calls, covariant string/object identity,
array-store checks, Int32/UInt32 reads, null/bounds/message behavior and mixed
class/vector dispatch. They provide 1,764 repeated local native comparisons at O0
and O2 with sanitizers and collection stress. Negative checks remove each grant,
reject unsupported enum/enumerator operations and corrupt detached dispatch facts.

Review corrected the pinned exception resource and signed/unsigned array-interface
equivalence. The full build and final 112-check composed run pass. Review covered
bounded metadata and relation closure, selected identity, complete dispatch targets,
receiver/return types and rooting; no remaining issue was found in the admitted
scope. The reviewed core fixture has 208 inputs and 73 grants; it is evidence, not
production approval. Main remains an ancestor. Full kernel equivalence is pending.
