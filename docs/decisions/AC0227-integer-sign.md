# ADR AC0227: Signed integer Sign

Recorded 2026-10-09 for AC8.83 after a604f53 (AC8.82).

Frozen BarrierOperation.Span 0x06000d47 IL_0045 needs Math.Sign(Int32) to turn
coordinate differences into directions. Add Sign(Int32) and Sign(Int64) to the
existing signed integer Math profile. Return -1, 0 or 1 through managed
comparisons; never negate a minimum signed input. Existing integer CIL/native
lowering supplies the implementation. No new capability, profile, runtime or
production-budget change (CIL108/IR64/interface20/native80).

## Converted reference

The separately pinned Alchemy C++ capture has BarrierOperation.Span's two Int32
Sign calls at Alchemy.Engine__7.cpp lines 16253/16260, after managed unchecked
coordinate subtraction. SHA256:
`97384aa23802e2a960a590c4131b3d659aaba9ed854abf845c583c5d940e4521`.
The generated Int32/Int64 Sign definitions are at mscorlib__3.cpp lines 6957-6972,
SHA256 `084084beb3ee910081566af2a9587d763d3e2421dcc30cbea26474b4afc13359`.
These are under the existing alchemy-reference-20261008T2312Z generated cpp
directory. The reference defines the intended result, while Cilica's comparison
implementation avoids depending on the converted code's signed negation/shift
expression. No Unity runtime or implementation is copied. Captured DLLs and
frozen audit DLLs remain separate evidence.

## Validation and review

Snapshot artifacts/alchemy-integer-sign starts at a604f53. All 10 IntegerSign and
adjacent IntegerMath checks pass in 13.967s on macOS ARM64. Four new roots x
54 argument pairs x three repeats x O0/O2 = 1,296 new native/.NET comparisons.
Cover signs, zero, both signed minima/maxima, composed Int64 bit patterns,
unchecked negation and barrier-style wrapped coordinate subtraction contrasted
with wide subtraction. Exact overload admission verifies only Int32 and Int64
Sign map; other overloads remain diagnostic.

ASan/UBSan, forced collection, exact root/borrow cleanup, exact/one-short frame
budgets and 2 KiB frame warnings remain enabled. Native source/executable hashes
are in native-evidence; build has only the existing unrelated fixture warning.
Review checked signed comparison widths, Int32 result storage, no minimum-value
negation in Sign, unchanged overload mappings and no platform dependency. No
remaining findings. No full suite or hosted tests ran.

## Frozen scan

The scan passes Sign and reaches 8,192/8,192 diagnostic snapshot fields, needing
four more for Engine type 0x020001cb through TransferFirstComponentOperation.Prepare
0x06000fa1 IL_00be. There are 3,791 snapshots at the last counter. This first scan
charges 3,717,195 metadata units in 26,975ms. StateLookup validates 15 methods and
11,742 units in 478ms. Both emission fields are null; full engine C++ was not run.

Core SHA256: `193328b773446566636b2f11c3505a01647c017260a36f1ea91ce4c662651b98`.
Manifest SHA256: `0dbd32a027c858197c0b504bd58826d399b7c9eab8896efdff69ee5729f7e34a`.
All six frozen inputs remain unchanged. Receipts are in
artifacts/alchemy-integer-sign-refresh and -engine. Full closure and saved-game
replay remain incomplete.
A follow-up raises only the copied diagnostic MaximumFields to 16,384. Discovery
then reaches Guid.TryParseExact in InspectShieldComponentPrimitive.ParseOption
0x06000f09 IL_0029, through InspectShieldOperation.ApplyToSpell 0x06000f0d IL_0022.
Engine work is 3,728,691 units in 27,034ms; StateLookup remains 15 methods/11,742 units
in 451ms. Both Emission fields remain null. Production field limits and the tested
compiler are unchanged. fields16384.json and fields-source-hashes.json preserve
this follow-up separately from the original 8,192-field scan.
