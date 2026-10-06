# ADR AC0071: Three- and four-string concatenation

Accepted 2026-10-07 for AC7b.7.

String.Concat now admits its three- and four-string overloads through ordinary
owned IL. The pinned .NET 10.0.11 null/empty reductions preserve original identity
when one input contributes and the canonical empty result when none contribute.
Nonempty inputs use an Int64 combined length, the existing exact too-long fault,
a rooted Int32 UTF-16 buffer and the already granted FromCodeUnits construction.
The array overload shares the length guard, retaining its snapshot/retry logic.
No object/enumerable overload, new grant or allocation-limit expansion is implied.

The new three-string body exposed an overly broad intrinsic recognizer: every
three-argument String method was treated as the ordinal Equals candidate.
OrdinalStringCall now filters the declaration name before resolving StringComparison
and retains every exact signature, enum, constant-proof and grant check for Equals.
The new Concat probes reduce this failure; existing denied-grant and ordinal-mode
checks remain green. No IR/native profiles change.

Six probes pass 11,664 repeated O0/O2 native/.NET comparisons covering all 81
null/empty/nonempty four-input patterns, three-input patterns, original identity,
embedded NULs, surrogate code units, repeated allocation and exception messages.
Forced GC, ASan/UBSan, exact cleanup and the 2,048-byte frame bound pass. The existing
array-concat overflow probe exercises the shared length guard without constructing
multi-gigabyte strings. All 71 selected string, ordinal and public-baseline checks
pass. Evidence: artifacts/alchemy-fixed-concat/ac7g3-* and ac7g4-tests.log.
Review checked ordered shortcuts, signed-overflow avoidance, copy offsets, rooting
across allocation and unchanged intrinsic authorization.

Frozen EngineStep passes formatting and reaches a private containing-type helper
call: OccurrenceReactionPayload.IsLegalSelection IL_00a1 calls
AlchemyGameEngine.IsLegalBlockSelection. StateLookup remains at 257/256 discovered
identities. Full native engine/replay is unverified; the public baseline stays 31/16.
Evidence: artifacts/alchemy-fixed-concat/engine-and-state.json and
occurrence-selection-il.json. Core SHA-256:
69356718f55f03402865ad808087b2e78e0bbe321fed5ff6efca2a8f180a2b31;
manifest: 106f60978d928e52c3190535df1229b3c834d7d47de6caee78b52de3302d0ec2.
