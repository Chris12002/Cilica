# ADR 0100: Reference arrays in the composed CIL profile

Status: accepted for slice 6c.9. Recorded 2026-10-05.

## Decision

The explicit integer-object CIL entry point now accepts vectors of admitted
nominal references, including closed generic classes. `newarr`, typed generic
`ldelem`/`stelem`, reference element opcodes, bounded length, array casts and type
tests reuse the existing vector IR and native runtime. Int32 vectors retain their
existing representation. Generic array fields use the same structural element
identities as parameters, locals and operation operands.

Covariant reference assignments preserve the actual allocation descriptor;
stores still check runtime element compatibility after null and bounds checks.
The compiler does not flatten arrays into object fields or copy their elements
for casts. Array tracing therefore retains the existing collector behavior.

Discovery includes fallback views for null element operations. After stack
translation, unused vector views are removed; all declared fields and used
operation views remain in the catalog, and nominal closure validation is
unchanged. Array type tests require the explicit profile and a visible admitted
element. Generic argument access and type/metadata budgets remain checked.

## Validation and review

Independent C# probes cover generic allocation/copy, reference arrays in class
and struct fields, identity, zero-initialized elements, covariance, null-only
access, bounds/type fault order, successful/failed casts and cast messages.
All 1,176 repeated O0/O2 ASan/UBSan results match .NET with forced collection,
exact frame budgets, exhaustion and cleanup checks. Tests exposed and fixed
missing array-cast admission and unused fallback vector catalog entries.

All 238 focused checks pass. Review checked exact element identities, null fallback retention, unchanged
runtime checks, roots across allocation and continued legacy profile rejection.
Wide/narrow/value-element arrays, element borrows, generic interface array
helpers and broader class/value dispatch remain separate gates.
