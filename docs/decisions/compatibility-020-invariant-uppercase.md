# Pinned invariant string uppercase

2026-10-08. Owned follow-up to batch 017, using the established AC0189 casing
architecture and the already retained .NET/Unicode sources.

## Decision

Implement only parameterless String.ToUpperInvariant. Preserve the pinned
TextInfo identity path: empty returns Empty; unchanged ASCII returns the same
string; ASCII requiring change and every non-ASCII input allocate a fresh result.
Decode valid surrogate pairs, preserve lone surrogates and keep UTF-16 width.

Reuse the pinned Unicode 16.0.0 simple uppercase table already used for ordinal
comparison. The existing table excludes dotless i and long s; pal_casing.c's
invariant rule excludes only dotless i. A private scalar adapter restores long s
U+017F → U+0053 before table lookup. No new generated table, source-list entry,
compiler operation, intrinsic privilege, profile, static root or OS dependency.
The existing checked code-unit construction and managed frames own the temporary
vector and string, exactly as for ToLowerInvariant. Culture overloads remain
diagnostic. This is pinned simple casing, not full case folding or expansion.

## Validation plan

Compare the lookup against .NET on all 1,114,112 values including surrogate units;
compare changed scalars and neighbors exactly in native code, complete Unicode
string blocks by hashes, and identity, nulls, mixed pairs and allocation pressure.
Use O0/O2, forced GC, ASan/UBSan and unchanged frame limits. Advance the retained
owned decoder-shape probe only after exact native/.NET results agree. Preserve
actual candidate uncertainty: no upstream source execution is authorized here.

## Result and review

All 39 selected checks pass, with 18,564 new repeated O0/O2 native/.NET comparisons.
The lookup matches every code-point value on the .NET10.0.11/macOS oracle. Exact
scalar checks and full-range block hashes are retained separately; identity,
surrogates, faults, forced GC and prior lowercase/ordinal behavior pass. Retained
source hashes and generated table reproduction verify. No outstanding finding.
The owned decoder API sequence now passes; actual candidate execution remains
unapproved and unverified. See [batch018](../compatibility-candidates/batch-018-results.md).
