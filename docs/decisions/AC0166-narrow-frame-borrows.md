# ADR AC0166: Width-preserving local and parameter borrows

Recorded 2026-10-08 for AC8.51.

The frozen Boolean.TryParse(String, Boolean&) call needs a Boolean out location.
Implement reusable narrow frame borrows first: SByte, Byte, Int16, UInt16, Char and
Boolean locations carry an invariant signed/unsigned width through both IR stages,
call packets and private native carriers. Frame storage stays in existing Int32
slots. Loads and stores normalize the low byte/short with unsigned arithmetic;
Boolean preserves noncanonical low bytes instead of converting every value to 0/1.
Indirect loads additionally apply the instruction's signed/unsigned extension.

Only local/by-value parameter addresses introduce these views. Synchronous
reborrowing and existing proved scoped ref locals preserve the width and lifetime.
Ordinary full-width locations cannot satisfy narrow parameters or vice versa.
Readonly views cannot change widths; detached validation rejects invalid widths,
widths on scalar types, mismatched calls and narrow heap/array/boxed addresses.
The native argument check also verifies the width and authenticates active frame
storage before access. No raw native pointer or public layout is introduced.

Narrow field/array addresses and narrow ldobj/stobj/cpobj/initobj remain diagnostic.
No production resource limits change. Integer CIL v73, object IR v47 and native
v73 record the extension; object layout v26 and interface plan v16 stay unchanged.
The extra carrier field is included in existing frame-size accounting.

Twelve authored IL probes omit conversions before indirect writes and reborrow
through two callees. Nineteen boundary inputs include signed edges and noncanonical
Boolean bytes. Three C# probes cover overlapping aliases, a write before throwing
an allocating exception, and a loop using a scoped byte reference. These supply
2,250 repeated native/.NET comparisons at O0/O2 with forced collection, ASan/UBSan,
exact/short frame budgets and cleanup checks. Four mismatched opcode widths and
four detached-IR mutations fail closed. Native carrier checks cover all four
widths, invalid tags, wrong origins, readonly writes and parameter tag mismatches.

Review checked unsigned sign extension, width preservation through lowering and
readonly/call paths, normalized direct storage aliases, native frame lifetimes,
exception cleanup and unsupported-origin rejection. Evidence:
artifacts/alchemy-boolean-tryparse/location-{build,tests,composition,baseline}.log.
All 94 distinct selected checks pass, including four public baseline checks
(46 native controls and one retained diagnostic gap). Boolean parsing and the
unchanged engine replay remain follow-up work.
