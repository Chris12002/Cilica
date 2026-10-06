# ADR AC0072: Private calls from nongeneric nested types

Accepted 2026-10-07 for AC8.20.

OccurrenceReactionPayload calls a private static helper declared on its containing
AlchemyGameEngine. The integer-object profile now admits private members when the
caller's nonempty, validated lexical chain contains the target owner's exact chain
as a suffix in the same image. Both owner specializations and every chain node's
generic arity must be empty/zero. This is directional containment: it does not grant
access to siblings, children, unrelated owners or matching tokens in another image.
Direct initializer calls remain forbidden. Existing public/assembly and protected
base-constructor rules retain their separate checks.

The shared method-access rule is used by discovery and the logical/scalar validators.
It covers ordinary static/instance calls, private construction and delegate targets;
argument/receiver/constructor-prefix checks remain unchanged. Closed generic methods
on nongeneric owners are supported through their existing signature validation;
generic owner ancestry remains diagnostic. Matching complete suffixes avoids treating
a shared ancestor as private-member access. Both stages retain global consistency
checks across lexical declaration facts.

Nine authored probes pass 2,646 repeated O0/O2 native/.NET comparisons with forced
GC and ASan/UBSan. They cover deeper nesting, private constructors, null receiver
checks even when a method ignores this, generic static methods, ref/enum borrows,
exception identity and callbacks. Noncached cases have exact cleanup; callback-group
retention is stable across repeats. Four detached mutations fail in both stages:
missing caller/target chains, generic ancestry and PrivateScope flags. Metadata
checks reject sibling/parent/unrelated/foreign access, matching foreign tokens,
generic owners and the older exception profile. The former nested-enum private-call
negative is now an explicit positive and a native probe. All 17 slice checks and
63 adjacent checks pass (80 distinct selected checks), including protected constructors,
internal interfaces, lexical facts and the public baseline.
Evidence: artifacts/alchemy-private-calls/ac8t1-* (reduction), ac8t2-* and ac8t3-*.

Review checked image identity, directional lexical containment, complete suffix
consistency, generic-owner exclusion, initializer rejection and unchanged receiver/
constructor checks. Integer-object CIL advances to v17, object IR to v25 and native
profile to v51; no native runtime algorithm, physical layout or resource limit changes.

Frozen EngineStep passes selection and advances into AlchemyGameEngine.CreateHandlers
IL_0012 through the type initializer. It now encounters an application delegate that
fails the public-declaration restriction. StateLookup remains at 257/256 discovered
identities. Full engine/replay is unverified; the public baseline remains 31/16.
Evidence: artifacts/alchemy-private-calls/engine-and-state.json. Core SHA-256 remains
69356718f55f03402865ad808087b2e78e0bbe321fed5ff6efca2a8f180a2b31;
manifest: b94eedfbd4758b27ae22ed22b4ad78d54f2fa2ad214d85210ae082b716016b61.
