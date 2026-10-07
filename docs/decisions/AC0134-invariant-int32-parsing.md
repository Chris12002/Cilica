# ADR AC0134: Selected invariant Int32 parsing

Recorded 2026-10-07 for AC5a.4.

After committed AC7a.5 d49e574, frozen discovery reaches NumberStyles/Int32.TryParse
in EffectParameters.RequireNumber after 322,192 metadata work units. StateLookup
remains eight methods/9,923 units. No engine emission/execution is claimed.

Admit only Int32.TryParse(String, NumberStyles, IFormatProvider, out Int32) with
constant NumberStyles.Integer and directly proven CultureInfo.InvariantCulture.
The exact selected declaration needs the new InvariantIntegerParsing grant.
Retain selected primitive identity and parser-signature evidence on the method
binding; both IR stages validate it and recheck style/provider provenance. Other
styles, unknown styles, null/other providers and default/span overloads remain
build errors. Preserve NumberStyles constants and FlagsAttribute metadata so the
existing unsupported flags-formatting diagnostic cannot be accidentally bypassed.

Use a Cilica-owned ordinary managed parser, without host parsing or native calls.
Consume optional ASCII leading whitespace, one invariant sign, ASCII digits,
optional ASCII trailing whitespace and trailing NULs. At least one digit is
required. Bound unsigned accumulation before multiplication/addition against
2147483647 or 2147483648; only successful parsing writes the signed result after
the initial zero. This preserves Int32.MinValue and clears every failed result.
Separate final stores keep the out location out of a branch stack join. No
allocation occurs inside the parser. Managed argument evaluation and existing
location rooting preserve heap output references and null-output faults.

API reference: [Int32.TryParse](https://learn.microsoft.com/en-us/dotnet/api/system.int32.tryparse?view=net-10.0)
and [NumberStyles](https://learn.microsoft.com/en-us/dotnet/api/system.globalization.numberstyles?view=net-10.0).
The measured oracle is .NET 10.0.11. No source implementation was copied.

All 43 selected checks pass. Four probes supply 2,892 repeated O0/O2 native/.NET
comparisons with forced GC and ASan/UBSan. A separate managed check supplies
131,648 cases: 64 authored inputs, each UTF-16 code unit before/after a number,
and 512 seeded numeric cases. That exhaustive character sweep is managed-only
evidence. Three declaration mutations and two operand mutations are rejected at
both IR stages; exact grant removal and five unsupported API/style/provider/text
probes fail closed. Numeric formatting, enum formatting and all 45 public controls
with two retained gaps pass. No production limits or test budgets changed.

Review covered overflow, whitespace/NUL boundaries, negative zero, failed output
reset, alias rooting, admission provenance and preserved enum flags metadata.
Integer CIL v54 and object IR v41 record the extension; layout v25/native v67 stay
unchanged. Evidence: artifacts/alchemy-invariant-int32-parse. Full engine execution,
frozen fingerprint execution and saved-game replay remain open.
