# ADR AC0213: Measured interface-body discovery allowances

Recorded 2026-10-08 for AC8.69 after boxed variance 3baf279.

The completed initial interface plan reports 10,797 distinct bodies/declarations/
mapping identities against the diagnostic 8,192 allowance. Raise only the copied
compiler's method allowance to 12,288. While loading selected implementations, it
then reports 16,385/16,384 aggregate parameters and 9,675/12,288 locals. The bounded
scan charges 2,534,163 aggregate metadata units in 10,246ms; StateLookup remains
15 methods, 11,616 units in 478ms. Evidence: artifacts/alchemy-methods-12288.

Raise only diagnostic aggregate parameters to 32,768. The scan now reaches a real
library gap: netstandard type token 0x02000048, System.Decimal. The requested game
method token 0x06000f3e is SpellRouteSelectionInstruction.Run, reached from
EffectProgramRunner.Run. Metadata-only token inspection identifies both exactly.
The frozen IL uses Decimal.op_Implicit(Int32) at IL_040a/041b, op_Multiply at0420,
Decimal(Int32) at0426, op_Division at042b and ToString(IFormatProvider) at049c. The
source calculates remaining route feet from two Int32 quantities divided by two,
then stores invariant text in the effect bindings. No game source or frozen IL is
changed to hide this unsupported numeric family. Source inspection finds one Decimal
arithmetic expression in the engine; this is not a proof of every reachable library
or future application Decimal use. Evidence: artifacts/alchemy-contract-token
(route-il.json is CLI text disassembly despite its historical extension).

The final scan charges 2,591,247 aggregate metadata units in 10,482ms; StateLookup
remains 15 methods, 11,616 units in 462ms. Evidence: artifacts/alchemy-parameters-32768.
All probes use the reviewed 3baf279 compiler snapshot and exact owned core/manifest:
bb90c5bc200ea0868b736424616921d033d190079dc444933d369f4b5acd3409 and
d9a1ab0108ff3f6fef6e01a6b3c1256fd124d17451dba531bc9666b9b657046c.
The later independent ArraySegment commit is not included in these frozen receipts.

Review confirms only ignored diagnostic copies change. Current diagnostic allowances
are types 3,072; methods 12,288; aggregate parameters 32,768; locals 12,288; fields
6,144; instructions 262,144; metadata work 4,194,304. Production types/methods/
parameters/locals/fields remain 256/512/4096/4096/4096; instructions and metadata
remain 65,536. Per-method limits and 60-second probe bounds are unchanged. No profile,
core body, runtime or native execution changes. No tests mirror this documentation
update. Full closure/IR admission, native execution, fingerprints and replay remain
unfinished; Decimal requires a separately designed and tested implementation.
