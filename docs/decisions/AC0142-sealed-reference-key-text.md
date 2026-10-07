# ADR AC0142: Sealed reference-key error text

Recorded 2026-10-07 for AC3.26.

## Evidence and decision

After AC8.37, the frozen engine stops on Dictionary.Add reference-key rendering
for sealed EffectiveEffectNode in EffectSchedule.Compile IL_00d7. AC3.25 provides
the independent equality/hash prerequisite; key rendering is still required.

At the exactly granted DictionaryKeyText<T> boundary, require a sealed ordinary
class and walk its base/interface closure to reject IFormattable. Resolve the
exact Object.ToString slot. An ordinary override, including inherited overrides,
becomes its existing direct managed call. A default Object target becomes the
ordinary DefaultReferenceKeyText<T>.Render helper. Its private Name declaration
has a separate exact ReferenceKeyTypeName grant and may only be called by its
own closed helper. Recheck sealing, formatting contracts and the default slot
before replacing it with the bounded metadata runtime name as a normal string
literal. Sealing proves static and dynamic receiver identities are equal.

The selected dictionary checks null before rendering; the private override path
therefore needs no invented null receiver behavior. Existing concatenation,
callback fault propagation, rooting and reentrant dictionary mutation remain
unchanged. Non-sealed/polymorphic keys and IFormattable require later dispatch;
this is not a general Object.ToString or reflection implementation. There is no
new native operation or object representation. Integer CIL v60, object IR v41,
native v67, layout v25 and all production budgets remain as documented.

## Validation and review

Six probes cover default, closed generic and hidden nested names; inherited
virtual overrides; null and throwing text; allocation in callbacks; callbacks
mutating the same dictionary; duplicate/missing messages; and null-key order.
Review checked sealed receiver proof, exact slot identity, inherited formatting
contracts, direct-call argument shape, lexical access, literal budgets and the
private helper/grant boundary. All 45 selected checks pass: new key text, reference key operations, existing
value-key messages, core build/mapping and the public compatibility baseline.
The six new probes supply 1,764 repeated O0/O2 native/.NET comparisons with
ASan/UBSan, forced GC, exact cleanup and unchanged 2,048-byte native frames.
Exact grants, private helper callers and broader formatting boundaries are tested.
Public baseline stays at 46 controls/one gap. All six frozen input hashes match.
Evidence: artifacts/alchemy-reference-key-text/{tests,regressions}.log.
Full engine/replay remains open.
