# ADR AC0189: Pinned invariant string lowercasing

Recorded 2026-10-08 for AC7b.22.

The unchanged frozen engine calls String.ToLowerInvariant from its construction
contribution closure. Implement exactly that member using the pinned TextInfo.cs
identity contract and pal_casing.c invariant simple mapping. ASCII with no capitals
returns the original string; empty returns Empty; every other input allocates a
fresh same-length result, including unchanged non-ASCII and lone surrogates.
Decode valid high/low pairs, map the scalar, then re-encode at the same width.
Unpaired surrogates remain code units. U+0130 stays unchanged. No contextual Greek
sigma handling, expansion, normalization or culture substitution is introduced.

Pin UnicodeData 16.0.0 with its license and digest. The generator reads simple lower
field 13, excludes U+0130, validates unchanged UTF-16 width, and verifies exact
expansion of 1459 mappings grouped into 186 ranges. Eight small managed decision-tree
pages avoid static data roots, per-lookup allocations and OS library dependencies.
The existing checked StringConstruction copies a temporary Int32 code-unit vector;
the source string and vector are rooted by ordinary managed call-frame ownership.
No new privilege or compiler/native profile is needed (current CIL83/IR54/native76).
Culture overloads, uppercase and case-insensitive comparison remain outside this slice.

Validation: 14 new checks pass. The compiled managed lookup matches .NET10.0.11 on
macOS ARM64 for all 1,114,112 code-point values (including surrogate code units),
comparing every output unit and width. Unicode15.1 was independently found to differ
on 27 values, so it is not substituted. Native exact scalar probes cover 2167 changed
characters and neighbors (13002 repeated comparisons); 544 string-block hashes cover
the complete range (3264 comparisons); identity/mixed/null probes add 1890. Total
18156 at O0/O2 with ASan/UBSan, forced collection, frame exhaustion and exact cleanup.
The whole-string sweep compares hashes, distinct from the exact scalar/table checks.
Another 84 adjacent string checks pass (98 selected total). All 76 pinned .NET
source hashes verify and generated Unicode output reproduces exactly. No full suite
or non-macOS execution claim; a host with different Unicode data may differ, while
Cilica's pinned table is deterministic across targets.

Review checked pair boundaries, lone-surrogate preservation, invariant exceptions,
identity, overflow bounds, no retained table state, and generated lookup expansion.
Local main 48ecf14 remains an ancestor. Shared dictionary work is excluded from the
frozen probe: core sources are read from 6e04948 plus only these two casing files.
Evidence: artifacts/alchemy-invariant-lower{,-refresh,-engine}. Core SHA256
4d31a76ec1917f63249c18578e098d34a71afb13aa6739469690a8b922b7228a;
manifest 8d93e2f55cb28247645e9985323fdca4464c7f644441c8678b493d24e0ba45a0.

Frozen discovery now exhausts aggregate locals: 10345/12288 parameters and 6145/6144
locals, at ContinueShieldContribution token 0x06001085 IL_0597. It charges 1067235
metadata units/7424ms. StateLookup remains 15 methods/11590 units/817 ms. No engine code
is emitted or executed. Full engine, unchanged fingerprint and saved-game replay
remain unfinished. Next is a measured diagnostic-only local-budget expansion.
