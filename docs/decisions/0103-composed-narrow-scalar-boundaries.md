# ADR 0103: Narrow scalar boundaries in composed CIL

Status: accepted for slice 6c.12. Recorded 2026-10-05.

## Decision

The explicit integer-object CIL profile accepts SByte, Byte, Int16, UInt16, Char
and Boolean scalar parameters, initialized locals and returns. As in ADR 0096,
stack values remain Int32. Parameter loads, local stores and returns normalize
through explicit unchecked casts; signed storage extends its sign and unsigned
storage retains low bits. Boolean storage retains the low byte rather than
forcing a canonical zero/one value. Instance signatures account for the receiver
before locating a declared parameter's storage signature.

Narrow by-reference parameters and narrow local/parameter address-taking remain
diagnostics. They must not silently become Int32 storage borrows. Narrow fields,
array elements and enum identities remain subsequent storage work.

C# conditional constructor field expressions can leave the direct struct `this`
receiver on the CIL stack across branches. Translation records that exact
parameter-zero provenance, omits it from transferred IR operands and reloads the
same checked receiver at each destination. Incoming edges must agree. This
retains readonly initialization verification and does not admit other borrowed
values at block joins. Constructor temporaries are reused by IL offset when
reference-join analysis revisits a block, avoiding artificial local growth.

## Validation and review

Two authored C# probes compose narrow constructor arguments, generic parameters,
loops, character/Boolean values and checked conversion catches. Eighteen exact
authored IL probes test all six storage kinds at parameter/local/return boundaries
without explicit conversion opcodes; independent DynamicMethod oracles execute
only this authored test IL. All 2,640 repeated O0/O2 ASan/UBSan results match .NET.
Native checks also cover collection, exact/insufficient frame budgets and cleanup.

Tests exposed the conditional constructor receiver gap and pass with the bounded
reload lowering. Narrow borrows/addresses and legacy profile expansion remain
rejected. Review checked normalization order, receiver provenance, direct readonly
writes, stack indices, revisited-block bounds and preservation of general borrow
join rejection. All 105 focused checks pass. No native runtime layout changed.
