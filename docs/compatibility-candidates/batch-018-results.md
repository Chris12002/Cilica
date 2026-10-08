# Batch 018: pinned invariant uppercase

Owned slice on `8f76ed2`, 2026-10-08. No upstream candidate source was downloaded,
built or executed. Existing review-lane changes are preserved.

## Outcome

String.ToUpperInvariant now follows the established invariant lowercase path:
empty returns Empty, unchanged ASCII returns the source, and all other inputs
allocate a same-length result. Valid pairs are decoded/re-encoded; lone surrogates
remain unchanged. The retained Unicode 16.0.0 ordinal uppercase table supplies
simple mappings, with U+017F restored to S for invariant casing. U+0131 remains
unchanged. No table, intrinsic, profile, root, quota or OS dependency was added.
Culture overloads remain diagnostic; no case expansion or normalization occurs.

**39/39 checks pass**: 36 focused in 171.820s and three adjacent in 14.851s. The lookup
matches .NET10.0.11 on all 1,114,112 values, including surrogate code units. Native
O0/O2 checks add **18,564 repeated native/.NET comparisons**: 2,229 exact changed
scalar/neighbor points (13,374), 544 complete-range string blocks (3,264), identity,
mixed-surrogate and null probes (1,890), and six owned decoder scenarios (36).
The whole-string sweep compares hashes; scalar/table checks compare exact values.
Forced GC, ASan/UBSan, strict 2 KiB native frames, exact roots/borrows and exact/one-short
managed frame budgets remain enabled. Lowercase identity and ordinal lookup
regressions pass. All retained upstream source hashes, Unicode data digest and
regenerated ordinal table verify. All 52 native source/binary hash pairs and two
adjacent pairs are retained alongside exact managed assembly/source hashes.

The owned readonly input → TrimEnd(Char[]) → ToUpperInvariant → ToCharArray →
Char-predicate Any/IndexOf sequence now executes. **This is not actual Albireo.Base32
validation.** The complete candidate closure, checked bit arithmetic, lambda cache,
output bytes and faults require a separately approved selected Decode run. Encode's
Decimal.Ceiling requirement remains outside this slice.

## Review and next action

Review checked pinned TextInfo identity, the distinct ordinal/invariant long-s
rule, dotless-i exception, UTF-16 width, pair boundaries, retained source lifetime,
allocation cleanup and unchanged lowercase body. No outstanding finding. The
Unicode 16.0.0 contract is deterministic; different oracle globalization versions
may differ. No full suite, Alchemy replay or non-macOS execution is claimed.

The next concrete step is an exact, offline, credential-free selected Decode
validation recipe for approval. Prior candidate exclusions remain recorded in
[batch018](batch-018.json). No disposable candidate copy was created or deleted.

- [Decision](../decisions/compatibility-020-invariant-uppercase.md)
- [Exact hashes and checks](batch-018-evidence.json)
- [Focused checks](batch-018/initial-tests.log)
- [Adjacent checks](batch-018/adjacent-tests.log)
- [Retained-source verification](batch-018/data-verification.txt)
