# UInt32 vectors and exact signed/unsigned helper views

Recorded 2026-10-08 for compatibility batch 007.

Pinned Crc32.NET static roots allocate a 4096-element UInt32 table. The independent
UnsignedTable probe first confirmed CIL2001 at vector admission. Append UInt32 to
the element-kind enum, retaining old serialized discriminants. Use the existing
UInt32 carrier and integer bit operations; ldelem.u4, ldelem.i4 and exact typed
loads preserve the same 32 stack bits. Widening remains an explicit IL conversion.
Literal-null unsigned loads prepare the existing Int32 fallback descriptor, so
null faults do not depend on an allocation elsewhere in the method. A failing
independent reduction caught and fixed this missing discovery edge.
No host pointer reinterpretation, collector operation, intrinsic or quota changes.

UInt32 arrays retain distinct runtime descriptors, native symbols, cast names,
Empty identities and generic specialization. Like the existing Int32/enum array
family, UInt32 arrays have compatible signed/enum array and interface views; this
is assignment compatibility, not loss of runtime identity. Native copy-compatible
pairs include the added family member. Constant data uses the existing four-byte
little-endian bit decoding and 256-element bound.

Generic array helpers use the requested element identity; nongeneric enumeration
uses the actual array identity. Prepare UInt32 helper views even when the only
physical allocation is Int32[]. Detached validation rejects substitution of an
Int32 helper for a UInt32 contract and conversely. This matters for boxed Current
and per-T caches despite both values sharing an Int32 IR carrier. Copy/enumeration
admission now recognizes exact UInt32; narrower and other unsigned families keep
existing diagnostics. Scalar ldelema remains unsupported, including compound array
assignment syntax. An independent negative regression preserves that boundary.

Integer CIL85, IR55, layout28, interface17 and native77 record this slice. CIL84 was
reserved by the concurrent ranged-search work; its files remain owned by Review
milestone 6. Validation uses isolated outputs and records the actual shared input
hashes. No other chat's library/source-list changes are staged with this slice.

Review examined every array-kind dispatch, exact storage width, type closure,
helper preparation/pruning, cast messages, boxing contracts, empty identity,
static bit decoding, exception order and native allocation/root cleanup. The new
suite compares independently authored C#/raw IL against normal .NET at O0/O2 with
ASan/UBSan and forced collection, plus malformed logical/scalar packets.
See ../compatibility-candidates/batch-007-results.md for final counts and supporting evidence.

Crc32.NET remains source-mapped and blocked: its mixed int+uint table index emits
conv.ovf.i, reproduced independently as CIL2001. Checked native-integer indexing
requires a separate width/overflow design. HashAlgorithm owner metadata and the
full singleton/candidate closure are still unverified. No candidate code, project,
build target, generator, test or binary was executed or downloaded in this batch.
DiffPlex selected sources remain mapped but await the separate execution approval.
