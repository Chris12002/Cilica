# ADR AC0231: Native array-length zero comparisons

Recorded 2026-10-09 for AC8.87 after 0969c44 (AC8.86).

Frozen EffectOperationDescriptor.Supports (0x060009f7 IL_0011) returns the result
of ldlen, ldc.i4.0, cgt.un after selecting one of two arrays. Converted
Alchemy.Engine__6.cpp lines 17012-17041 checks that selected array for null and
compares its length with zero. SHA256: `21455d5f4a89a1a37a981cbe305a4d91c5dfab7c553f63b5bcd738eb91368886`.
The reference capture's assemblies differ from the frozen audit; no input changes.

Recognize only adjacent ldlen/ldc.i4.0/ceq and ldlen/ldc.i4.0/cgt.un in one basic
block of the integer-object profile. Bounded array lengths are nonnegative Int32,
so lower these to Boolean equality/inequality using the existing ArrayLength and
Compare IR. The original array read owns the null fault. Consume the complete
sequence and preserve block/exception boundaries. Numeric native-int escapes,
nonzero comparisons, split consumers and older profiles remain diagnostic.
No general native-integer type is introduced. CIL111 changes; IR65, native81,
core/grants, native algorithms and production budgets are unchanged.

All 27 focused/adjacent checks pass in 27.341s (artifacts/alchemy-array-length-compare).
Four new C# roots plus two exact raw-IL forms x 49 pairs x three repeats x O0/O2
= 1,764 new native/.NET comparisons. Raw IL uses DynamicMethod only as an external
managed test oracle, never in the compiler process. Tests cover empty/nonempty,
selected scalar/reference/wide arrays, null-versus-arithmetic fault order, legacy
rejection, jumps into the constant/operator, intervening nop and numeric escapes.
Existing array truth-branch checks also pass. ASan/UBSan, forced collection, exact
root/borrow cleanup and frame-budget checks remain enabled. No full suite or hosted
work ran. The first snapshot-launch command used a not-yet-created workdir and
failed before executing; creating the snapshot from the existing cwd resolved it.

Review checked that both skipped instructions belong to the same basic block,
that signed/native width cannot affect these zero observations, and that null
faults retain the ldlen offset and region. No remaining findings. The full frozen
inventory is deferred to the next combined compiler probe; the previous inventory
has 43 failures, including this exact sequence. Full IR/native closure and
saved-game replay remain incomplete.
