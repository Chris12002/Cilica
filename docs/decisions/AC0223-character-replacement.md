# ADR AC0223: Replace UTF-16 characters

Recorded 2026-10-08 for AC8.79 after AC8.78.

Frozen SuppressTaggedEffectOperation.Label uses String.Replace(Char,Char) to replace
hyphens with spaces. Add that overload to StringCharacters using pinned
String.Manipulation.cs. Return the receiver before searching when both characters
are equal; return it after IndexOf reports no match. Otherwise copy the unchanged
prefix and replace matching UTF-16 units in the remaining range into an exact managed
Int32 buffer, then use the existing FromCodeUnits grant for a fresh string.

No code-point interpretation, culture handling or string-pattern replacement is
introduced. Existing checked reads and stores replace pinned pointer/SIMD copies.
Normal managed instance admission preserves null-receiver faults even when characters
are equal; argument evaluation precedes that fault. Compiler, runtime, profiles,
grants and production quotas remain unchanged (CIL106/IR63/interface20/native79).

## Validation and review

Isolated snapshot artifacts/alchemy-character-replace starts at 23ba473. All 12
focused/adjacent checks pass in 14.503s, including CharacterString constructor grant
and detached IR controls. Five new roots x 14 x 14 argument pairs x 3 repeats x O0/O2
provide 5,880 new native/.NET comparisons. Cover first/last/repeated replacements,
source immutability, original/fresh/empty identity, no-match and equal-character
shortcuts, NUL, both surrogate ranges, narrowing/wrapping inputs, null receivers,
throwing argument evaluation and repeated allocating replacements. String-pattern
and comparison overloads remain precise missing-method diagnostics.

ASan/UBSan, forced collection, exact root/borrow cleanup, exact/one-short frames and
2KiB frame warnings remain enabled. Build has only the existing unrelated fixture
warning. Review checked original identity shortcuts and prefix bounds against the
pinned implementation, zero-extended Char stores, exact result length and rooting
through buffer/string allocation. Production source/test bytes match the validated
snapshot, with no game changes or public API outside the exact overload.

## Frozen audit

All six immutable inputs match inputs-verified.json. Core SHA256
4975004c0bd5740d42b936974b9afe7d46511fc77ff65d415bc8385878554fde;
manifest 83da224eb5064a0b9df6e574c2f0f65f21d1afd9f82dcf802db266f58b1789d3.
With unchanged diagnostic allowances, engine discovery reaches CIL3010 missing
RemoveAll in Alchemy.Engine.Effects.ObstacleOverflow.Drain, method0x06000dcd IL0082,
member0x0a0006b3, requested by Breach IL00dc. Work is 3,379,372 units/17,852ms.
StateLookup validates 15 methods/11,742 units/445ms. Both Emission fields are null;
no engine emission, native compilation or execution. Receipts: artifacts/alchemy-
character-replace, -refresh and -engine. Full closure/replay and the separately
reduced polymorphic SortedDictionary duplicate-text gap remain open.
