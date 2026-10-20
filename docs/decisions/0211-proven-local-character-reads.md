# ADR 0211: Direct values for proven local character reads

Status: accepted. Native v104. Recorded 2026-10-20.

After v103, DiffPlex's character loop still constructs a general managed location,
copies an argument packet and resolves the location twice to read a local UInt16.
The Release IL at CharacterChunker token 06000021, IL_001a/IL_001c, takes the
local's address and immediately calls Char.ToString. Retained Unity output passes
`&V_2`; its character wrapper reads UInt16 and calls the scalar factory. Roslyn's
EmitLocalAddress similarly emits the local address. The exact Roslyn compiler
source revision is not matched; the actual DLL/PDB and Release IL remain pinned.

Recognize an adjacent AddressLocal/direct-call pair after full IR validation.
The local must be Int32 storage viewed as UInt16, and the target must satisfy
ADR 0208's structural readonly-character-forwarder proof. The address must have
exactly that one use in its block: reject subsequent reads/writes, aliases, stores,
other calls, returns and outgoing edges. Unknown operations fail closed. Parameter,
heap and array addresses, intervening statements and virtual calls retain the
ordinary path. Scan at most 128 statements and 16 candidates per block; no method
names, tokens, input hashes or benchmark-specific matching participate.

When typed internals are enabled, borrow diagnostics are off, the context is
healthy, marking is inactive and managed depth is below 63, skip local carrier
construction and pass the local's bits to a private helper. Both the address step
and helper use the same guard; adjacency proves no intervening operation can
change it. Keep frame registration and storage layout unchanged. The unused
fallback carrier is initialized frame storage and is not read on the direct path.

The helper normalizes to UInt16 and publishes a real empty RootScope for the
instance-method frame before calling the existing allocating scalar factory.
The caller's local contains no managed reference; no owner root or native pointer
escapes. The factory retains its own real frame, original allocation fault
origin/materialization and no-replay-after-allocation behavior. The caller roots
the result before its next allocation. Fresh string identity is preserved.

With CILICA_VERIFY_BORROW_LIFETIMES=1, typed internals disabled, pending exceptions,
marking or depth-boundary conditions, construct the original carrier at the
original IL location and call the original borrowed method. Native single-unit
and module output use the same declarations. No public boundary, collector,
intrinsic grant, managed profile, quota or native layout change.

Review checks the single-use proof, validator constraints on location consumers,
block boundaries, stable guard between adjacent statements, pointer/root lifetimes,
diagnostic restoration, native driver splitting and fault/depth ordering.
Full validation, provenance and before/after timing are retained in
[the performance report](../performance/local-character-performance-20261020.md).
