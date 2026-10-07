# ADR AC0139: Literal default formatting for boxed integers

Recorded 2026-10-07 for AC7b.17.

After AC7b.16 (abcc9c6), frozen discovery reaches boxed numeric formatting in
Alchemy.Primitives.GridPosition.ToString IL_001b, through Distance.StepCost and
movement validation. The diagnostic copy charges 336,391 metadata units;
StateLookup remains eight methods/9,927 units. No engine execution is claimed.

Admit directly boxed Int32/UInt32/Int64/UInt64 arguments in one/two/three-position
String.Format only with a directly proven literal without any placeholder format
suffix. Preserve literal colons and escaped braces. Both discovery and translation
check the literal restriction; translation independently checks actual same-block
box producers. Dynamic/aliased format strings, object aliases, numeric suffixes,
narrow integers, Boolean and Char remain diagnostic. Existing enum/string/ordinary
value formatting keeps its broader admitted suffix behavior when no numeric box
occurs. The runtime retains its documented fixed invariant-culture contract.

Selected private integer renderers unbox exact scalar identities and call the
existing owned invariant decimal algorithms. They require BoxedTextRender's exact
grant. A defensive managed guard rejects nonempty suffixes on direct implementation
calls; admitted framework calls cannot reach that branch. The shared composite
parser continues to own alignment, escaped braces, exact syntax/index faults and
callback order. Repeated placeholders preserve the original box and object roots.
No host formatting, provider substitution or new native operation is introduced.

Integer CIL v57 records admission. Object IR v41, native v67, layout v25 and all
resource limits remain unchanged. Review covers scalar identity and all 64 bits,
bounded literal checking, actual operand proof, managed unboxing, UTF-16 output,
callback/fault order and precise roots. Full engine/fingerprint execution and
saved-game replay remain open.

All 60 distinct selected checks pass: 19 new/mixed checks plus 41 surrounding
boxed, paired, enum, numeric and public-baseline checks. Seven probes over 77 pairs
with three repetitions at O0/O2 supply 3,234 native/.NET comparisons; the advanced
public two-integer control adds 294. Forced GC, ASan/UBSan and exact frame quotas
remain enabled. Eight unsupported cases check their specific rejection messages.
The dynamic-format diagnostic was clarified after a stricter assertion found the
older wording; only that assertion was rerun after the wording-only change.
Evidence: artifacts/alchemy-boxed-integer-format/tests.log, regressions.log and
diagnostics-fixed.log. The public baseline now has 46 controls and one gap.
