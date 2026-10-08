# ADR AC0230: Direct value returns into proven non-null heap fields

Recorded 2026-10-09 for AC8.86 after c1dfecd (AC8.85).

## Decision

Admit direct returned values in reference-instance fields only when the
integer-object translator proves that the receiver was non-null before evaluating
the RHS. Reuse the existing rooted caller-owned return packet and final leaf copies.
No null checks, allocations, field copies or calls move to a different position.

NonNullFieldReturnProof runs only on methods containing these stores. Forward
intersection tracks allocations through unaddressed locals, casts and stack inputs,
including joins and loop back-edges. Stable constructor receivers are non-null.
A successful field read, borrow or write proves a stable instance argument for
subsequent loads and normal successors. It never retroactively marks an earlier
receiver SSA value as non-null. Taking the argument's address disables that rule.
Address-taken locals are excluded throughout the method; exception/filter entries
and leave successors discard incoming facts. Validation happens after convergence,
with a separate 1,048,576-work cap per method and an actionable failure.

Nullable/unknown destinations remain diagnostic. The older profiles retain their
restriction. CIL110 identifies the new admission; IR65, native81, layout28,
interface20, owned core, grants and runtime resource limits are unchanged.

## Reference and caught regression

The frozen AttackDeclaration constructor (0x06000094 IL_0014) stores a returned
UnitId. Converted Alchemy.Engine__2.cpp lines 22537-22612 uses a returned value
followed by a field copy. SHA256:
`40464e461a6b15890181e01b2405e3483310d6357acbf6cb738d552222a599ab`.
The alchemy-reference-20261008T2312Z capture has different DLLs from the frozen
audit; it guides structure, while frozen metadata and the .NET oracle decide
admission and behavior. No game rules or frozen inputs changed.

Simply removing the old guard failed native comparisons: CompoundStore(0,0)
reported DivideByZero where .NET reported NullReference; a side-effecting RHS also
ran in native code when it did not run under .NET. These failures remain preserved
in artifacts/alchemy-field-returns/tests.log. That prototype was not committed.
The non-null proof resolves the ambiguity without encoding a platform's struct
return-buffer null-check timing.

## Review and validation

Review checked fixed-point convergence, contradictory loop paths, exception entry
seeds, finally mutation, address aliases, stable receiver identity, block retranslation
and proof work charging. Existing scalar lowering retains receiver/returned references
across allocation and only publishes field leaves after the call succeeds. Readonly
and detached IR validation remain unchanged. No remaining findings in this scope.

All 32 distinct selected checks pass across tests-proof.log (30, 79.762s), the
updated FieldReturnTests run tests-proof-final2.log (3, 13.462s), and the added
callee-fault check tests-callee-fault.log (2, 4.259s). The final extension preserves
all earlier cases; the additional method checks failure after constructing the
return value, so an exception cannot partially overwrite the previous field.
Initial build failure was a nonexistent test helper and was corrected before testing.

Ten new field-return roots x 49 pairs x three repeats x O0/O2 = 2,940 comparisons;
nullable return/internal enum roots add 588, and long-tuple returns add 420:
3,948 new comparisons. Coverage includes reference-bearing nested values, empty
values, virtual returns, readonly initialization, changing an unrelated holder,
call faults, branches/loops and receiver reads before branches. Seven negative
cases plus the original CompoundStore pin null-ordering, nullable joins/back-edges,
borrowed aliases and exception/finally fact loss. ASan/UBSan, forced collection,
exact root/borrow cleanup, frame budgets and the 2 KiB frame warning remain enabled.
No full suite, hosted tests or device tests ran.

## Frozen body inventory

A diagnostic-only compiler copy now inventories every discovered body even after
aggregate IR capacity is exhausted, always returning a null program and emitting
no C++. Individual body checks remain active. At c1dfecd it discovers 15,253
bodies, translates 15,185 individually and finds 68 failures in six categories:
25 field returns, 21 managed-location joins, 11 exact storage mismatches,
eight static addresses, two integer parser grammars and one array-length comparison.
The first aggregate failure is 4,097 blocks against 4,096; the successful-body total
is 59,820 blocks, 279,982 statements and 968 exception regions. These are capacity
measurements, not a production limit increase or a validated full IR program.

The allocation/constructor proof removes 24 field-return failures. The remaining
LineOfEffect iterator already checks its receiver, but needs that fact carried
through branches; the final proof extension handles this case and is checked below.

Final inventory (artifacts/alchemy-nonnull-field-flow-inventory) translates 15,210
bodies individually, with 43 failures across the five other categories. All 25
field-return failures are gone. Engine discovery charges 3,818,341 metadata units
in 63,795 ms. Successful bodies total 60,410 blocks, 284,194 statements and
992 exception regions. StateLookup still validates 15 individual methods in
325 ms. Both program/emission results are null; full IR/native closure and saved-game
replay remain incomplete. The intermediate same-block-only proof inventory still
had the one iterator failure; receipts preserve that result separately.

All six frozen DLL hashes match. The compiler-only audit reuses the unchanged
AC8.85 owned core (`92758a62b697681f4c99eedc222698bc14693eef2b1f6a9275e9dc1b9a14d751`)
and manifest (`a66a016b1b370ca085f69cf7ecb4aa2f0311467c97cd2930bb9ebfd8ad8179b2`).
