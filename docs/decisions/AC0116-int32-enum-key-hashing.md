# ADR AC0116: Int32 enum default key hashing

Recorded 2026-10-07 for AC3.19.

## Decision

Frozen 195ac06 clears the internal enum token and reaches Dictionary.Hash through
VictoryEvaluators.Resolve/TryGetValue/Find after 228,369 metadata units. Enum keys
were being sent to constrained struct override resolution. StateLookup remains
eight methods/9,921 units. No engine native emission/execution. Evidence:
artifacts/alchemy-nonpublic-enum-{refresh,engine}.

Within the existing exact, core-private ClosedKeyHash grant, select the owned
Int32.GetHashCode managed body for a validated public nongeneric Int32 enum key.
This matches the pinned upstream Enum.GetHashCode underlying-type contract.
The normal managed call uses the same Int32 location carrier, reads the original
value and preserves all bits. No new intrinsic, box, runtime algorithm or IR form.
Integer CIL v42 records admission; native v64/layout v24 remain unchanged.
Generic argument visibility, nullable semantics and non-Int32 enum bounds remain.

This does not yet make Dictionary<Enum,T>/HashSet<Enum> executable: their closures
contain enum collection interfaces, and runtime equivalence between enum arrays
and underlying integer arrays is still unsupported. Four reduced collection probes
pin this dependency. A class-only declaration workaround was reviewed and rejected:
object-to-enum-interface casts may observe an Int32 array at runtime. Keep the
existing diagnostic until equivalence is represented correctly end to end.
Duplicate-value alias formatting remains a separate reduced negative test.

## Validation and review

A core-private test witness calls the granted hash directly; its native O0/O2
results match .NET Enum.GetHashCode for 294 repeated comparisons, including signed
extremes and unnamed values. Removing ClosedKeyHash rejects that same witness.
Nine focused checks pass, including four collection dependencies, other enum widths,
hidden generic arguments and duplicate-name formatting. Test-helper failures now
include the fixture method name. All 22 selected dictionary, hash-set, nullable-key
and public-baseline regressions pass: 31 checks total. Forced GC, ASan/UBSan,
2,048-byte frame warnings and 30-second subprocess caps remain enabled. The public
baseline stays 43 native controls/four gaps. All six frozen application digests
still match. Main 48ecf14 remains an ancestor; review has no outstanding finding.

Review checked the selected declaration/assembly/grant, validated enum shape,
byref scalar ABI, evaluation/fault behavior and unchanged array-interface rejection.
Production limits and frozen application inputs are unchanged. Full engine
compilation and saved-game replay remain unfinished.

Evidence: artifacts/alchemy-enum-keys/tests-{d,e}.log and hashes.json.
Core: fbf06d7abd311dbc38fa5c8bb5939533165ef08bbd1c6581165dbddeb0ff1a6c.
Manifest: d79a9a5c17d752f21aa560fcb36ebb15ce2037956110736436966e880bdfad3a.
Fixture: 344e5a51c286e528b1893e1fa116abcef29b01d7df33ab8e0668b4e1431872b0.
