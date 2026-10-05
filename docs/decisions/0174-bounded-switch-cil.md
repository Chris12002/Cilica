# ADR 0174: Lower bounded CIL switch through existing branch IR

Accepted 2026-10-06; milestone 6c.59.

The real kernel's Step and DriveAutomatic methods use switch tables. Admit them
in integer-managed-objects-cil-v2 by expanding each table into equality branches.
The selector must be Int32. Each case consumes the selector logically; only the
unmatched comparison path carries it onward. Default handles negative and large
indices naturally, with no signed arithmetic or native unchecked table indexing.
Empty tables consume their selector and fall through.

Insert comparison blocks in source order, assigning their source positions inside
the original switch operand span. Actual CIL targets and exception boundaries must
still name instruction starts; they cannot enter those synthetic positions. This
keeps existing IR monotone offsets and protected-region ranges valid without
introducing a new terminator throughout validators, lowering and native emission.

Charge all synthetic blocks, stack inputs, statements and edge arguments against
existing limits. Legacy CIL profiles continue to reject switch. This favors a
bounded, reviewed implementation over a new jump-table optimization; native
compiler optimization remains free to simplify the branch ladder.

Dense/shared/default cases, integer extremes, loops and catch/finally behavior
match .NET at O0/O2 with sanitizers and forced collection. Authored empty/duplicate
tables preserve residual stack values. Non-Int32 selectors, operand targets and
one block over the exact 4,096-block bound are rejected. Total new repeated native
outcomes: 1,488. The real kernel advances to readonly constructor receiver validation.
