# Compatibility 021: bounded polymorphic dictionary text

Implemented against clean compiler baseline `232c8d6`, independently of the review
lane's handler-registry regression `1e0d6f4`. The frozen netstandard2.1 engine stopped
in SortedDictionary.Add because DispelOperation stores non-sealed List<string>
values. Duplicate diagnostics are reachable semantics even when a particular
successful run never creates a duplicate. Merely removing the sealed check would
bind an override or default name to the static type incorrectly.

## Decision

Keep the established sealed fast path. Non-sealed ordinary classes use an owned
ReferenceKeyText<T>.Render bridge and existing receiver-bounded virtual dispatch.
Roslyn's class-constrained reference `box !0` is an identity operation: discovery
can recover its exact closed reference bound, while typed translation independently
checks that operand. Canonical slots preserve overrides, inherited overrides,
new-slot hiding and explicit base calls. The completed receiver catalog is checked
for IFormattable subclasses; provider formatting stays diagnostic.

The selected Object.ToString now has a managed body calling an exactly granted
private ObjectText.Default declaration. The new ObjectDefaultText IR is limited to
that default body. Its generated descriptor/name table reads the actual runtime
class and uses existing per-instance UTF-16 string literals, GC roots and charging.
No runtime headers, collector, ownership predicates or limits change. Name length
and aggregate literal limits are checked before copying code units, including in
validation of detached IR. A table walk bounds native stack usage; an initial branch
chain failed the existing 2 KiB warning and was discarded.

Both direct and virtual calls independently require an ordinary typed class
receiver in IR validation. Object aliases, vectors, boxes and reflected Type names
remain diagnostic. Nulls retain managed faults. The enum-formatting specialization
keeps its existing separate grant, but now authenticates Object.ToString's exact
managed declaration instead of the former bodyless InternalCall shape. The earlier
sealed name helper still requires its own exact grant and sealed identity proof.
CIL107, object IR64 and native80 record the extended contract; interface20 and all
production resource limits remain unchanged.

## Actual converted-code reference use

The user explicitly requested use of the converted Alchemy reference. Inspected
source locations and complete hashes are retained in
[batch-019-reference.json](../compatibility-candidates/batch-019-reference.json).
In the Unity 6000.3.2f1 capture:

- mscorlib__6.cpp:5115–5123 obtains the runtime type for default Object.ToString.
  This informed the dynamic-name path and derived/default/explicit-base regressions.
- Generics__25.cpp:14–41 uses the actual receiver's virtual slot. This informed
  preserving Cilica's canonical dispatch and testing overrides versus new slots.
- Alchemy.Engine__6.cpp:15848–15902 obtains virtual StringBuilder text before hashing
  in SemanticFingerprint. This reinforced retaining returned text through existing
  managed roots and subsequent allocations.
- The installed il2cpp-codegen.h:717–726 performs raw class-slot lookup. Cilica
  deliberately retains its validated IR maps, descriptor membership and instance
  checks; Unity's lookup is not copied into the implementation.

The exact frozen SortedDictionary path is absent from the narrow capture. All five
Unity engine-graph DLL hashes differ from the frozen audit's DLLs. Source presence
is not execution coverage; this is neither same-binary comparison nor full engine
validation. No Unity implementation or runtime was adopted, and milestone 7 is
not unblocked. The semantic default-name contract was also checked against pinned
[.NET Object.cs](https://github.com/dotnet/runtime/blob/79d0c463f1b55624c874a11585f7e47731e8d675/src/libraries/System.Private.CoreLib/src/System/Object.cs#L35-L38).

## Validation, review and frozen result

120 distinct selected checks pass: 21 new focused/boundary checks, 78 adjacent checks,
and 21 existing enum-text checks. Overlapping reruns are not counted again. The final
focused run took 37.939s; adjacent run 199.672s; overlapping focused/enum/core run
80.038s. The new semantic probes supply 2,598 repeated O0/O2 native/.NET comparisons.
They cover exact default class/generic/hidden names, inherited overrides, new-slot
hiding, explicit base calls, null/throwing/allocating/reentrant callbacks, duplicate
and missing diagnostics, and sorted List<string> values. Managed diagnostics are
compared through UTF-16 hashes; the native boundary test also compares the default
name's exact UTF-16 against a .NET-derived oracle.

Two additional native checks cover exact/one-short charged string bytes, object
quota failure without publication, recovery, cached identity, foreign-instance
rejection, malformed-root allocation rejection and collection with only the cached
name retained. ASan/UBSan, forced collection, exact root/borrow cleanup, exact/
one-short execution frames and the 2 KiB native frame limit remain enabled.
Grant denial, private caller rejection, both-stage malformed packets, nonvirtual
Object/array bypass attempts and literal-budget rejection pass. Review identified
the direct-call bypass before integration; the shared IR call validator now covers
both call forms. Failed recipes and the enum declaration integration failure remain
in the receipts. The final build has zero warnings/errors; an earlier clean build
reported only the pre-existing CandidateHashAlgorithmOwner.State fixture warning.

All six frozen DLL hashes remain unchanged. With the existing diagnostic-only audit
allowances, engine discovery advances beyond SortedDictionary.Add to scalar boxed
formatting in BoardObjectState.WithHitPoints, token 0x060000cd IL0028, requested by
ObjectDamage.CalculateFixed token 0x06000da9 IL0088. Work: 3,390,014 units/18,744ms.
StateLookup validates 15 methods/11,742 units/445ms. Both emission fields are null;
no engine C++ compilation or execution occurred. Core SHA256 is
19d2c0c21699f079b8db0d393e7be34c6f75df6ad5e46589b53b9963ced15b78; manifest SHA256 is
cea95f5a1024c2a2da897af1f983bbd69d274a29485dd462072b1e7526971d27.

Receipts: [batch019](../compatibility-candidates/batch-019-results.md). Full replay,
non-macOS validation and the separately unapproved Albireo candidate run remain open.
