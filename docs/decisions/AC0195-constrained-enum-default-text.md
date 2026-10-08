# ADR AC0195: Constrained default enum text

Recorded 2026-10-08 for AC7b.23, following AC5a.11.

## Design and boundaries

Frozen ContinueConstructionContribution's callback calls constrained
ElementGender / Object.ToString at IL_0063, then String.ToLowerInvariant. Authenticate
the adjacent constrained/callvirt pair, exact selected Object.ToString declaration
and grant, Int32 enum identity/access, unique constants and attribute restrictions.
Normalize metadata's unknown kind to the validated value kind before closing the
helper, and register the existing scalar payload descriptor for enum tables. No box
is allocated. CIL lowers the receiver location load and ordinary managed helper
call; receiver faults occur before formatting. No new IR/native operation is needed.

A separate public managed EnumDefaultText<T>.Default(Int32) bridge owns a private
per-closed-type name cache. Build the array fully before publishing; failures can
retry without exposing partial state. Private EnumText<T> metadata helpers remain
private, including their lexical admission checks. The exact additional
Object.ToString EnumFormatting grant does not authorize arbitrary object rendering.
The bridge uses only already-authorized private metadata operations and ordinary
managed storage; it does not grant external access to those operations.

Pinned .NET and a managed identity oracle show repeated named values return the
same name object; another enum's equal name and string literals are distinct.
Unnamed values share existing integer cache entries for 0..299 and produce fresh
text outside that range. Use IntegerText.Int32 for that path. This preserves the
existing fixed-invariant numeric profile; no allocation-count or threshold parity
is claimed. Flags, duplicate values, custom attributes, other widths, general boxed
calls and direct enum-vector element addresses remain diagnosed. Copied vector
values and local/field/generic receivers are covered. No game source/rule changes.

## Validation and review

All 77 selected direct/boxed-format/enum-compare/constrained-value/reference checks
pass in 99.604 seconds. Nine direct probes add 3030 repeated O0/O2 native/.NET
comparisons: names, empty enums, signed/unnamed values, cache/literal identity,
separate enum types, private enums in closed generic calls, mutable fields/copied
vectors, null receiver order and lowercasing. Two additional native checks sweep
129 allocation limits apiece and retry with full quota, asserting exact roots and
no retained partial arrays. Existing malformed enum-table IR checks pass at both
stages; new exact grant denial and unsupported-shape checks pass. ASan/UBSan,
forced GC, frame exhaustion, exact cleanup and subprocess bounds are retained.

Initial discovery exposed unknown-kind generic metadata, then a missing scalar
layout registration; both are now explicit. A fixture's direct enum-array borrow
was split into an expected diagnostic and copied-value control. A regression caught
public exposure of the old metadata helper; the separate bridge preserves its
existing private boundary. Review checked these paths, per-instance static roots,
publication after successful copies, exact declaration/grant selection, immutable
constant tables and unchanged ordinary constrained dispatch.

Evidence: artifacts/alchemy-enum-direct and artifacts/alchemy-enum-direct-audit.
Compiler base 8cb2cc5 plus owned changes; CIL88/IR57/native78. No production or
diagnostic limit changes. All 85 pinned source hashes and six frozen inputs match.
Core bab84c2708edac54bae178cf817634ae4078e4af7c1d3ad8d898d536b5ba81a4.
Manifest 067dad975eb1f6787292e1bbc211ebc3b963fce6fc078c5b46c48cf07f4302c2.
Frozen discovery reaches SpellContributionSources.TryAdd token 0x060003f3 IL_010d:
12290/12288 parameters, 7138/8192 locals, 1172621 metadata units/4772 ms. StateLookup
translates 15 methods/11616 units/456 ms. Evidence: artifacts/alchemy-enum-direct-
{refresh,engine}. No engine emission/execution; full engine, unchanged fingerprint
and saved-game replay remain unfinished. Main 48ecf14 remains an ancestor.
