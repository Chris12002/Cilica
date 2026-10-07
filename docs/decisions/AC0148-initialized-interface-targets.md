# ADR AC0148: BeforeFieldInit interface targets

Recorded 2026-10-07 for AC8.41.

## Evidence and decision

The committed d6f23d2 frozen probe clears enum interface catalogs and reaches
AlchemyGameEngine/SpellResolutionHandler.get_Id after 440,238 metadata units.
Its owner initializes a static readonly ResolutionHandlerId. The class catalog
and ordinary body discovery admit BeforeFieldInit owners, but interface method
preparation still rejects every owner initializer. StateLookup remains eight
methods/9,927 units. No engine emission/execution occurred.

Align closed interface preparation with the existing integer-object rule:
BeforeFieldInit owners are admitted, strict type/module initialization is not.
The method body uses ordinary static-field access and its existing initialization
trigger. Dispatch itself does not initialize the owner, and null receiver checks
retain their existing order. Constructor, lexical access, closed generic identity,
method signature, selected helper grants and readonly storage checks are unchanged.
The older non-closed interface profile still rejects initialized owners.
Integer CIL v65 records admission; IR/native profiles, core and budgets are unchanged.

## Validation and review

Six probes provide 1,764 repeated native/.NET comparisons at O0/O2, with ASan/UBSan
and forced collection. They cover a private nested handler returning a static
value, explicit implementation, boxed struct implementation, closed generic class
state with exactly two retained reference roots, repeated failure/inner identity,
and null receivers. A metadata-only check accepts the closed BeforeFieldInit
owner but rejects both the older profile and a strict initializer. No assembly
initializer runs in the compiler.

The metadata check initially used a test helper restricted to generic interfaces;
it now resolves the nongeneric declaration directly. All 90 distinct focused
initialization, private/interface, boxed-value and plan checks pass. Evidence:
artifacts/alchemy-initialized-interfaces/{tests,regressions,boundary}.log.

Review checked initialization timing, retained failure ownership, receiver null
order, private and explicit mappings, boxed value copies, generic static isolation
and the scope of the closed-profile gate. Existing 2,048-byte frame and subprocess
limits are unchanged. Full engine, unchanged fingerprint execution and saved-game
replay remain unverified.

All four public-baseline checks also pass: 94 distinct checks overall, 46 public
controls and one retained gap. Evidence: baseline.log. Local main remains
ancestor 48ecf14.
