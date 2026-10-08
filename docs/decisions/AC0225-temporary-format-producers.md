# ADR AC0225: Formatting arguments through temporary values

Recorded 2026-10-09 for AC8.81, following polymorphic dictionary text
(compatibility 021, commit 6374716).

The immutable Alchemy Engine WithHitPoints method 0x060000cd formats an Int64
property in an exception message. Its IL keeps the literal on the evaluation
stack while a returned value is stored in local 2, takes ldloca.s 2, calls its
property getter, boxes Int64 and calls String.Format at IL_0028. Integer
formatting already exists; argument discovery could not traverse the temporary.

Extend the existing 256-instruction backward candidate scan to consume local/
argument stores and their expressions without treating those stores as argument
producers. Admit bounded local/argument address loads. Skip intervening stores
before selecting each producer as well as before locating the format literal.
Every visited instruction remains charged and branch/exception entries stop the
scan. The typed translator still independently requires the actual same-block
BoxValue and literal with no format suffix. This is candidate discovery, not a
new permission to transport unknown objects or infer literals across branches.

Keep ordinary getter evaluation, mutation, exceptions and boxing in their original
IL order. No application logic, formatter implementation, runtime, grants, IR or
production budgets change. Integer-object CIL advances to v108; object IR64,
interface20 and native80 remain unchanged.

## Converted reference

Directly inspected the separately pinned Alchemy capture at
`/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/evidence/alchemy-reference-20261008T2312Z`.
In `generated/Library/Bee/artifacts/MacStandalonePlayerBuildProgram/il2cppOutput/cpp/Alchemy.Engine__2.cpp`,
WithHitPoints around lines 24117-24136 stores the returned BoardObjectId, obtains
its Int64 Value, boxes it, formats the message and throws. SHA256:
`40464e461a6b15890181e01b2405e3483310d6357acbf6cb738d552222a599ab`.
This guides operation/order coverage; no Unity implementation is copied. The
capture and frozen audit use different DLLs. The frozen method was independently
read as metadata, and all six frozen input hashes remain unchanged.

## Validation and review

`artifacts/alchemy-format-producer` is an isolated snapshot of 6374716 plus this
slice. The baseline fixture reproduced the exact scalar-format diagnostic.
An intermediate multiple-argument failure showed that stores must be skipped
before recording the next producer, not only inside its recursive consumer.
The corrected implementation passes all 31 selected checks in 94.589s:
FormatProducerTests, BoxedIntegerFormattingTests, MixedFormattingTests and
BoxedFormattingTests. An earlier mixed class/method runner filter selected zero
tests; native-selected-tests.log is the authoritative completed run.

Six new roots x 49 argument pairs x three repeats x O0/O2 supply 1,764 new
native/.NET comparisons. Cover temporary property chains, three arguments,
mutating addressed locals/parameters, Int64 extremes, callback counts, argument
faults preceding format faults and null receiver order. Exact frame/borrow
cleanup, one-short frame rejection, ASan/UBSan, collection before allocation and
2 KiB frame warnings remain enabled. New alias/dynamic-format and raw branch-to-
store cases remain diagnostic; existing exact-grant and same-block box tests pass.
Native source/executable receipts are in native-evidence. No full suite or hosted
tests ran.

Review checked monotonically decreasing scan cursor/budget, nested store
consumption, address storage types, control-flow stops, producer selection before
literal checking and independent typed proof. No remaining findings in this slice.

## Frozen scan

The diagnostic scan passes WithHitPoints and reaches ObjectTypeRelations' unchanged
1,048,576 work limit while constructing assignment relations. The first scan
charged 3,401,709 metadata units in 19,329ms. A counter-only follow-up reports
1,048,577/1,048,576 relation steps at array contracts, with 3,590 identities,
1,467 interfaces, 235 arrays and 160,198 generic candidate pairs. StateLookup
continues to validate 15 methods (11,742 metadata units); both emission fields
remain null. This is discovery evidence, not native full-engine execution.

Core SHA256: `19d2c0c21699f079b8db0d393e7be34c6f75df6ad5e46589b53b9963ced15b78`.
Fresh manifest SHA256: `52b28454923426ba0af2537efe98d36d471a838c5e156b24033335bb745d801f`.
Receipts: artifacts/alchemy-format-producer-refresh and -engine, including the
original work12288.json and counter-only relations-limit.json. Production limits
remain unchanged. Full closure and saved-game replay remain incomplete.
