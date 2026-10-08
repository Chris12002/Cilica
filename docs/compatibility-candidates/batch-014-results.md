# Batch014: explicit-character string trimming

Online-first screening selected Albireo.Base32 Decode at pinned
1eabd2fcba515d0bd63881fd2a2879555c43648b (MIT, netstandard2.0) because its TrimEnd
requirement is a compact general library gap within the current managed profile.
No candidate source was cloned, staged, compiled or executed. The selected next gap
and managed design were reported before implementation; Unity lane coordination
confirmed no clean timing gate. Peer Decimal commit30fe50d remains intact.

## Implemented and verified

String.Trim/TrimStart/TrimEnd(Char) and (Char[]) plus parameterless TrimStart/TrimEnd
now compose existing UTF-16 reads, UnicodeWhitespace and Substring. They preserve
exact code-unit membership, directional boundaries, null/empty-set whitespace fallback,
unchanged identity and canonical empty results. Sets are neither mutated nor retained;
subsequent calls see set mutations. No new compiler/runtime operation, profile, grant,
quota, culture behavior or temporary membership buffer.

**19/19 focused and adjacent checks pass in69.708s.** New probes and former gaps
supply15,042 repeated O0/O2 native/.NET comparisons (14,454 new-root comparisons plus
588 for two formerly rejected reductions). Observations include full payloads,
identity, later allocations, duplicate/empty/null sets, NUL/isolated surrogates/FFFF,
receiver/argument fault order and input mutation. Existing exhaustive UTF-16 whitespace,
character construction and grant/packet boundaries also pass. Native checks use forced
GC, ASan/UBSan, exact root/borrow cleanup, frame-budget exhaustion/unwinding and strict
2KiB native frames. All20 generated-source/executable hash pairs are retained.

The original pre-implementation reduction fails CIL3010 for Trim(Char). Two harness
repairs remain in evidence: a span negative probe was unavailable in the fixture's
reference target, and source-level params required a missing core ParamArrayAttribute.
The implementation now uses the exact Char[] signature because callers lower params.
Neither repair changes candidate semantics or expands the supported runtime profile.

A final1/1 check tightened the existing ToCharArray assertion to exact CIL3010 and
member name; it overlaps the19 checks. Peer SequenceEqual edits arrived afterward;
full-suite and boundary assembly hashes remain separately recorded.

See [design/review](../decisions/compatibility-016-character-set-trimming.md),
[hashes and attempts](batch-014-evidence.json), [passing log](batch-014/repaired-tests.log)
and [resumable manifest](batch-014.json). Generated native files were disposable;
hash receipts are retained before harness cleanup.

## Candidate outcomes and next priorities

- Albireo.Base32: mapped-blocked. The owned ToCharArray reduction still diagnoses;
  Char-predicate delegate and readonly input-byref compositions need confirmation.
  Encode needs Decimal.Ceiling beyond the peer's bounded Decimal route. No external
  semantic result or full compatibility claim.
- Wiry.Base32: skip encoder/decoder unsafe fixed-pointer kernels outside scope.
  Its selected source/project/licence and RFC tests were inspected; validation-only
  roots would need separate mapping.
- LruCacheNet: preflight skip for Monitor locking/events on useful paths; do not
  remove synchronization to make it fit. Full test mapping stopped at this boundary.
- SimpleBase current pin: preflight skip, only net8/net9/net10 assets. No earlier
  release was selected; source/test mapping stopped after framework rejection.

No candidate copies needed cleanup. Previous verified CircularBuffer/CRC32 roots
remain skipped; no performance optimization was adopted. Full suite, full Alchemy
execution/replay and all new upstream execution remain untested. Next useful owned
slice is String.ToCharArray, followed by confirming the decoder's Char-predicate and
byref closure before preparing any external execution recipe.
