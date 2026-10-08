# ADR AC0207: Named Int32 enum value arrays

Recorded 2026-10-08 for AC5b.16 after AC8.64.

Frozen SpellModeChoices.BondsAfter calls Enum.GetValues(typeof(ElementGender)) and
casts the result to ElementGender[]. Add the exact public owned GetValues declaration
and the EnumValues capability on that method and Type.GetTypeFromHandle. Discovery
recognizes an uninterrupted ldtoken/GetTypeFromHandle/GetValues sequence, checks both
selected declarations and grants, original operand access, enum shape and constant
rows. Branch targets and exception-region edges cannot enter the last two operations.
Both calls are specialized as one operation; the temporary Type handle/object is not
materialized. Outside this exact sequence, GetTypeFromHandle retains its managed body.
The selected enum/type declarations cannot have initialization side effects.

Read validated constants without executing input assemblies. Sort by their unsigned
Int32 bit patterns, retaining duplicate values. Emit an ordinary fresh nominal enum
vector plus existing checked stores, including a fresh allocation for zero constants.
This reuses array identity, cast rules, allocation, fault edges, roots and collection;
there is no new IR operation or native runtime helper. Each construction is limited
to 256 entries and expanded instructions are charged to the existing discovery budget.
Dynamic Type arguments, aliases/joins, non-enums and other underlying widths remain
actionable build errors. Direct System.Array.GetEnumerator remains independently
unsupported; the Alchemy call uses an explicit typed-array cast.

The first 29 checks had two failures at System.Array.GetEnumerator in the flags fixture.
Use the typed-array path for flags and retain direct Array enumeration as a negative.
All enum-values checks then passed in the 64-check adjacent run; one old enum-array
negative unexpectedly admitted UInt32 aliases. The same failure reproduces on the
unmodified prior Range snapshot in 0.830s. Promote that obsolete rejection to a native
comparison and add signed/unsigned mutation tests covering all 32 bits. The final
51 enum-values/enum-array checks pass in 52.671s; the 17 unchanged runtime-type and
static-array controls passed in the preceding 74.439s run. Thus 68 distinct checks
are covered. Ten new probes plus one promoted probe x49 pairs x3 repeats xO0/O2 yield
3,234 new comparisons. ASan/UBSan, forced GC, exact cleanup and frame budgets apply.
Tests cover ordering, aliases, flags, empty/fresh identity, copies, generic views,
private enum access, array faults, 256/257-entry limits, both missing grants, dynamic
forms and a valid branch merge entering the handle call. No full-suite claim is made.

Review checked exact library/member identity and capability binding, sequence/control
flow boundaries, lexical access, unsigned sorting, input validation and instruction
budgets, fresh arrays, rooting/fault edges and preserved adjacent semantics. CIL94;
IR59/native79 unchanged. All 94 upstream hashes and six frozen assembly hashes verify.
The pinned Enum.cs/RuntimeType.cs contract copies values to a new enum array; this is
an independent compiler specialization, not a substituted runtime. Source snapshot
c64312e; local main 48ecf14 remains an ancestor. No game-source changes or rebuilds.

Evidence: artifacts/alchemy-enum-values{,-refresh,-engine}. Engine discovery advances
to the absent parameterless List<Int64>.Sort in GameStateHasher.AppendSpell,
0x0600014b IL_03ac/member 0x0a00025e. It charges 1,344,122 metadata units in 4,616ms;
StateLookup still translates 15 methods, 11,616 units/425ms. Existing diagnostic-only
allowances are unchanged; no full engine emission or execution occurs.
Core fdd938322a3382aca0115e9fb62e739a78a83edba95c0c02147f15a8e10461e4.
Manifest 01e68e1bb670ed1fbb5abb65918fcdba80d79c50cbf4dac984e5d980b6854efb.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.
