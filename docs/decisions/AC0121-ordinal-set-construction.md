# ADR AC0121: Explicit ordinal string-set construction

Recorded 2026-10-07 for AC3.21.

After committed AC3.20 (997af79), frozen engine discovery reaches
HashSet<string>(IEqualityComparer<string>) with a direct StringComparer.Ordinal
getter in EffectiveSpellCompiler.Compile IL_021f. The diagnostic copy charges
238,666 metadata units. StateLookup remains eight methods/9,921 units. Evidence:
artifacts/alchemy-list-find-{refresh,engine}; no engine emission/execution.

Add an exact selected constructor declaration and a managed CreateOrdinal factory.
A new HashSetOrdinalConstruction grant authorizes the factory. Discovery checks
selected nominal owners, generic arity, constructor/helper signatures and flags.
The translated comparer argument must be a direct selected Ordinal getter call
in the same basic block; null, aliases, joins, lookalike getters, custom comparers
and non-string owners remain diagnostic. Getter evaluation is retained before
construction. Both ordinal collection bridges now share getter-declaration checks.
Integer CIL v46 records this admission; native v65/layout v24 and resource limits
are unchanged.

Existing string keys already use ordinal equality/hash behavior. Preserve a
private explicit-ordinal marker on the managed set. This controls enumerable
copy construction: the default comparer and explicit Ordinal have distinct
comparer identities, so copying the latter must enumerate/rebuild instead of
copying slots. The first differential run caught a wrong enumeration order after
a removal because that distinction was missing; the Copy fixture reproduces it.

Four probes cover null/empty/case-sensitive/exact UTF-16 keys, add/remove/contains,
retained key identity, iterator versioning and copy construction after removals.
Six unsupported comparer forms and grant removal stay diagnostic. All 48 selected
checks pass, including existing set construction/enumeration/operations, ordinal
list sorting and the public baseline. Four new probes provide 1,176 repeated
O0/O2 native/.NET comparisons with forced GC and ASan/UBSan. The 2,048-byte
frame and 30-second subprocess bounds remain. Public Comparer/randomized hashing APIs and general custom comparer
execution remain outside this slice.

Review checks getter identity, evaluation order, managed construction/rooting,
copy optimization preconditions, and unchanged ordinary set operations. Main
48ecf14 remains an ancestor. Application rules/frozen inputs are unchanged. Full
engine compilation/execution and AC9 saved-game replay remain unfinished.

Evidence: artifacts/alchemy-ordinal-set/tests-b.log and hashes.json.
Core: 4a30a32747b5f8096609cb49a17cedd6ee822844984269fa6c2b32cd759be76f.
Manifest: 24cd5281f9c1b7affee48bc2d153851ff7b7bad6b6a01d163e39ab9297a83693.
Fixture: c28d0a868f2fc01e76a70957f2a579d121272382b4f9cdcaaa9108251ef883c7.
