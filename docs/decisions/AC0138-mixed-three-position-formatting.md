# ADR AC0138: Proven strings and three-position composite formatting

Recorded 2026-10-07 for AC7b.16.

After AC3.24 (f0a29aa), the frozen engine reaches String.Format(String,Object,
Object,Object) in EffectOperationDescriptor.Validate IL_0029 after 327,926 metadata
work units. Its arguments are String, boxed OperationSourceKind and String.
Evidence: artifacts/alchemy-array-indexof-engine/format-il.txt. No engine execution.

Extend the existing owned composite parser to three positions and exact-grant the
four-parameter String.Format declaration. Add a selected ordinary string renderer
that performs a checked string cast and preserves null. Strings ignore suffixes;
existing enum and ordinary non-IFormattable value renderers keep their behavior.
One/two/three argument helpers share alignment, escaped braces, error positions,
UTF-16 and callback ordering. Repeated positions render the same retained box.

Discovery scans at most 256 producer instructions backwards for up to three
arguments, stopping at unsupported operations or control-flow/exception entries.
Branch entry sets are cached per method; metadata resolutions use the existing
shared work budget. The scan supplies candidates, not authority. Translation
independently requires each actual value argument to be its exact same-block
BoxValue, and each string argument to have String or null storage. Object aliases
and joined boxes remain rejected. Existing one/two-box discovery is retained
without duplicate receiver validation. No general object or provider formatting.

Integer CIL v56 records the new admission. Object IR v41, native v67, layout v25
and production/diagnostic limits remain unchanged. Selected BoxedText.Render still
requires its independent grant, including the string specialization. Typed checked
casts and ordinary managed calls preserve existing detached IR validation/rooting.

Review covers bounded proof work, exact selected overloads, argument evaluation,
null handling, repeated-box identity, enum suffix dispatch and all error paths.
An initial test requested 1,000,001 padding characters, exhausting the existing
native allocation bound; an invalid eight-digit width now tests parser errors
within budget. No runtime limit was raised. Full engine/fingerprint execution and
saved-game replay remain open.

All 43 selected checks pass, covering the new mixed/triple probes, previous boxed
and paired formatting, enum formatting and the 45-control/two-gap public baseline.
Eight probes over 112 pairs with three repetitions at O0/O2 supply 5,376 new
native/.NET comparisons, forced GC, ASan/UBSan and exact frame quotas. Six negative
flow/format cases and separate triple/renderer grant removal remain diagnostic.
Evidence: artifacts/alchemy-mixed-format/tests-fixed.log.
