# ADR AC0238: Hidden generic value-array allocation

Recorded 2026-10-09 for AC8.94 after b9b346f.

After all 15,254 frozen bodies translate, bounded diagnostic whole-program
validation rejects MaterializationBuffer<LiveShieldComponentSelection>.ctor at
Array.Empty<T>. AC0035's instruction-scoped generic evidence covered references,
while AC0126 intentionally left hidden value arrays diagnostic.

Converted Alchemy.Engine__6.cpp 3949-3955 retains nested value/string fields in
LiveSpellModeChange; 33612-33644 copies that exact struct through its typed List
array. SHA256: `21455d5f4a89a1a37a981cbe305a4d91c5dfab7c553f63b5bcd738eb91368886`.
These analogous representations guide the payload/copy/root tests; the converted
capture differs from the frozen inputs and is not substituted for Cilica.

## Decision and review

Extend the existing bare type/method-variable newarr/Array.Empty evidence to
hidden named/constructed inline values and admitted Int32 enums. The exact
closed-context hash, variable index, instruction offset, operation and nominal
array kind must all match. Explicit source names still pass their original lexical
checks before substitution. Proof does not authorize casts, unrelated instructions
or member access. Primitive/nested-array shapes are not inferred from this proof.

Value snapshots may be deferred until catalog construction. At a proven bare
variable site, discovery obtains the value through the existing charged snapshot
cache, then records evidence only when the declaration is hidden. Enum identity
uses its validated catalog. Both IR stages reconstruct the exact reference/value/
enum array; scalar validation retains the boxed composition's value declarations
for access checks instead of losing that permission boundary after expansion.

A tampering test found that the first scalar implementation accepted a hidden
value allocation after its proof was removed. The final implementation checks
visibility against the retained value catalog and rejects that case. Existing
standalone scalar-location contracts are unchanged. Review checked snapshot
charging/cache reuse, specialization identity, exact array kind, proof confinement,
logical/scalar agreement and unchanged tracing/allocation algorithms. No remaining
findings in this scope. CIL118/object IR71; native86, layouts, core, grants and
production budgets remain unchanged.

## Validation

92 selected checks pass: 55 focused/adjacent tests in 40.532s and 37 private-generic,
hidden-enum and materialization tests in 71.215s. Nine new fixture roots and one
promoted foreign-assembly raw probe add 2,940 native/.NET comparisons (49 pairs x
three repeats x O0/O2), with ASan/UBSan, forced collection, exact cleanup and
frame-budget boundaries. Cover reference-bearing copies, owner/method variables,
constructed values, empty structs, enum identity, empty-array caching, bounds,
List growth and iterator materialization. Eleven mutation families run against
both IR stages for named/constructed/enum cases. Existing reference proof and scan
bounds remain tested; foreign explicit names/member access remain rejected.
The private-field diagnostic assertion was updated to the existing AC8.85 wording.
Receipts: artifacts/alchemy-hidden-value-arrays.

## Whole-program diagnostic inventory

The initial bounded full-IR probe reaches the first generic value-array failure;
its all-method follow-up records 2,400 invalid bodies, including 2,376 literal
failures cascading from the 256-distinct-string limit. Both runs keep production
code unchanged and return no engine program. The diagnostic copies admit at most
131,072 blocks/parameters/locals, 1,048,576 statements, 262,144 inputs, 524,288 edge/
call arguments, 4,096 exception regions and 2,097,152 expanded scalar slots, while
retaining the prior bounded discovery allowances. No emitter is invoked.

After this slice, the next diagnostic run allows 16,384 distinct literals and
reports 3,512 distinct literals/352,000 charged units. It validates each method
individually with shared catalogs and records 35 failures: 25 isinst, four castclass,
two constructor-prefix, two direct EmptyArray, one direct NewArray and one delegate
capture. Each is the first failure per body; later checks can still expose more.
All 15,254 bodies still translate before validation, with 61,756 blocks/294,454
statements, 1,810 reference types, 387 value types and 1,488 interfaces. Discovery
charges 3,839,271 units; the engine probe takes 69,333ms. StateLookup passes logical
and scalar validation (15 methods, 232 scalar statements, 152 roots/max24) in
679ms/11,759 units. It is not newly emitted or executed.

Receipts: artifacts/alchemy-full-ir-probe, artifacts/alchemy-full-ir-inventory and
artifacts/alchemy-value-array-ir-inventory. Core is the unchanged pinned
`9c18b9882eab4b53a1fc1b174dcfae1d29ccafa5a86b5b7f159e0aea7e6e4b86`, manifest
`b874d325bf97252dd865047527023291f37b7dd924b55777e4c0f12d2efb555e`.
Full logical/scalar closure, production capacity, native engine and saved-game
replay remain unfinished. No complete-program success is inferred from the audit.
