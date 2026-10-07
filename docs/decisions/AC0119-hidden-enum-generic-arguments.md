# ADR AC0119: Authorized hidden Int32 enum generic arguments

Recorded 2026-10-07 for AC5b.13.

## Decision

After b9c6ec4, frozen discovery passes the custom message constructor and reaches
Nullable<WaveKind> in EffectOperationRegistry::.cctor IL_0000 after 231,245 metadata
units. WaveKind is internal to the caller's assembly. StateLookup remains eight
methods/9,921 units. No engine native emission/execution. Evidence:
artifacts/alchemy-exception-messages-{refresh,engine} and effect-registry-il.txt.

Allow a validated hidden nongeneric Int32 enum as a generic argument when the
source caller can name its declaration. Explicit source uses retain the existing
assembly/private lexical checks before substitution; hidden inline structs and
other enum representations remain outside this extension.

For resolved operands inside generic bodies, an enum can also be carried opaquely
through the method/owner's already-bound arguments. A bounded 4,096-node walk
checks exact declaration identity through constructed arguments/vectors. This
permits copies, boxing and nullable helper operations without granting foreign
code access to the enum's explicit name. Declaration checks never use opaque
argument authority: foreign explicit box/type-token/local uses still fail even
when T is bound to that same enum. Final nominal box checks use the same proof.

Reuse the existing Int32 carrier, exact nominal boxes and Nullable managed helper
bodies. Integer CIL v45 records source admission. Native v65/layout v24, exception
IR v7, grants, owned core and production limits are unchanged. Enum dictionary/set
closures now pass their hidden argument check and retain the separate enum-array
interface diagnostic; this does not claim those collections execute.

## Validation and review

Six native probes provide 1,764 repeated O0/O2 comparisons: internal/private holders,
nullable absent/present round trips, private boxing/copies and a foreign generic
box helper. The raw foreign probe checks nominal isinst as well as copied bits.
Three malformed foreign source uses are rejected despite receiving the hidden
enum as T. A hidden-struct fixture retains its original boundary.

All 87 selected checks pass, including generic/nullable/access/enum-key/public
baseline regressions. All 11 focused checks also pass after strengthening nominal identity coverage.
Forced GC, ASan/UBSan, 2,048-byte frame warnings and 30-second subprocess caps remain.
Public baseline: 43 controls/four gaps. Review checks pre-substitution source access,
exact opaque identity, bounded traversal, nominal box lowering and unchanged struct/
array/member boundaries. Main 48ecf14 remains an ancestor. Full engine compilation
and saved-game replay remain unfinished.

Evidence: artifacts/alchemy-hidden-enum-generics/tests-{b,c}.log and hashes.json.
Core: fbf06d7abd311dbc38fa5c8bb5939533165ef08bbd1c6581165dbddeb0ff1a6c.
Manifest: 13c0ff6b3612e71a054012dffdd28a82150c129ee768a31d519aa21ff5e858b8.
Fixture: ea27083f7b3983359555b8812809ffe5650bc1a1dc2b0482f25d62021afa03c5.
