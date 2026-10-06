# ADR 0184: Bounded numeric record embedding

Accepted 2026-10-06; milestone 7c.5. Desktop evidence only.

## Context

V3 transfers scalar bits, retained references and strings, but applications also
need copied structured values. Exposing a C++ struct would make padding, metadata
field order and compiler layout part of the public contract. The compiler already
lowers values to scalar slots and returns them through checked internal locations.

## Decision

Add `EmbeddingObjectContract.Records` with at most 32 declarations. Each binds a
stable ID/name to a public value type and declares all 1..64 numeric scalar leaves.
Fields have stable IDs/names, exact metadata paths and Int32/Integer64 kinds.
Every nested declaring value and field must be public. Reject reference-bearing
or empty values, duplicate/missing paths and mismatched widths. Record shapes
match logical signatures before lowering; the lowered signatures are checked
again, including hidden return locations. This requires the composed emitter.

Sort host fields by stable ID and explicitly map them to the existing native
scalar plan. The canonical schema includes record and field IDs/names/kinds;
physical types/paths remain separate bindings. Existing contracts without records
retain their hashes and layouts. Record capability is 2048; any wide leaf also
advertises Integer64 capability 512.

Generate `invoke_records` for modules with record declarations. Parameters expand
to tagged numeric leaves in host field order; top-level scalars and handles each
retain one slot. A separate caller-owned output buffer receives result fields,
while the normal result carries kind Record and the stable record ID. Ordinary
invoke variants reject record exports. Scalar exports also work through the new
function. No host struct layout, borrowed managed pointer or external allocator
is introduced.

Validate capacity before admission/execution, then validate all input fields
before reserving an optional exception/result handle or running managed code.
Allocate argument and result scratch storage against the existing execution
budget. An internal frame and borrow scope expose checked return locations to
the lowered callee. The scope remains alive through the call, then copies numeric
fields only when no fault is pending. No allocation occurs during publication.
Failures leave the field buffer untouched; ordinary managed effects are not
rolled back. The optional exception output uses existing capture ownership and
quota rules. Public ref/out parameters remain unsupported.

## Evidence and review

C11 shared-library consumers compare nested copies, stable field reordering,
mixed records/handles, allocation and wide wrapping with .NET at O0/O2 under
ASan/UBSan. Forced collection, malformed fields, preflight short buffers, unchanged
inputs/failure outputs, exception capture and zero quotas have local coverage.
Contract checks reject private/reference leaves and invalid schemas.

An initial unoptimized adapter exceeded the existing 2 KiB frame warning gate.
A separate return helper and loop-based carrier construction fixed that growth.
The full 64-field regression passes O0/O2 with sanitizers and the unchanged gate.
The milestone record reports exact test counts and rerun scope.

Review covered logical/physical signature agreement, stable mapping, arithmetic
bit preservation, return borrow lifetime, resource bounds, output ordering and
capture cleanup. Numeric scratch contains no managed references; top-level
handle arguments remain rooted by admitted leases. Reference-bearing records,
empty values, public borrows, independent-root discovery and phone-device
evidence remain separate work.
