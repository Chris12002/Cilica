# ADR 0135: Owned read-only collection contracts

Status: Accepted

## Decision

Supply Cilica-owned declarations for IDisposable, nongeneric IEnumerable and
IEnumerator, and generic IEnumerable, IEnumerator, IReadOnlyCollection and
IReadOnlyList in the zero-reference core. Preserve the pinned .NET Standard 2.1
interface inheritance, covariance and exact member signatures and flags. These
are ordinary interface contracts and grant no intrinsic privileges. Existing
closed interface dispatch executes application implementations through the
explicit library mapping.

Keep the declarations in CollectionContracts.cs and include that file in actual
core builds. Do not substitute a special compiler collection or erase inherited
interfaces to bypass unsupported semantics. List storage, runtime vector helpers
and variance-only executable dispatch remain separate work. No compiler/runtime
profile or intrinsic grant is added.

## Evidence and review

All 33 distinct focused checks pass, covering mapped collection execution,
existing core probes, member correspondence and closed interface translation.
Six new probes supply 1,764 repeated native results at O0/O2 with ASan/UBSan,
collection before allocations, frame exhaustion and ownership cleanup. They
cover Int64 elements, reference identity, generic/nongeneric enumeration,
inherited Count/MoveNext/Reset/Dispose, invalid enumerator states and index/null/
arithmetic exception order. Array and variance-only calls remain diagnostic.
Every declared member and the complete inherited interface set are compared with
the pinned reference image; binding also checks generic parameter attributes.

Review found no remaining actionable issue. The change adds no generated C++
or new storage/ownership path. Local main remains an ancestor; no hosted work was
run. The reviewed local core fixture records 203 inputs and the existing 65 grants:
image SHA-256 `56ac3fd703a86e4bea79006539a4fef8dded8cdc4e146301ace1d00edd1f13d0`,
manifest SHA-256 `33cc3c6e5a79cc58450c8dd6671b7d286a02381e9a933e3af6cf24bf2cbb0705`.
Production approvals remain empty. The actual mapped kernel now stops at missing
System.Action`2 (reference TypeDef 0x02000007); native kernel equivalence remains
pending.
