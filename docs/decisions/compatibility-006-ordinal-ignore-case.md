# Pinned Unicode ordinal-ignore-case comparison

Recorded 2026-10-08 for compatibility batch 006. The selected DiffPlex path chooses
OrdinalIgnoreCase or Ordinal for Dictionary<string,int> piece interning.

Implement typed StringComparer.OrdinalIgnoreCase Compare/Equals/GetHashCode as an
ordinary owned comparer, with a separate runtime-instance singleton cache. Use the
same canonical scalar mapping for ordering, equality and hashing. Keep null ordering,
null/null equality, null hash ArgumentNullException("obj"), and exact singleton
identity. No compiler changes, privilege grants, native helpers or quota expansion.

The reference is dotnet/runtime 79d0c463f1b55624c874a11585f7e47731e8d675:
Ordinal.cs ASCII dispatch, OrdinalCasing.Icu.cs scalar/pair comparison, pal_common.c
BMP table initialization, and retained CharUnicodeInfo.cs supplementary casing.
A valid high/low pair sorts above every unpaired unit; both pairs compare mapped
scalar values, while single units compare mapped units. Remaining length breaks
prefix ties. Invalid surrogates are retained, never replaced or combined across
string boundaries. Simple uppercase keeps U+0131 and U+017F unchanged. This is not
invariant lowercase, Unicode full case folding, normalization or culture collation.
For example dotless-i/ASCII-I and long-s/ASCII-S remain unequal; sigma variants and
micro-sign/Greek-mu compare equal; sharp-s does not expand to "SS".

Reuse licensed UnicodeData 16.0.0 already pinned in the repository. The separate
network-free generator checks its SHA256, unchanged UTF-16 width, idempotence and
exact range expansion. It emits 1475 mappings in 203 ranges through nine small managed
page methods, without table allocations, static roots or OS ICU linkage. The pinned
table is deterministic across targets; a runtime with other Unicode data/NLS mode
may differ. No broad culture-sensitive globalization support is claimed.

Hash canonical UTF-16 with a deterministic DJB-XOR accumulator. Equal text has equal
hashes, including supplementary mappings and malformed sequences. Numeric hash
identity with randomized .NET is not part of this contract. Hashing and comparison
allocate no intermediate normalized string and rely on existing rooted arguments.
Nongeneric/object comparer methods, culture factories, String comparison-mode
expansion and ignore-case HashSet constructor admission are not added.

Managed tests compare exact ordering/equality for all 1,114,112 scalar/unit values
and 12,000 seeded malformed UTF-16 pairs on .NET 10.0.11 macOS ARM64. Native O0/O2
checks cover every changed mapping and neighbors, every canonical equality/hash
pair, all48-by48 catalogue pairs, dictionary selection, null faults and singleton
identity, with ASan/UBSan and collection before allocation. No whole candidate has
been executed. The separate explicit test/recipe requires approved isolation and
compares each result field/code unit, not just a transcript checksum.

See [results](../compatibility-candidates/batch-006-results.md) and the
[execution approval plan](../compatibility-candidates/diffplex-execution-plan.md).
