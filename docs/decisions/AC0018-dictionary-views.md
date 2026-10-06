# ADR AC0018: Dictionary key and value views

Accepted 2026-10-06 for bounded AC4.2.

## Decision

Add exact public nested KeyCollection and ValueCollection types. Dictionary
properties cache one instance of each per dictionary; all corresponding generic,
read-only and nongeneric properties return those same objects. Manually constructed
views stay distinct. Views retain the dictionary, forward live Count and SyncRoot,
and reject mutation with pinned key/value-specific NotSupportedException messages.
Their constructors check null with parameter dictionary. Copying and enumeration
are still build errors.

Key lookup forwards ContainsKey. Netstandard2.1 clients reach it through
ICollection<TKey>; the pinned runtime's public Contains is not part of that older
reference surface. Tests were corrected to use the reference contract. Because
interface dispatch must cover both view implementations when key/value types
coincide, implement Dictionary.ContainsValue and value-view lookup in this slice.
Capture the entries vector, re-read dictionary count during the scan, skip free
slots and use the existing null/equality selectors. Equality receives a copy of
the stored value, preserving mutation/throwing/allocation behavior without
modifying the dictionary slot. Nullable/default-struct/arbitrary-reference fallback
equality remains unsupported. No compiler permission or runtime profile changes.

## Evidence and review

Ten authored probes plus public DictionaryKeys supply 3,234 repeated native/.NET
comparisons at O0/O2 on macOS ARM64 with ASan/UBSan, forced GC and frame/cleanup
checks. Cases include all view property forms, cached/manual identity, live counts
through removal/clear/reuse, null constructor/key/value handling, read-only faults,
reference-bearing values, integer/string lookup and allocating/mutating/throwing
value equality. The original view-only body reached the unsupported value lookup
through the shared interface closure; this dependency is now implemented.

Two GC checks retain only a key view: seven objects remain (view, dictionary,
bucket/entry arrays, entry, value and emergency exception), then only the emergency
exception remains after dropping the view. This proves both dictionary retention
and collection of the dictionary/view cycle. Exact nested interfaces and member
mappings pass; unknown value equality remains a build error. Forty-one existing
dictionary/mutation/baseline regressions pass. Review checked cache publication,
null-before-access ordering, no mutable view escape, root cycles, captured-vector
semantics and copied equality receivers. The ledger has 23 gaps and 24 controls;
no private replay execution or broader collection support is claimed.
