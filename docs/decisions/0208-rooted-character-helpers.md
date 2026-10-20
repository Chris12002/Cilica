# ADR 0208: Rooted direct character-construction helpers

Status: accepted. Native v101. Recorded 2026-10-20.

After single-allocation strings, the two Char.ToString wrappers still reserve
general execution storage for tiny straight-line operations. Roslyn's Release
IL performs a borrowed UInt16 load followed by a static call; the static helper
constructs a string. Retained .NET native code folds the wrappers into allocation,
while Unity's generated wrappers use native locals and ordinary calls.

Add a separate structural proof for allocating character helpers. Do not extend
the no-safepoint typed-kernel whitelist. The factory must have one Int32 argument,
an unchecked UInt16 normalization, the validated StringFromCharacter operation
and immediate exact-string return. The optional forwarding helper must have one
readonly UInt16 location argument, load/normalize it, call that proven factory
directly and return its result. Neither shape can have locals, block inputs,
handlers, extra operations or alternate branches. There are no method-name/token
tests and no recursive or arbitrary allocating call chains.

Warm healthy calls enter this path under CILICA_TYPED_INTERNALS. Each helper
publishes a real Frame/RootScope, so GC root-chain length agrees with managed
depth. The forwarding helper validates the borrow before reading and roots its
owner throughout the allocating call. The factory has no reference inputs. Its
result reaches the caller without an intervening allocation; destruction of these
scopes neither allocates nor collects. No address into a popped frame escapes.

The factory requires depth below 64; forwarding requires depth below 63 so both
frames fit. Cold entry, invalid borrows, existing exceptions/faults and boundary
depth cases use the original framed implementation. A valid warm helper can
succeed without spare execution-arena space, as with existing leaf optimizations.
Allocation failure records the original factory method/IL origin and materializes
the managed exception when appropriate. No path replays after allocation begins.
Public ownership checks, null/bounds checks and intrinsic grants are unchanged.

Review covered proof closure, exact operands/types, module linkage, normal and
failure rooting, borrow cleanup, depth/fault order and result publication. The
44-check focused suite passes, including O0/O2 helper probes with forced GC,
internal-reference/borrow/allocation verification and ASan/UBSan. New native
checks cover live/stale/foreign borrows, wrong slots/widths, zero arena use, cold
entry, allocation failure/recovery and separate translation units. A final
three-check run also covers successful allocation at depth 64 and faults just
beyond it. Seven pre-existing test assertions were corrected to expect the
readonly Char receiver already introduced by earlier source changes.

All 523 DiffPlex observations match in release and forced-GC sanitized execution
on a requested 512 KiB stack (observed size checked within 512–576 KiB). Largest
individual frames are 432 bytes release and 320 bytes diagnostic. Paired timing
is v100 66.051 us/diff versus v101 61.511 us/diff, 6.87% less time; combined with
ADR 0207 the reduction from v99 is 16.68%. These are macOS ARM64 workload results.
See [the performance report](../performance/character-performance-20261020.md).
