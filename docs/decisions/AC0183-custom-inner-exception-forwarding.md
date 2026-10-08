# ADR AC0183: Custom message/inner exception forwarding

Recorded 2026-10-08 for AC8.57.

Frozen DistanceFromFeet constructs internal EffectiveSpellCompilationException
with a message and caught OverflowException. Its constructor forwards both values
to Exception(String,Exception). Add that exact selected ExceptionConstructor grant
and permit fieldless custom chains ending directly at Exception. Validate public
or same-assembly internal access, ordinary body shape, every unchanged ldarg and
exact direct-base signature. Do not execute inputs or skip additional behavior.

Reuse the traced inner slot and ordinary message constructor lowering. A shared
bounded identity calculation admits Exception and its custom forwarding families,
plus the already supported exact InvalidOperationException. It excludes descendants
crossing other framework resources/runtime faults; TypeInitializationException
keeps its special path. Both IR stages and native make_exception use these same
identities. Null/default messages, UTF-16, identity, allocation origin and quotas
remain unchanged. No new physical layout, generic capability or limit is added.

Seven authored probes plus the retained original base-exception reduction provide
2,352 repeated O0/O2 native/.NET comparisons. They cover null/empty/unpaired UTF-16
messages, inherited/internal constructors, retained chains, caught runtime faults,
filters, rethrow identity and left-to-right argument faults. Two native GC checks
verify inner/message roots, allocation failure preserving the pending origin,
corruption rejection and cleanup. Exact two-argument grant denial, constructor
side effects/replaced arguments and five detached payload mutations are checked.
All 18 new and 72 neighboring exception/public-baseline checks pass (90 distinct).
ASan/UBSan, forced collection and existing frame/subprocess bounds are retained.
All 69 pinned source hashes verify; Exception.cs supplies the message/inner contract.

Review checked constructor cache access, exact parameter forwarding, bounded
ancestry, selected wrapper identities, native payload ownership and fault roots.
Integer CIL v79, logical/scalar exception IR v9 and native v75 record the extension.
Object IR v51 and physical layouts are unchanged. Local main 48ecf14 is an ancestor.
Evidence: artifacts/alchemy-custom-inner{,-refresh,-engine}.
Core SHA256: 417ba4696436b0d741550a270517929d0028497abc23c22e1c61db006fc3fe8f.
Manifest: 9f2da2462f00e694cc1958da148c36c2c2e8923a8ebd5e8f712a63712de03fb6.

The frozen engine now stops at missing EqualityComparer<T> mapping in
SpellResolutionHandler.Contains IL_0004, after 1,039,295 metadata units/7,293 ms.
StateLookup remains 15 methods/11,566 units (708 ms). Production limits still reject
the broad engine; the isolated diagnostic compiler emits/executes no engine code.
Full engine, unchanged fingerprint and saved-game replay execution remain unfinished.
