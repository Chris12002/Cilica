# ADR AC0109: Scoped scalar/reference ref locals

Recorded 2026-10-07 for AC5b.10.

## Decision

After AC3.17 (081760e), the unchanged engine diagnostic charges 167,571 metadata
units and stops at BoardTopology.Blocking's reference-byref local. The frozen body
holds a reference-array element address across a branch for cached ??= assignment.
StateLookup remains eight methods/9,919 units; no engine emission/execution.
Evidence: artifacts/alchemy-list-exists-{refresh,engine}, including blocking-il.txt.

Separate local lifetime support from reference-array slot addressing. This slice
admits Int32, Int64 and reference ref locals in the integer profile. Each read
requires one assignment site that dominates it, with same-block statement order
checked explicitly. A bounded backwards predecessor proof cuts at the defining
block and rejects any surviving path to entry. A 65,536-step per-method proof
limit bounds malformed-input work. Both logical and scalar IR validate the proof.
An assignment site may execute repeatedly in a loop; multiple distinct assignment
sites remain unsupported. Unread local declarations need no assignment.

No location may return, enter a heap field or cross a stack join. Methods owning
these locals cannot have exception regions; callees may throw and caller handlers
may recover after the owning frame unwinds. Inline-value and narrow-width ref
locals remain excluded. Existing invariant element and readonly rules still apply;
addresses of ref locals themselves cannot form nested locations. Frame carriers
retain authenticated active borrow tokens. The emitter publishes a heap owner root
on each store to a ref local, using the existing budgeted location/root slots.

Integer CIL v35, managed-location IR v2, scalar-location IR v3, object IR v35 and
native v62 record the extension. No native carrier layout or library change,
production budget increase, or reference-array borrow support is claimed here.

## Validation and review

Seven probes provide 2,058 repeated O0/O2 native/.NET comparisons for frame aliases,
alias copies, scalar/wide heap fields, reference replacement, borrowed parameters,
branches, loops, allocation pressure and cleanup through a throwing callee. Root
plans explicitly include each local's owner. Three source boundaries reject
reassignment, protected regions and inline-value locals. Five authored IR mutations
at both stages reject missing/duplicate stores, same-block early reads, bypass
paths and readonly weakening. Positive controls retain the original reachable
callee closure so these tests exercise lifetime proof rather than reachability.

The 76-check initial regression run passed 71 checks; five authored mutation tests
had omitted their original reachable callees and were corrected. The final
27-check run passes, including those five, value/location lowering, native value
and vector borrows, and all seven baseline checks. All 98 distinct selected checks
pass. The baseline remains 43 native controls/four gaps. Forced GC, ASan/UBSan,
2,048-byte frames and 30-second native subprocess caps remain enabled.

Review checked dominance on branches/loops, definite assignment, proof work limits,
readonly invariance, owner publication, existing frame lifetime authentication and
unwind cleanup. No frozen inputs or application rules changed. Full engine and
AC9 saved-game replay remain unfinished. The next slice supplies checked reference
array element borrows. Local main 48ecf14 remains an ancestor.

Evidence: artifacts/alchemy-scoped-ref-locals/tests-{a,b,c}.log and hashes.json.
Core: d77f711997696db4e72d687ba835aed4eccd83fbd606ddfbae60911e3447b569.
Manifest: fb38ddcb594fbed4fbd14400eafc160b41367b404a955a8f20e4583a71bc7bdf.
Fixture: a1469af07041c881a695e3241d0df172ad56a2fd3147915f426a849f6d42209d.
