# ADR AC0060: Typed ordinal comparer and generic comparer contracts

Accepted 2026-10-07 for AC7b.6.

Frozen ReconcileControlOpportunity needs ordinal string ThenBy. Build its comparer
foundation independently of the ordering iterator. The owned core declares exact
IComparer<T>/IEqualityComparer<T> contracts and the StringComparer base. Its Ordinal
factory returns a fieldless sealed class implementing typed string comparison and
equality through the existing CompareOrdinal/string-equality algorithms. The typed
methods use AC8.13 abstract class dispatch; exact generic interface calls use the
existing scalar/inline argument lowering. No variance-only dispatch is admitted.

A separate managed helper stores the singleton in a static readonly field. This
keeps interface method facts initializer-free while using existing per-instance
static initialization, failure handling and tracing. The observable singleton
identity stays stable across calls and differs across runtime instances. Randomized
string hashing, comparer-object equality/hash, nongeneric comparison/equality and
other factories are deliberately unavailable. Unsupported reachable members fail
before execution; this does not implement default comparers or custom comparer
construction for existing collections.

StringComparer and both generic interface originals are pinned to runtime v10.0.11
in library/provenance.json. Their digests were checked. The adaptation omits span,
ref-struct, culture, serialization and ignore-case paths outside this bounded scope.
Three exact type mappings are added to full-core tests and the private harness;
minimal-core mapping lists are unchanged. No intrinsic grants, compiler profile,
native ABI, layout or quota change is needed.

Seven authored probes pass 8,376 repeated O0/O2 comparisons against .NET on macOS
ARM64: 400 string pairs each for direct compare, interface compare and typed equality;
49 integer pairs each for identity, scalar/inline application comparers and null
interface receivers, repeated three times per optimization. Strings include null,
empty, embedded NULs, unpaired surrogates, surrogate pairs, different lengths and
separate equal-content allocations. ASan/UBSan, forced collection and existing frame
and cleanup checks remain enabled. Five unsupported-shape tests pass. Two native
checks prove exactly one comparer root in each of two instances, pointer isolation
and collection after roots are cleared. Their initial absolute heap-count assertion
omitted the runtime emergency exception; the final test explicitly checks that
baseline object and the additional comparer instead of relaxing cleanup. Twenty-five
adjacent core, ordinal-text and public-baseline tests pass (38 selected tests total).

Review checked null ordering, exact numeric compare results, no reference-identity
substitution for string content, rooted singleton lifetime, ownership isolation and
reachability of explicit unsupported members. Evidence: artifacts/alchemy-ordinal-comparer.
The refreshed core SHA-256 is 9f801ce31b62db9266cfb0e98abd86046f3f9a26f534818700d786854b3fbb01;
manifest dee4548804e4373c3a770eebe0ba22d25891ed0e5b1fe7b201a747e3a1238375.
Public baseline remains 31 native controls and 16 gaps. Frozen engine discovery still
stops at IOrderedEnumerable (OrderBy in ReconcileControlOpportunity IL_0079); state
still reaches 257/256 discovered identities. No full engine/replay claim is made.
