# ADR AC0140: Closed reference receivers for constrained calls

Recorded 2026-10-07 for AC8.37.

After AC7b.17 (0c216eb), frozen discovery reaches a constrained reference call in
EffectSchedule.Group IL_0028 through ValidateAuthored IL_0021. The diagnostic copy
charges 340,517 metadata units; StateLookup remains eight methods/9,927 units.
Evidence: artifacts/alchemy-boxed-integer-format-engine. No engine execution.

Normalize the constrained operand identity before distinguishing reference and
value receivers. Preserve the existing exact value-body path. For an admitted
reference, retain the closed receiver type, prefix offset and explicit argument
count. Require an instance declaration and the adjacent constrained./callvirt
pair. Translation requires a managed location of exactly that reference type;
managed byref covariance and ordinary reference-stack operands are rejected.

After argument evaluation, load the reference from that location, then use the
existing ordinary call lowering. Existing interface/class virtual dispatch and
receiver null checks apply. The normal rooted LoadLocation operation preserves
heap owners and loaded references across allocation; no receiver box, new native
operation or dispatch rule is introduced. Integer CIL v58 records admission;
object IR v41, native v67, layout v25 and all resource limits remain unchanged.

Roslyn sometimes emits a value/reference test and a managed-location stack join
when evaluating a generic call receiver before effectful arguments. That separate
join remains unsupported. Three replacement probes explicitly pin this boundary;
heap receiver replacement before entering a simple constrained helper is executed.
Do not claim general managed-location joins or boxed primitive fallbacks.

Review covers exact location type, metadata operand identity, argument/receiver
order, interface and class dispatch, forced-GC rooting and unchanged value calls.
Malformed ordinary-reference stacks, wrong prefix types, branch entry into a
prefix bundle and combined tail prefixes are rejected. The IL decoder rejects
branch entry before lowering, so its diagnostic is the expected boundary.
Full engine/fingerprint execution and saved-game replay remain open.

All 64 distinct selected checks pass: 14 new reference-call checks, 14 existing
constrained-value translation checks and 36 surrounding borrow, dispatch and
public-baseline checks. Seven probes supply 2,058 repeated O0/O2 native/.NET
comparisons with forced GC, ASan/UBSan and exact frame quotas. Four malformed
prefix/location cases fail at their intended gates. Three managed-location join
probes remain diagnostic. Evidence: artifacts/alchemy-constrained-reference/
tests-fixed.log, regressions.log and diagnostics-fixed.log. The final rerun only
corrects the expected early decoder diagnostic; native results are unchanged.
