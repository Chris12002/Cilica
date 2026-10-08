# Bounded vector section Array.Clear

CircularBuffer's Clear uses the public Array.Clear(Array,Int32,Int32) overload.
Batch009 diagnosed that missing API independently of the candidate. This slice
admits exact selected-core declarations with separate public-call and managed-helper
grants. A same-block producer scan infers the closed vector parameter; ordinary
translation independently checks the actual stack value is assignable to it.
The scan has a 128-instruction budget, charged metadata resolution, cached block
entries and rejects joins across argument producers. Typed local/argument/field,
method-result and allocation producers are admitted; literal null uses an Int32
vector helper because null faults before element access. General Array aliases,
multidimensional/nonzero-bound arrays and joined argument stacks stay diagnostic.

ArraySections<T>.ClearSection is an ordinary public managed helper. It is public
so rewritten application calls obey existing type/method access rules; external
direct calls are safe ordinary managed calls too. No access-check exception or
privileged store is introduced. Exact grants authorize selection, not a bypass
of the helper's full null/range validation. The first internal-helper attempt
correctly failed ordinary access checks; the retained initial log documents it.

The helper checks null, then negative index/length and the complete section before
any write. Subtraction avoids signed overflow. Its loop stores default(T) through
existing typed vector operations, preserving array identity, reference covariance,
inline-value copying and release of references. Allocation/argument evaluation
happens in the original IL order; array replacement in a later argument cannot
redirect the already captured array operand. There is no extra helper allocation.
Pinned System.Array range behavior is recorded in existing ArrayInterface.cs
provenance and its original licence remains retained.

Validated types are Int32, UInt32, Int64, Int32 enum, reference/covariant reference
and supported reference-bearing inline-value arrays, including closed generic
calls, field producers and empty arrays. Byte/Char/Boolean generic default(T)
locations reach the existing narrow-memory restriction; three negative controls
retain that boundary. This slice does not expand that runtime capability.
CIL94 advances to95; IR59/native79, descriptor layout, collector, resource quotas
and instance ownership are unchanged.

Eight independently written semantic probes supply 2,352 repeated O0/O2 .NET
comparisons over zero/full/partial/invalid sections, integer extremes, null/fault
order, exact-message hashes, covariance and argument replacement. Four explicit
native lifetime checks collect after middle/full/repeated clears of rooted reference
and inline-value arrays, requiring exact survivor counts. Grants, safe direct
managed helper access and unsupported inputs are checked. The original CircularBuffer
Clear reduction adds294 repeated comparisons; the candidate itself is not run.
All native checks use ASan/UBSan, forced collection and existing frame limits.

Review confirms no raw memory clearing, new pointer arithmetic or native operation;
stores use existing typed barriers/root handling. Ordinary type/access checks and
metadata budget checks remain active. A neighboring unsigned-copy rejection also
failed on a frozen d37c2ad export without Clear. Replace that stale expectation
with high-bit UInt32 copy/fault controls (294 comparisons), preserving its exact
grant/external-intrinsic denials. See batch010 evidence for final check totals,
original failures and baseline reproduction. Public Copy and ArraySegment still
block CircularBuffer; full Alchemy engine/replay remains unverified.
