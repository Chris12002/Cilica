# ADR AC0143: Measured diagnostic engine type capacity

Recorded 2026-10-07 for AC8.38.

After AC3.26 (1dfc7fd), frozen discovery reaches its diagnostic 1,024-type cap
in List<T> construction through EffectiveSpell.Copy. A counter-only source copy
records 1,025 roots: 74 vectors, 92 values, 859 references; one additional enum
array identity; 993 snapshots, 2,645 fields, 540 nominal declarations and 462
closed generic roots. It has 2,926 method declarations, 69,045 instructions,
5,642 parameters and 3,049 locals. It charges 356,926 metadata units. These are
partial discovery counts, not emitted runtime layouts.

Raise only the isolated copy to 2,048 types. With all other diagnostic limits
unchanged, discovery advances to Object.ToString on a duplicated StringBuilder
receiver in SemanticFingerprint.Create(IReadOnlyList<IEffectNode>) IL_000c,
after 368,197 metadata units. The receiver remains on the evaluation stack while
AppendNodes consumes another copy; the existing immediate-producer sealed proof
does not track that pattern. StateLookup stays at eight methods/9,927 units.

All six frozen input hashes match. No engine emission or execution occurred.
Evidence: artifacts/alchemy-engine-type-audit and alchemy-engine-types2048,
including scripts, copied-source hashes and the unchanged frozen Create IL.
Production limits remain 256 types, 512 methods, 65,536 instructions/metadata
units and 4,096 parameters/locals. Diagnostic limits are now 2,048 types, 4,096
methods, 131,072 instructions, 524,288 metadata units, 8,192 parameters and 4,096
locals. Per-method, native frame and test subprocess budgets are unchanged.
Full-engine/fingerprint execution and saved-game replay remain open.
