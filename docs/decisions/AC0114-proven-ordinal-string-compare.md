# ADR AC0114: Proven ordinal String.Compare

Recorded 2026-10-07 for AC7b.14.

## Decision

After 269d01c, frozen engine discovery clears constrained enum comparison and
reaches String.Compare(String,String,StringComparison) in StatResolver.Compare
IL_004c. The same diagnostic bounds charge 218,037 metadata units; StateLookup
remains eight methods/9,919 units. No engine emission/execution. Evidence:
artifacts/alchemy-enum-compare-{refresh,engine} and the preceding stat-compare-il.txt.

Add the exact selected String.Compare declaration and StringOrdinalCompare grant.
Extend the existing ordinal text bridge discovery to share declaration, helper and
enum-shape checks with StartsWith. Translation requires an immediate proven Ordinal
constant in the same block, consumes that argument and invokes the existing managed
CompareOrdinal(String,String) helper. Both original string arguments are evaluated
in order before that call. The pinned .NET Ordinal path and CompareOrdinal share
identity/null handling and the same comparison helper, so exact integer results
are retained, including code-unit differences rather than only their sign.

Each bridge retains its own opcode, static/instance, parameter and result checks,
helper choice and grant. StartsWith's direct-call literal-receiver restriction
remains unchanged. Culture/ignore-case/unknown modes and aliases are diagnostic,
even when null/equal arguments could otherwise hide the missing mode validation.

Integer CIL v40 records the extension. No native/IR algorithm, layout, production
budget or application-input change. Existing native v64/layout v24 remain current.

## Validation and review

Four probes provide 4,338 repeated O0/O2 native/.NET comparisons: 400 text pairs,
225 code-unit pairs, identity and argument side effects. Cases cover null, empty,
prefixes, padding, embedded NUL, unpaired surrogates and high UTF-16 units. Five
negative mode/alias cases and grant removal preserve explicit rejection.

All 47 selected checks pass: 19 new/shared-prefix checks, then 28 text equality,
ordinal comparison, prefix-boundary and public-baseline regressions. Forced GC,
ASan/UBSan, 2,048-byte native frames and 30-second subprocess limits remain enabled.
Baseline stays 43 native controls/four gaps. Review checked exact source/helper
signatures, approved declaration identity, shared bridge separation, mode proof,
evaluation order, null behavior, metadata/argument budgets and existing prefix
restrictions. No outstanding finding. Main 48ecf14 remains an ancestor.
Full engine compilation and saved-game replay remain unfinished.

Evidence: artifacts/alchemy-ordinal-compare/tests-{a,b}.log and hashes.json.
Core: fbf06d7abd311dbc38fa5c8bb5939533165ef08bbd1c6581165dbddeb0ff1a6c.
Manifest: c4a28322af4a7772083cbe236b3e5796b0b72e6274fae81ba087ea6c973cdcb7.
Fixture: d55907d481a1d6964864275fbc4716c9e24a6b993dc14a03e4be361a0f42868f.
