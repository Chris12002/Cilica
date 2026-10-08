# ADR AC0219: Bounded ordinal sorted dictionary

Recorded 2026-10-08 for AC8.75 after AC8.74.

The frozen Dispel operation needs sorted string-key maps, ordered traversal, lookup,
insertion and replacement. Adapt the pinned SortedDictionary/SortedSet insertion and
cursor algorithms into the owned core. Keep game code and all frozen inputs unchanged.
A new exact SortedDictionaryOrdinalConstruction grant selects CreateOrdinal only for
a string-key newobj with a directly proven StringComparer.Ordinal getter. The ordinary
managed factory owns the comparer, pair comparer and tree. Other keys/default/null/
aliased/ignore-case construction remain compile-time diagnostics. Legacy hash dictionary
construction is unchanged and its adjacent differential controls pass.

Reuse pinned red-black insertion, split and rotation order. The first insertion updates
count/version only after allocating its node. A nonempty insertion increments version
before searching, even on a duplicate or later allocation failure, because searching
may rotate the tree. Replacement retains the old key/node and increments version;
an already-current cursor observes the node's updated value. New-key index assignment
performs the same initial find followed by insertion. Null-key checks precede traversal
or writes to the out value. Duplicate errors format both key and proposed value through
the existing bounded text helpers, retaining their support limits.

Concrete SortedDictionary cursors wrap the owned SortedSet cursor. Cursor copies share
a pending-node path, as pinned Stack-based cursors do. The path reserves the pinned
2*floor(log2(count+1)) height bound, clears popped references and supports reset.
Current reads the live node; exhausted/default typed Current is default. Nongeneric
Current checks state; MoveNext/Reset check version first; Dispose does nothing. A default
cursor still faults on MoveNext. Empty interface enumeration uses the mutable
SZGenericArrayEnumerator<pair>, shared with arrays and distinct from Dictionary's
stateless GenericEmptyEnumerator<pair>. Add nominal/nested mappings for SortedDictionary
and SortedSet; public SortedSet constructors and APIs remain absent. Dictionary
interfaces retain their declaration shape; unsupported operations use diagnostic stubs.

The generic node is a reusable public Cilica.Runtime metadata helper with internal
accessors, matching the existing dictionary-entry approach. This satisfies the current
reference-vector visibility rule without broadening application access. It does not
add a public framework SortedSet.Node API. No new native operations, layouts, ABI or
production quota changes. CIL106; object IR63/interface20/native79 retained.

## Validation and review

Prototype IR checks found missing interface flags, private helper access and internal
node-vector visibility. Correct these in the owned library using real interfaces,
internal accessors and the runtime node helper. No application/compiler access policy
was relaxed. One mechanical accessor insertion briefly duplicated properties in Path;
repaired before native validation.

The initial 12-check native run passes 10. Both optimization levels expose the wrong
empty cursor identity; pinned Common EnumerableHelpers confirms array cursor sharing.
Correct it and reset the shared oracle cursor before each fixture observation, making
before/after exhaustion messages deterministic across runs. The corrected 19-check
prototype/adjacent run passes in 108.551s. An added cursor typeof assertion subsequently
hits the existing named-value ldtoken boundary; preserve it as an exact negative rather
than expanding reflection here. Final 21 checks pass in 113.931s on isolated 8f76ed2 plus
this slice, including the committed Char-array interface changes. The later independent
uppercase slice is preserved at integration but is not in these test/probe receipts.

Eight roots, 18 insertion seeds/extremes x 3 modes x 3 repeats x O0/O2 = 2,592 new
native/.NET comparisons. Cover ordinal UTF-16/NUL/surrogate ordering, lookup, missing-key
and duplicate messages, replacement, mutation invalidation, copied/shared cursors,
reset/exhaustion/default cursors, empty identity and sole-cursor retention of reference-
bearing wide values across allocation. Two native quota tests prove empty/nonempty
failed-insertion version behavior, unchanged count, successful retry and exact cleanup.
ASan/UBSan, forced collection, exact and one-short frames and the 2KiB native frame cap
remain enabled. Grant removal and construction/member boundaries stay diagnostic.
The non-sealed value fixture specifically asserts the existing sealed-reference text
restriction; this is a known independent gap, not general TValue formatting support.

Review verifies rotation links/colors against the pinned source, version placement,
current-node semantics, array-path height and shared-copy behavior, null/out exception
order, roots during node/path/text allocation and exact grant/helper signature checks.
Retain the two full pinned tree files and Common EnumerableHelpers (103 originals total),
including MIT headers and SHA-256s in provenance.json.

## Frozen audit

Six frozen assembly hashes match inputs-verified.json. The core is
1695c4e14c54989d1a40963bdf1075c26d7e176903abb57471e9ea61a2e355e1; manifest
b29cd77af5e6ed35a163b06e608851f57054dcc9c3b9cb1fc7d249555b6da17f.
Unchanged AC8.73 diagnostic allowances reach CIL3010: String.LastIndexOf(Char),
SurfaceOccupants.SettleDrops token0x06000f8f IL004a/member0x0a00070d, through
DispelOperation.Run IL0161. Engine charged work 3,299,490 units/17,943ms.
StateLookup validates 15 methods/11,700 units/451ms. Both Emission fields remain null.
The compiler has not yet drained all SortedDictionary helper bodies; the reduced
List<string> value fixture already establishes a later duplicate-text blocker.

Evidence: artifacts/alchemy-sorted-dictionary, -merged, -refresh, -engine and -design.
Full closure admission, native engine execution, canonical event/fingerprint equality
and saved-game replay remain open. This is not an end-to-end Dispel or replay result.
