# ADR AC0073: Lexically visible nongeneric delegates

Accepted 2026-10-07 for AC2.5.

The frozen engine constructs a private CommandHandler delegate. The integer-object
profile now retains lexical declaration chains on delegate metadata and admits
nonpublic delegates only when the complete type/owner chain is nongeneric. Public
exactly closed delegates keep their existing support. Declaration validation still
requires the selected MulticastDelegate base, a sealed fieldless type, exact runtime
constructor/Invoke declarations, supported signatures and no initialization or
native layout. Application-specific exceptions are not introduced.

Source operand checks and generic argument checks retain their existing access
rules. Logical and scalar IR validate declaration-chain shape, visibility, generic
arity and global consistency with methods, layouts and static fields. Construction,
Invoke, type tests and reference-array allocation use the shared lexical visibility
rule. A delegate may be transported as a generic argument without granting its
private name to unrelated callers. Scalar signature expansion preserves these facts
with the existing record copy; native payloads, ownership and runtime code do not change.

Eleven probes exercise internal/private declarations, static/instance captures,
arrays, concrete lists, casts, null/wrong-type values, reference/void results,
exception identity, deeper nesting and reference-bearing value parameters/results.
They pass 3,234 repeated O0/O2 native/.NET comparisons under forced GC, ASan/UBSan,
exact cleanup and unchanged 2,048-byte frame/30-second subprocess limits. Seven
metadata mutations fail at both IR stages, foreign-image casts fail in discovery,
and hidden generic delegates remain diagnostic. Older delegate profiles still
reject hidden declarations.

Review checked visibility at each executable operation, source image identity,
consistent ancestry, signature expansion and unchanged exact target signatures.
Integer-object CIL advances to v18, delegate IR to v7, scalar delegate IR to v5,
object IR to v26, layout schema to v21 and native profile to v52. The schema addition
is metadata only; runtime algorithms, physical payloads and resource limits stay fixed.

All 181 distinct selected checks pass: 18 focused, 35 existing logical/declaration
checks and 128 adjacent delegate/model/public-baseline checks. Evidence:
artifacts/alchemy-hidden-delegates/ac2e2-tests.log, ac2e4-tests.log and ac2e5-tests.log.
The public baseline remains 31 native controls and 16 diagnostic gaps.

Frozen EngineStep passes handler construction and advances to UnitRoster::.ctor
IL_000c via PrepareForfeit, where discovery reaches the 256-snapshot bound (27 value,
217 reference and 8 vector explicit roots). StateLookup remains at 257/256 catalog
identities. Full engine execution and saved-game replay remain unverified.
Evidence: artifacts/alchemy-hidden-delegates/engine-and-state.json.
Core SHA-256: 69356718f55f03402865ad808087b2e78e0bbe321fed5ff6efca2a8f180a2b31.
Manifest SHA-256: 0bd7b4e879e05610542f3fdddf5faa94f96e7df086e3d6dfd0d31991ad5b4b8f.
