# ADR 0136: Closed generic delegates and the owned Action contract

Status: Accepted

## Decision

Extend delegate discovery to exact closed generic declarations and supply the
owned System.Action<T1,T2> contract. Substitute constructor/Invoke signatures
through the existing bounded generic environment. Preserve the content hash,
definition token and specialization key in the delegate identity, including when
multiple specializations have identical Int32 call signatures.

Retain the current single-target representation: Int32 parameters, Int32 or void
return, and accessible static or nonvirtual instance targets. Validate ordinary
constraints and variance positions against the exact selected MulticastDelegate
base. Share the bounded variance walk with interfaces; for delegates, Invoke is
the callable signature. Legacy asynchronous helpers are not admitted by discovery
and their signatures are not prerequisites for Invoke. Unknown classes and
foreign lookalike bases do not acquire variance privileges.

The integer-object composition permits exact delegate casts/type tests using
existing runtime descriptors and visibility checks. Covariant/contravariant
declaration flags do not add conversions between delegate specializations.
Reference, wide, value and borrowed call signatures, private closures, virtual
capture, multicast and asynchronous invocation remain unsupported.

Advance the explicit CIL delegate profile to delegate-values-cil-v2. Its existing
cast restriction remains; delegate type tests use the integer-object composition.
The detached IR/native representations already carry exact specialized identities
and do not change. No native storage path or intrinsic grant is introduced.

## Evidence and review

Six mapped probes supply 1,764 repeated native results at O0/O2 with ASan/UBSan,
forced collection, frame exhaustion and cleanup checks. They cover captured
receiver cycles, static actions, generic returns, null/argument/target exception
order, exact type tests and invalid-cast messages. Detached IR rejects interchange
of identical-shaped specializations and inaccessible cast targets. Metadata
regressions cover invalid variance positions, constraints, nested declarations,
foreign runtime identities, closed signature substitution and work budgets.

The local solution builds. Focused delegate, generic constraint, interface,
mapping and owned-core suites pass. Review preserved the older explicit profile's
cast limit, added the missing delegate type-test validator case and charged the
closed declaration header to the shared metadata budget. No remaining actionable
issue was found. Local main remains an ancestor; no hosted work ran.

The reviewed local fixture has 204 inputs and the existing 65 grants. Its image
SHA-256 is `ff9ed3f613ecc31c01b30edb7e91d5ee58f3bde1519b434d8c61ef0960cada18`;
manifest SHA-256 is `8ddde5726e2fbf79cffb08af30878f32f0f03d93023da6951d76f60ca65fc59a`.
Production approvals remain empty. The real mapped kernel next stops at missing
System.Guid (reference TypeDef 0x02000074). This is declaration progress, not a
claim that its reference-valued callbacks or full library closure execute.
