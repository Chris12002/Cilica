# ADR AC0184: Default comparers for exact equatable keys

Recorded 2026-10-08 for AC3.32.

Frozen SpellResolutionHandler.Contains<T> requires EqualityComparer<T>.Default for
ID structs. Add the abstract typed comparer and managed GenericEqualityComparer<T>,
preserving its exact IEquatable<T> constraint. Four selected integers gain that
interface and ordinary typed Equals bodies. A private EquatableComparerCreate
grant selects the managed factory for four integers or exact equatable keys;
existing key grants independently authorize equality/hashing. Validate the helper
identity, full signature, access, body flags, capability and closed key contract.
Ordinary generic static initialization retains one object per closed T per runtime.
No application-call pattern substitution or native equality operation is added.

Supported: typed Equals/GetHashCode, scalar widths, mutating reference-bearing
value copies, typed reference/null callbacks, generic interface dispatch, custom
subclasses and stable/distinct singleton identities. Specialized string, enum,
nullable and non-equatable fallbacks fail even for factory-only use. Nongeneric
interface methods and comparer-object equality/hash remain unsupported bodies.

Eleven probes supply 3,234 repeated O0/O2 native/.NET comparisons; two native checks
prove four exact cached roots, independent runtime ownership and cleanup. Seven
unsupported families, three missing grants and hostile helper calls are checked.
All 19 new and 61 adjacent primitive/boxing/key/dispatch/public-baseline checks pass
(80 distinct), with ASan/UBSan, forced collection and exact roots/frame bounds.
Initial custom/null fixtures expected a root without initializing Default on those
branches; initialization is now explicit and all focused checks rerun successfully.

Review checked exact constraints, argument copies, callback null suppression,
per-runtime static ownership, unchanged budgets and unchanged application inputs.
Integer CIL v80, native v75; existing IR/layout profiles unchanged. Main 48ecf14 is
an ancestor. All 70 pinned source hashes and six frozen input hashes verify.
Evidence: artifacts/alchemy-equality-comparer{,-refresh,-engine}.
Core SHA256: b5f944db6fc33c84aa082a29f6ebaf513eee7a7359bbca6a96429b7e4b0e26af.
Manifest: bb03e1b871eb80097ade755195ed9621775bd318688ab1ea8417ef75e122811c.

Frozen discovery reaches the existing field limit through ConstructionInvitations
and Enumerable.Where: 1,038,837 metadata units/4,707 ms. StateLookup remains 15
methods/11,566 units (479 ms). No engine emission/execution. Full engine, unchanged
fingerprint and saved-game replay remain unfinished; field-budget measurement follows.
