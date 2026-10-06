# ADR AC0061: Deferred concatenation and observable terminal decisions

Accepted 2026-10-07 for AC6.10.

The frozen control-opportunity pipeline concatenates projected players after sorting.
Implement the independent Concat prerequisite with the existing owned query helpers.
One managed ConcatIterator combines the private two-source and linked-chain layouts.
It retains first/second null validation, empty-array identity shortcuts, deferred
source acquisition, same-thread cursor reuse, clone state, cached Current, unsupported
Reset, and source disposal before acquiring the next cursor. A failing transition
retains the pinned state and disposal behavior. No application rules are involved.

Terminals do more than concatenate values. Two-source counts try non-enumerated
counts before fallback enumeration; linked counts traverse later heads backwards.
Any uses a cheap count when available and otherwise obtains the first value. The
two-collection ToArray path reads counts then copies first/second even at zero;
all-collection chains count backwards, then copy backwards with the pinned zero
shortcuts. Mixed two-source materialization buffers the uncounted source before
copying the collection. Mixed chains preserve segment-dependent Count, enumeration
and CopyTo decisions. Ordinary traced arrays and a linked segment list replace spans,
pooled arrays and inline-array scratch storage. The eight-item scratch threshold and
sixteen-based capacity buckets remain because callbacks can observe copy offsets
and destination lengths. Buffer/allocator reuse is not exposed as a new library API.

Select's internal traversal distinguishes materialization (cheap-count request)
from effectful Count (evaluate every selector without an extra source Count call).
An owned first-result interface lets Concat finish source first-result/disposal work
before projection. Zero-count materialization does not claim or dispose the source
cursor. Existing Where/Select traversal decisions remain covered by adjacent tests.
No generic virtual method, boxed collection factory or general span/pool support is
added. Actual List/Dictionary/HashSet enumerable sources remain diagnostic when their
boxed enumerator factories enter the reachable closure. An attempted List fast-path
was removed because merely including its layout pulls that unsupported factory into
complete interface dispatch; no placeholder enumerator was substituted.

Twenty-one probes use 122 distinct input pairs, repeated three times at O0/O2:
15,372 native/.NET comparisons on macOS ARM64 with forced collection, ASan/UBSan,
exact cleanup and unchanged frame/storage quotas. The expanded grid exercises every
authored get/move/current/dispose/count/copy mode. It also covers empty/null sources,
identity and cursor reuse, early break, reverse collection callbacks, scalar/reference/
nullable/inline values, mixed segments, projection and zero-count cursor preservation.
A negative boxed List factory test passes. Forty-four adjacent Select/First/core and
public-baseline tests pass (59 selected checks total). Initial translation failures
were adapted with explicit Int32 length calls and branch-local out assignments; an
oracle fixture accidentally requested an enormous array and was bounded to preserve
checked-overflow coverage without expensive allocations. Logs remain under
artifacts/alchemy-concat, including failures and the final expanded matrix.

Review checked linked traversal termination, fault/disposal precedence, checked count
arithmetic, segment copy boundaries, rooted reference-bearing copies, no cursor
consumption for empty materialization and exact cleanup after faults. Upstream source
digests are pinned in library/provenance.json. No grants or compiler/native/layout
profiles change. Refreshed core SHA-256:
3319c4f4721183c8db50c969a42d63c5dd06faf32351340869e75207c0f0d153;
manifest 1938d5733529d8eabc56f75ad1f8a1f27e3c68aeb3bc21e8941a30bdb441b88e.
Public baseline remains 31 native controls/16 gaps. Frozen engine still needs
IOrderedEnumerable/OrderBy at ReconcileControlOpportunity IL_0079; state retains its
257/256 discovered-identity bound. Complete engine emission and replay are unverified.
