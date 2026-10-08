# ADR AC0233: Invariant Int64 Integer-style TryParse

Recorded 2026-10-09 for AC8.89 after 5a72329 (AC8.88).

Frozen InspectShieldComponentPrimitive.ParseOption (0x06000f09 IL_003b) and
SpellResolutionHandler.PointCommitments (0x060010b1 IL_0046) pass NumberStyles.Integer
(7) to Int64.TryParse. Converted Alchemy.Engine__7.cpp lines 27431-27439 shows the
explicit invariant provider, style 7 and borrowed Int64 output. SHA256:
`97384aa23802e2a960a590c4131b3d659aaba9ed854abf845c583c5d940e4521`. The converted capture's inputs differ from the frozen audit;
no application rules or input assemblies changed.

Reuse IntegerParsing.Int64IntegerStatus, already used by throwing Int64.Parse,
through a Boolean Int64Integer helper. The owned Int64.TryParse body selects None
or Integer after checking the provider. Failed parses leave output zero; Integer
admits signs and the existing ASCII whitespace/NUL grammar with signed limits.
No duplicate parser algorithm or native intrinsic is introduced.

Share IntegerParserRules.AllowsStyle between CIL admission and detached call
validation: Int32 remains style 7; Int64 accepts 0 or 7; both require directly
proven CultureInfo.InvariantCulture. Dynamic styles, aliases, other providers and
unmapped signatures remain diagnostic. Existing exact InvariantIntegerParsing
grants remain mandatory. CIL113/object IR66/native82 identify the changed admitted
call contract; native algorithms, layouts and production resource limits do not change.

## Review and validation

All 31 selected checks pass in 33.531s: Int64IntegerParseTests, Int64ParseTests and
ThrowingIntegerParseTests. Four new roots add 3,660 O0/O2 native/.NET comparisons:
two parsing/storage roots x 64 strings x four observations x three repeats x two
optimization levels, plus two roundtrip/null-output roots x 49 pairs x three x two.
The owned parser also matches every UTF-16 edge character in leading/trailing
position and 512 seeded full-width numbers. Tests cover signed extrema, overflow,
invalid suffixes, zeroed output, borrowed heap output, null receiver order, exact
grant denial and forged declarations/style/provider operands at both IR stages.
ASan/UBSan, forced collection, exact root/borrow cleanup and frame budgets remain
enabled. No full suite, hosted or device tests ran.

Review checked reuse of overflow precedence and default-output behavior, both
validator gates, None regression coverage and absence of grammar broadening beyond
0/7. The discovery diagnostic now names both admitted styles. No remaining findings.
Receipts: artifacts/alchemy-int64-integer-tryparse.

## Frozen inventory

Both parser blockers clear with the rebuilt core. The diagnostic-only inventory
has 15,254 discovered bodies and 15,228 individually translated; 26 failures remain:
11 readonly receiver/storage mismatches, eight static-address operations, six
joins selecting different slots, and one derived-address join. Exact-type tracing
in this diagnostic copy confirms all 11 storage mismatches are readonly-to-writable
borrow mismatches. No production semantic check is bypassed.

Engine discovery charges 3,818,376 metadata units in 62,843 ms. Individually
successful bodies total 60,944 blocks, 288,553 statements and 1,000 exception
regions. StateLookup translates 15 methods in 335 ms. The inventory returns
no program and emits no native code. Full IR/native closure and saved-game replay
remain incomplete. Frozen input receipts retain all six verified hashes.
Core SHA256: `6813cd1a74c6e7f79a2ecc357ab2209293b861b578b25f1092b76241e8c88afa`.
Manifest SHA256: `49aad12860238eb0ecf9c565d2726339fafde371f84091ab2473cbc447136919`.
Receipts: artifacts/alchemy-int64-integer-inventory.
