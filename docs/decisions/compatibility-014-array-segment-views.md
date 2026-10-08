# Bounded ArraySegment construction and views

Independent CircularBuffer reductions need ArraySegment<T> constructors and Array,
Offset and Count. Add an owned readonly managed struct with the backing vector and
two Int32 fields. Default values retain null/zero fields; copies share backing identity.
Both constructors preserve pinned dotnet/runtime 79d0c463 behavior. Validation order is
null array, negative offset, negative count, then complete range. Validate offset before
subtracting, and compare count to remaining length to avoid overflow. Resource strings
match pinned ThrowHelper/Strings. Full original MIT files and hashes are retained.

Keep IList<T>/IReadOnlyList<T> declarations: their implicit Count implementation is
virtual/final in the reference contract. Omitting interfaces causes a correct CIL3014
binding rejection, not permission to weaken metadata checks. Other interface bodies
invoke the established UnsupportedLibraryMember diagnostic. Existing ordinary boxing
and interface dispatch can execute Count, so that path receives positive coverage.
Public indexer, Empty, Slice, CopyTo, equality, hash/text and enumeration remain
unsupported. No new interface lowering or special ArraySegment intrinsic is added.

The source list and System.ArraySegment`1 mapping are explicit in CoreLibraryBuildTests
and documented for library consumers. CIL99/IR60/native79, grants, layout machinery,
collector and limits remain unchanged. Existing value storage/copies/boxing trace the
backing reference. Empty segments must keep the whole backing array and all payloads
alive, even with offset at the end; GC tests require precisely that behavior across
copies and clearing. No special slice-based tracing or native pointer is introduced.

Eight semantic roots plus boxed Count and the original candidate reduction yield 2,940
new repeated O0/O2 native/.NET comparisons; four GC variants and adjacent Copy/Clear
checks pass. Final 48/48; initial metadata/negative-expectation failures are retained
in batch012 evidence. Review checked overflow, fault order, default/empty identity,
value/reference/narrow/wide storage, interface metadata and exact retained roots.

The separately approved frozen CircularBuffer trial did not execute candidate code:
translation stopped at same-owner constructor chaining before oracle/native generation.
An owned generic class reproduces that CIL2001 gate at IL_0008. BaseCall currently permits
base-owner initialization, not same-owner chains; keep that separate from segment support.
A future fix needs constructor-state and malformed-IR tests, followed by a newly frozen
trial proposal. Full candidate compatibility, other segment APIs and Alchemy replay
remain unverified. No runtime ownership optimization was adopted.

Profile correction: the retained tested compiler binary embeds CIL99; the earlier
CIL98 label missed the concurrent peer update. Original logs and hashes are unchanged.
