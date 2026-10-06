# ADR AC0063: Executable closed generic interface methods

Accepted 2026-10-07 for AC8.15.

ThenBy needs a generic factory method on IOrderedEnumerable<T>. Compile each exact
closed method separately, with final newslot implementation selection from AC8.14.
Do not add runtime generic lookup or substitute a cast-based application adapter.

A declaration-only metadata reader authenticates owner, argument counts, closed
constraints and the simultaneously substituted signature without reading IL. It
reports all work against existing metadata limits, including declarations whose
call has no concrete receiver in the closure. Ordinary closed interface reads use
this same path. The generic bodyless interface declaration requires managed IL.

Interface declarations and selected bindings carry existing ClosedGenericEvidence.
Both validators recompute the combined owner/method specialization and owner-only
specialization, require nonempty exact method arguments, and preserve the same
method argument key across each mapping and implementation. Argument keys are
computed once per method, not repeatedly per target. Evidence consumes the existing
aggregate metadata-work bound and grants no access to private types. Native bodies
remain ordinary individually specialized functions using the existing packets.

Generic final slots must introduce themselves and occur in a validated interface
plan. Class virtual maps cannot use them. Direct/class generic instance calls and
method pointers are rejected before cached discovery; an earlier interface mapping
must not accidentally authorize them. Closed generic overloads may have identical
lowered signatures, so they retain unique MethodDef plus specialization identities;
metadata authenticates their distinct open signatures before substitution. Ordinary
nongeneric duplicate signature checks remain unchanged. The legacy interface reader
retains its prior implementation-flag boundary.

Profiles advance: integer-managed-objects-cil v15, object-operations-ir v24,
delegate-values-ir v6 and interface-dispatch-plan v11. Native v49 and layout v20
remain unchanged. No new library grant or larger resource limit is required.

Ten probes pass 2,940 native/.NET comparisons on macOS ARM64 at O0/O2 with forced
collection, ASan/UBSan, exact cleanup and the existing 2048-byte frame bound. Cases
include implicit/explicit/inherited implementations, multiple receivers, Int32 vs
UInt32 identities despite equal storage, Int64, references, reference-bearing inline
returns, void calls, null-only maps, generic body exceptions, overloads that collapse
after substitution and a multi-layer generic delegate factory resembling ThenBy.
Seven plan forgery cases fail at both logical and scalar stages; three unsupported
class/override/mixed call cases remain diagnostic. Seven new declaration-reader
checks cover forged signatures/arity/owner, open arguments, constraints, foreign
readers and exact work exhaustion. Together with adjacent interface, abstract class,
legacy plan and public baseline checks, 125 distinct selected tests pass. Logs:
artifacts/alchemy-generic-interface-methods/ac8o5-*, ac8o7-* and ac8o8-*.
The initial fixture namespace was corrected to the pinned C# 9 language version.

Review checked specialization separation, same-width map swaps, reader-free
provenance, null/fault order, existing rooted argument/result packets and cached
call-boundary enforcement. The frozen engine still stops at IOrderedEnumerable /
OrderBy in ReconcileControlOpportunity IL_0079; state still reports 257/256
discovered identities. These are partial closures, not emitted layouts. Refreshed
evidence is artifacts/alchemy-generic-interface-methods/engine-and-state.json.
Core hash remains 3319c4f4721183c8db50c969a42d63c5dd06faf32351340869e75207c0f0d153;
manifest hash is 576dbee7232c79b05b4b27ef92a44d6328390a1e461fe4d99324d6468dfc6f4b.
Public baseline remains 31 native controls and 16 gaps. Ordering, full engine emission
and saved-game replay remain unfinished.
