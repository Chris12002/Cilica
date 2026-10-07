# ADR AC0100: Dictionary checked insertion and indexed reads

Recorded 2026-10-07 for AC3.14.

## Evidence and decision

The committed 6e0c46e compiler with tuple mappings and unchanged diagnostic-only
1,024-type/1,024-method/262,144-metadata limits reaches Dictionary.Add in
UnitRoster::.ctor IL_007e, for PlayerId/ParticipantState, after 105,378 metadata
units. A second site uses UnitId/UnitState. StateLookup stays at eight methods
and 9,917 units. All six frozen input hashes still match. No native engine
emission/execution was attempted; see artifacts/alchemy-long-tuples-{refresh,engine}.

Implement Add and the read indexer using existing bucket/entry lookup and insertion.
Duplicate Add preserves stored data and version; successful Add retains ordinary
growth/free-slot reuse. Missing reads throw the pinned key-specific message.
Both null-key paths validate before collection access/text.

Keep error text outside the shared insertion body. Otherwise even TryAdd/setter
closures would demand key-formatting support. Change its private key parameter
to byref so Add receives the key mutated by the constrained hash callback, matching
ThrowOnExisting inside pinned TryInsert. The caller's original value remains
unchanged. Find hashes a copy, so the indexer formats its original lookup key.

The exactly granted private DictionaryKeyText<T>.Render declaration redirects to
ordinary managed code: invariant integer formatting for Int32/UInt32/Int64/UInt64,
string identity, or a copied named-value box through the existing exact renderer.
Each actual ordinary ToString target must be implemented; general IFormattable and
reference-key formatting remain diagnostic. Managed concatenation preserves
content/callback order but makes no .NET allocation-count/threshold claim.
No general scalar String.Format support is implied. Integer CIL v32; native
v60/object IR v33/layout v22 and all production resource limits remain unchanged.

## Validation and review

Eleven probes, 84 input pairs each, three repetitions at O0/O2: 5,544 native/.NET
comparisons. They cover signed/unsigned wide keys, UTF-16 strings, null order,
duplicate/missing messages, growth/free reuse, unchanged duplicate version,
reference-bearing values, mutable hash/text callbacks, null text, callback faults
and ToString adding a second entry to the same dictionary. Existing mutation,
view/cycle, enumeration and mutable key regressions pass with forced GC, ASan/UBSan,
2,048-byte frames and unchanged 30-second subprocess caps. Exact grant denial,
private callers, unsupported IFormattable and nominal API bindings are checked.

The first eleven checks passed. The 39-check regression run had 37 passes and two
lazy-fixture failures at a stale hostile-caller diagnostic expectation. Type
visibility now rejects that private call before the key-intrinsic guard; the
expectation was updated to its precise lexical-scope diagnostic. The final
15-check dictionary/callback run passes, followed by the public baseline on the
final fixture binary. All 52 selected distinct checks pass. Public DictionaryAdd
and DictionaryRead advance the baseline to 42 native controls and five gaps.

Review checked hash-copy differences, exception order, retained roots and cyclic
cleanup, reentrant text callbacks, nonmutation on duplicate insertion, integer
width/sign selection and the preserved TryAdd/setter boundary. Local main remains
an ancestor at 48ecf14. Evidence: artifacts/alchemy-dictionary-add-read, including
ac3-14{a,b,c,d}-tests.log and hashes.json.
Core: 501bc368c7e29ffc27c457aeb48d3bafb36c721c658a8ab4ec883c59fe137af5.
Manifest: 72387f7a9621eb894ba0d6ee5fdd17d3bc509d379f92e750ec993ec1a9810b67.
Fixture: 0280edff232823cbf90be7f8db4d84af38c3b9ca00a4c9c75cec7a15eccba456.
Full engine/replay completion still requires a refreshed closure probe.
