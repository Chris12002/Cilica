# ADR 0179: Shared scalar export module

Accepted 2026-10-06 for the bounded milestone 7b module. Device support remains
unverified; iOS is the first planned phone target.

## Decision

Expose selected public static Int32 methods from one validated compiled closure
through `EmbeddingModuleEmitter` and a version 2 C interface. An application-owned
contract assigns stable UInt32 IDs and names independently of metadata tokens.
One runtime token owns the heap, statics, initializer state and descriptors shared
by all declared exports. A bounded switch dispatches only declared IDs and rejects
unknown IDs/wrong arity before acquiring the runtime.

Validate exports in logical IR before value lowering. Otherwise a one-field struct
parameter could look like a scalar export after flattening. Check public ownership,
static kind, non-initializer/special-name method, exact identity and expected arity;
bound export count at 64 and reject duplicate IDs/names/methods. Generate names only
from restricted ASCII identifiers that cannot create reserved C++ identifiers.

Emit a deterministic JSON manifest. Its interface fingerprint covers ABI version,
module/schema identity and sorted export signatures, while exact compiled method
bindings remain separate build evidence. Creation requires the host's expected
fingerprint so a stale header fails before execution. The fingerprint does not
authenticate code or establish semantic equivalence across builds. Preserve the
v1 boundary rather than changing its calling convention.

Use the shared private admission registry for both versions. V2 additionally copies
per-instance heap/object/execution quotas, permitting reductions (including zero)
but no increase above the existing bounded defaults. Capability bits enumerate
only implemented boundary features. A separate non-inlined packet helper per export
keeps native stack storage independent of the number of switch cases. The guard
remains busy until pending-exception roots, execution frames and arena cleanup finish.

## Alternatives and limits

Do not join separately emitted runtime worlds: their descriptor/static slot layouts
can differ even when methods originate from the same assembly. Do not weaken IR
reachability/catalog validation to make unrelated roots fit. This slice selects
methods already discovered through one ordinary inclusion root; a facade can call
them for discovery. The runtime never executes that facade on create. Independent
multi-root discovery remains future compiler work.

The module accepts only Int32 logical storage signatures. Retained objects,
strings, wide values, buffers and structured exception type/message copying are
the next boundary slice. One module artifact is generated per library; existing
private closure symbols are not renamed for combining duplicate closures.

## Evidence

C11 consumers link a generated shared library at O0/O2 with hidden internals and
ASan/UBSan. Reset/step/read/recovery exports share persistent state, match an authored
.NET transcript and preserve roots with ordinary/forced collection. Other instances
remain isolated. Tests reject mismatched manifests, invalid configs and unknown
exports, and exercise heap/object/execution exhaustion. Negative contracts cover
private methods and struct signatures before flattening.

A 64-export authored closure compiles under the 2 KiB native-frame warning at
O0/O2, dispatches every ID and stresses concurrent entry across different exports.
The four v1 lifecycle/race cases still pass after sharing the registry. No hosted
jobs, services, phone builds or publication are part of this slice.
