# Bounded Int32 vector element locations

DiffPlex at f500e73f28e28813f942f1f16dec5a34b40e48e3 takes an Int32 element
address for Equals in BuildModificationData (token 0x06000010, IL_001e). The
independent EqualsElement and EqualsAfterArgumentMutation reductions preserve
that unsupported shape without retaining candidate source. Eagerly copying the
receiver before evaluating arguments would be incorrect: arguments can mutate
that same element, and the method must observe the new value.

## Design and boundaries

The integer-object profile admits ldelema with an exact Int32 vector view. It
produces the existing AddressElement and scalar AddressArraySlot operations; the
existing nonescaping managed-location carrier keeps owner, index and slot zero.
No scalar read is moved across argument evaluation. Existing phase-A LocationOwner
roots keep the original array alive after aliases are replaced and across managed
calls or allocations. Loads, writes, readonly checks, synchronous reborrows and
closed generic calls use the existing location protocol. Native/raw address
conversion and older execution profiles remain diagnostic.

Both IR stages check the exact admitted view, Int32 result element, slot zero,
Int32 index, fault edge and absence of narrowing. Other declared scalar array
views remain unsupported, including UInt32, Int64, enum, Boolean, byte and char.
Int32 views over actual UInt32 or Int32-enum arrays follow the existing CLI reduced
array-type relation, confirmed against normal .NET. Direct enum/unsigned element
addresses are not newly admitted.

The emitted descriptor helper recognizes only registered Int32 views and uses the
existing exact descriptor predicate for compatible owners. It never infers type
compatibility from array_kind=1: byte, char and Boolean use that same internal
buffer representation. The runtime validates instance ownership, registered type,
backing storage and bounds before indexing the owner's uint32 buffer. No pointer
reinterpretation to another element type, new descriptor layout, grant, allocator,
quota or runtime capability is added. Reference covariance checks and invariant
value-array locations remain unchanged. Null and bounds faults occur at address
formation, before argument evaluation; argument exceptions retain normal order.
Private malformed/stale/foreign carriers fail validation, and readonly carriers
cannot be written.

CIL admission advances 91→92, object IR 58→59 and native output 78→79. Value scalar
lowering and root assignment already implement the needed generic carrier behavior
and are unchanged. This slice is coordinated with Review milestone 6's separate
GroupBy work; its compiler/library implementation is not included here.

## Validation and review

Eight authored semantic roots supply 2,352 repeated native/.NET observations with
O0/O2, forced collection, ASan/UBSan and exact frame/root cleanup. They cover element
Equals, post-argument reads, array replacement and value copying, writable and
readonly reborrows, closed generic writes, signed/unsigned/enum aliases and ordered
null/bounds/argument exceptions. Seven rejection cases retain other scalar views,
native escape and legacy-profile boundaries. Six malformed IR cases check both
stages where applicable; two native tests check slot/index/width/narrowing, readonly,
foreign/stale owners, marking state and incompatible array representations. All 31
checks pass. Generated C++ review confirms rooted owners, guarded uint32 access,
no early receiver load and unchanged reference/value array fault handling.

Evidence is in artifacts/compatibility-int32-array. The unchanged candidate must
still pass its independently run .NET oracle and native transcript comparisons;
these reduced tests alone are not a DiffPlex compatibility claim. Earlier trials
and their exact compiler/source pins remain retained.

The adjacent selection initially passed 153/155 checks. Its two allocation-only
rejection expectations also fail on an isolated export of pre-change 90291f9:
Boolean and Int64 array allocation were already supported. The tests now assert
scalar layout identity and positive native execution (588 further comparisons).
All eight value-vector checks pass on rerun, yielding 186 distinct passing checks
across the new and adjacent selections. Original baseline/failure logs are kept;
no runtime change was made to satisfy the stale expectations.
