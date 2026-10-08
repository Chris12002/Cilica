# Batch 017: exact generic Char-array enumeration

Owned slice on `30cbcf8`, 2026-10-08. The provider-parsing change is preserved.
No upstream source was downloaded, built or executed.

## Outcome

Char vectors now use the existing ArrayInterface<Char>.Enumerate helper and managed
SZGenericArrayEnumerator<Char>. Discovery and detached validators admit this one
additional narrow recipe, require exact Char receivers, and select IEnumerator<Char>
by its closed primitive identity. Any's existing array loop and fallback discovery
now compose; no LINQ body, intrinsic grant, native operation or GC path changed.
CIL105/interface-plan20 record admission; object IR63/native79 stay unchanged.

**90/90 distinct checks pass**: 54 focused in 89.866s and 36 adjacent in 98.325s.
The new probes add **9,588 repeated native/.NET comparisons** at O0/O2, including
all 65,536 UTF-16 units, live generic Current, exact Char boxes from nongeneric
Current, fresh nonempty cursors, mutable closed-type empty caches, state/fault
messages, Any/All/Count and null/predicate order. Four additional native checks
prove sole-array retention until cursor release, empty-cache nonretention and
cross-instance cache isolation. Forced GC, ASan/UBSan, strict 2 KiB native frames,
exact roots/borrows and exact/one-short managed frame budgets remain enabled.

Denied exact enumeration grants and malformed logical/scalar receiver/result
packets reject. Char indexed access/set/copy, Byte/Boolean generic enumeration,
signed contracts and other existing narrow gates remain diagnostic. Existing
array enumeration, narrow nongeneric cursors and LINQ predicate regressions pass.

The first run passed 42/53. Its 11 failures shared a proposed full decoder closure
which reached missing String.ToUpperInvariant. This reduced source remains an
exact CIL3010 regression; positive cursor cases now translate independently.
No production change was needed after that initial run. Exact managed assemblies,
owned sources, initial failures and native source/binary hashes are retained in
[the evidence manifest](batch-017-evidence.json).

## Next candidate requirement

Albireo.Base32 stays mapped-blocked at ToUpperInvariant in the owned decoder
composition; complete decode/encode compatibility is unverified. The next small
slice is invariant uppercase using the existing pinned casing model. Prior unsafe,
locking and framework exclusions are retained in [the manifest](batch-017.json).
No candidate copies were created, so no candidate cleanup was required. No full
suite or Alchemy replay was run.

Review checked vector-specific admission, exact primitive result identity,
Char receiver preservation at both stages, unchanged managed cursor/boxing
semantics and lifetime after end/disposal. No outstanding production finding.
See [the decision](../decisions/compatibility-019-character-array-enumeration.md),
[focused checks](batch-017/refined-tests.log), [adjacent checks](batch-017/adjacent-tests.log)
and [initial failures](batch-017/initial-tests.log).
