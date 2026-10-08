# ADR AC0180: Selected invariant Int64 parsing

Recorded 2026-10-08 for AC5a.8.

The unchanged damage-replacement reader calls Int64.TryParse with NumberStyles.None
and InvariantCulture. Extend the existing exact parser boundary with one additional
method grant. Require the selected four-argument signature and out Int64, retain
primitive identity evidence, and check the constant style and direct invariant
provider at translation and both IR validation stages. Int32 remains Integer-only;
Int64 remains None-only. Dynamic/other styles, null/other providers, default overloads
and Flags formatting remain diagnostic. The grants are independently revocable.

An independently authored managed parser consumes ASCII digits and trailing NULs,
without signs or whitespace. At least one digit is required. Check unsigned
accumulation before multiplication/addition against Int64.MaxValue. Zero the output
first, and write all 64 bits only after full success. No native parsing, host culture,
allocation, public API substitution or full numeric-grammar claim is introduced.

Four probes supply 3,660 repeated O0/O2 native/.NET comparisons over boundaries,
leading zeroes, overflow, NUL/whitespace/sign/digit grammar, high/low output bits,
heap locations, null output receivers and formatting roundtrips. Another 131,648
managed-only comparisons cover authored inputs, every UTF-16 unit before/after a
number, and seeded signed numbers. Both IR stages reject four declaration/width
corruptions and two operand mutations. Removing only the Int64 grant denies Int64
while preserving Int32. Existing Int32 parsing, managed locations, core-library
checks and the 46-control/one-gap baseline pass: 72 distinct selected checks.
ASan/UBSan, forced GC and exact frame/cleanup checks remain enabled.

Review checked unsigned bounds, failed output reset, full-width copies, signature/
owner identity, same-block style/provider proof and per-method authorization.
CIL v76 and object IR v49 record the extension; native v74/layout v26/interface v16
and all production/diagnostic limits are unchanged. Main 48ecf14 remains an ancestor.
Evidence: artifacts/alchemy-int64-parse{,-refresh,-engine}.

Frozen discovery next rejects nonpublic GameOccurrence.Permits(PlayerId) class
dispatch at ShieldReactionDiscovery.Discover IL_00d4: 998,224 metadata units in
4,242 ms. StateLookup remains 15 methods/11,547 units (469 ms). Exact caller IL is
archived. Full engine, unchanged fingerprint and saved-game replay remain unfinished.
