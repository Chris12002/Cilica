# ADR AC0170: Generic HashSet copies

Recorded 2026-10-08 for AC3.30.

The frozen EffectTags.Combine path reaches HashSet<T>.CopyTo through List.AddRange.
Implement all three generic overloads from pinned HashSet.cs. Short overloads read
Count and call the three-argument overload. That overload validates null array,
negative arrayIndex, negative count, then destination capacity. Negative faults
retain actual Int32 values and pinned parameter-specific messages through existing
argument-exception and invariant text operations. No new intrinsic is required.

Only after validation does the public body access SetStorage. Its ordinary managed
copy helper captures the entry vector, scans active physical slots and decrements
the requested count after each store. Counts larger than the set size are allowed
when destination capacity covers the request. Removed slots are skipped. Copying
performs no hashing/equality calls, allocation, cursor creation or version changes.
Covariant stores preserve earlier writes before ArrayTypeMismatchException.
The three-argument overload checks arguments before touching a null receiver;
short overloads read Count first. This ordering is preserved for direct raw calls.

Eleven C# probes, 72 index/count combinations and four authored direct-call IL
probes provide 4,842 repeated native/.NET comparisons at O0/O2 with forced GC,
ASan/UBSan, frame-budget and cleanup checks. They exercise all overloads, active
slot order, retained reference-bearing values, null elements, callback silence,
unchanged live cursors, exact faults, partial stores, empty/cleared sets and real
List.AddRange/List construction patterns. DynamicMethod oracles verify raw calls.
All 35 distinct selected checks pass, including existing set storage/cursor checks
and the 46-control public baseline with one retained gap.

Review checked validation-before-subtraction, count versus set-size semantics,
rooted captured storage, negative message values, raw null call timing and absence
of cursor/version side effects. Profiles, grants and all production/native limits
remain unchanged. Evidence: artifacts/alchemy-hashset-copy{,-refresh,-engine}.

Frozen engine discovery advances to aggregate diagnostic parameter exhaustion:
8,193/8,192 parameters, 4,504/6,144 locals, 1,159,312 charged metadata units in
4,325 ms. StateLookup remains 15 methods/11,529 units (463 ms). No full engine,
unchanged fingerprint or saved-game replay execution is claimed. A separately
recorded diagnostic-capacity slice will continue discovery.
