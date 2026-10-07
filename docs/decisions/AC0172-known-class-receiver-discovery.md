# ADR AC0172: Known class receiver discovery

Recorded 2026-10-08 for AC8.53.

The frozen type-key hash calls Object.GetHashCode through ldind.ref from Type&.
The sealed-only discovery proof lost that static type and discovered an unrelated
OrdinalCaseSensitiveComparer override. A reduced Type-key/ordinal-comparer fixture
reproduces the original unsupported-body diagnostic before the fix.

Extend the existing bounded zero-argument callvirt proof to ordinary class types,
including abstract and closed generic bases. Resolve the original public virtual
slot against that static receiver and retain normal subclass dispatch. Follow one
ldind.ref only when its producer proves a managed location with a reference element.
This uses the existing canonical slot resolver; it does not devirtualize nonsealed
receivers, infer caller allocations, cross control-flow/handler entries, bypass
visibility, or change the 256-instruction/depth bound. Typed SSA validation still
checks the actual receiver and borrow. Unknown Object elements, invalid pointer
origins, arrays and interfaces do not establish a narrower class proof.

Six new native fixtures cover parameters, locals, fields, ref parameters, nulls,
abstract/generic subclasses, hidden new slots and real Type dictionary keys with
an unrelated comparer present. A seventh object-identity-hash fixture makes the
existing grant-denial check precise: Type hashing no longer requires the unrelated
object-identity grant, while actual ordinary-object hashing still requires it.
These supply 2,058 repeated O0/O2 native/.NET comparisons with forced GC and
ASan/UBSan. New negative cases retain Object-ref and invalid-indirection diagnostics.
All 47 distinct selected checks pass after correcting the grant fixture's static
receiver; these include existing sealed/stack receivers, canonical slot resolution,
virtual values, runtime identity and the 46-control/one-gap public baseline.

Review checked backward stack effects (including dup and ldind.ref), invariant
borrow elements, original declaration access, canonical new-slot selection,
subclass closure, null checks and unchanged native ownership/rooting. Integer CIL
v74 records the discovery extension. Object IR v47, native v73, grants and all
production/native/diagnostic limits are unchanged.

Evidence: artifacts/alchemy-known-receivers and alchemy-known-receivers-engine.
The diagnostic copy reuses the exact AC3.30 archived core/manifest because library
code is unchanged. Frozen discovery now reaches a real string-argument hash call:
UnitId.GetHashCode invokes StringComparer.Ordinal.GetHashCode(string). It charges
1,182,823 metadata units in 4,321 ms. StateLookup remains 15 methods/11,529 units
(461 ms). Exact UnitId IL is archived. Full engine/fingerprint/replay execution
remains unfinished; no engine emission or execution is claimed.
