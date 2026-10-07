# ADR AC0122: Parameterless fixed-invariant integer text

Recorded 2026-10-07 for AC7b.15.

After committed AC3.21 (4c62871), frozen engine discovery reaches parameterless
Int32.ToString in EffectiveSpellCompiler.Compile IL_03ab after 238,853 metadata
units. StateLookup remains eight methods/9,921 units. Evidence:
artifacts/alchemy-ordinal-set-{refresh,engine}; no engine emission/execution.

Add Int32/UInt32/Int64/UInt64 parameterless overrides using the existing managed
IntegerText entry points. Require each exact overload's InvariantIntegerFormatting
grant and parameterless override flags. The existing one-provider overload keeps
its direct InvariantCulture provenance requirement; it is not relaxed. Reuse the
existing decimal arithmetic and 0..299 cache. Integer CIL v47 records admission;
IR/native v65/layout v24, cache ownership and resource limits are unchanged.

The owned core already fixes default numeric text to invariant culture (AC0015).
This slice uses that same contract, not arbitrary ambient CurrentCulture behavior.
The differential host selects InvariantCulture and restores it with finally.
No locale-changing APIs are introduced. Format strings, broader primitive types
and boxed scalar ToString dispatch remain outside this slice.

Seven probes compare every decimal code unit for full-width extrema and cache
boundaries, heap-field formatting, mixed widths and identity shared with explicit
invariant formatting. Four grant-removal cases require the exact zero-parameter
overload despite its one-provider sibling remaining granted. All 30 selected
checks pass; all eight focused checks also pass after strengthening cache-boundary
identity coverage. The final seven probes provide 9,630 repeated O0/O2 native/.NET
comparisons with forced GC/ASan/UBSan. Existing provider/cache/core checks pass;
2,048-byte frame and 30-second subprocess caps remain unchanged. The public
baseline remains 43 controls/four gaps.

Review checks signed-minimum handling through the unchanged formatter, full-width
receivers, cache identity/ownership, source overload matching and provider checks.
Main 48ecf14 remains an ancestor. Frozen application inputs/rules are unchanged.
Full engine compilation/execution and AC9 saved-game replay remain unfinished.

Evidence: artifacts/alchemy-default-integer-text/tests-{a,b}.log and hashes.json.
Core: ab19cfec0d104da213a013f98cfdcd5fcfee0a66fa3ca727d4c00d83f32f7847.
Manifest: d8cf1788bd0cbfe8f9ca3007813067a258c7eb52bc28dfb3d7624850076747ee.
Fixture: bbaa8f8885be1774ab3d8f8ffcfc54c095283e9d66a5f631bb34b0c11cc149c9.
