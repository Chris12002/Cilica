# Native-size array length confined to truth branches

DiffPlex's second approved trial reached `ldlen` followed by a direct branch in
CalculateEditLength. The existing array translator required adjacent `conv.i4`;
the independent EmptyPair reduction reproduced CIL2001.

The integer-object profile now accepts `ldlen` immediately followed by `brtrue`,
`brfalse`, or their short encodings in the same partitioned basic block. Existing
exception/branch leaders participate in that partition, so an alternate entry at
the consumer cannot bypass the confinement proof. No intermediate nop, alias,
local, duplicate, conversion, numeric comparison or jump is admitted by this path.
The original `ldlen; conv.i4` lowering is unchanged. Earlier execution profiles
still reject the new branch shape.

Lowering performs the existing checked ArrayLength at the original ldlen offset,
then compares it against zero and exposes only a Boolean Int32 to the adjacent
branch. Every admitted vector length is nonnegative and at most Int32.MaxValue:
the allocator and tracing checks both enforce this bound. Zero/nonzero is therefore
identical for the CLI unsigned native-size length on either 32- or 64-bit targets.
This does not truncate or normalize an arbitrary native integer. Other native-size
flows remain diagnosed before lowering. Actual 32-bit ABI execution is not claimed.

The read keeps its existing null fault, exceptional edge and reference-root
handling. The derived comparison cannot allocate or fault; branch polarity and
short-circuit order are unchanged. Generated C++ uses existing unsigned length
storage and comparison, with no new arithmetic or pointer operation. No IR packet,
layout, grant, native profile or quota changes. Integer CIL admission moves from
v89 to v90.

Four authored C# probes cover two-array empty checks, short circuit and null order,
finally cleanup, and byte/char/Boolean/reference/value vectors. Raw IL covers both
truth polarities and long/short branches against normal .NET DynamicMethod IL.
Ten rejection cases cover return/pop/dup, arithmetic/comparison, wide conversion,
local storage, nop separation, a jump join and an alternate branch entry. The raw
positive cases also check rejection by the older exception profile. Tests use
O0/O2, forced GC, ASan/UBSan and frame/root accounting. The eight semantic roots
supply 2,352 repeated native/.NET comparisons; all 20 new checks pass.

All 81 adjacent array/profile, contract/core and handcrafted GC/exception/boxing/
generic/dispatch checks also pass (101 selected checks total). The explicit
candidate driver now logs translation, completed oracle scenarios and completed
native observation batches without changing the candidate or its oracle logic.

See the linked compatibility trial result for the
actual unchanged-candidate outcome. Preserve both earlier DiffPlex failures and
keep semantic execution distinct from successful source/IR compilation.
