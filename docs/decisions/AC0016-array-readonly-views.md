# ADR AC0016: Array read-only views

Accepted 2026-10-06 for bounded AC3.5.

## Decision

Implement Array.AsReadOnly<T> as ordinary managed code. Validate null with
parameter array before reading its length. Nonempty arrays get fresh live
ReadOnlyCollection<T> wrappers. Empty arrays share a per-closed-T view over the
canonical Array.Empty<T>, matching the pinned .NET 10.0.11 implementation. Use
existing instance-owned static reference storage for the private lazy cache. No
public Empty API is added to the netstandard2.1 contract. The profile is single
threaded; no process-global cache or new synchronization behavior is introduced.

Pass the length as an explicit Int32 argument to the private factory, since the
compiler rejects general ldlen/native-integer branch flow. This is an owned-core
adaptation, not application rewriting. The Array.Empty recognizer now yields
unrelated names to ordinary managed-body validation; its own exact declaration
and grant checks are unchanged. Unsupported internal calls still fail.

## Evidence and review

Six authored probes plus public ArrayAsReadOnly supply 2,058 repeated native/.NET
comparisons on macOS ARM64 at O0/O2, with ASan/UBSan, forced GC and frame checks.
Coverage includes live integer/reference/value updates, snapshot value copies,
nonempty wrapper identity, shared empty identity distinct from manual wrappers,
per-type separation, covariance, SyncRoot, read-only and range/null faults.

Two native cache checks prove instance isolation, exactly five retained objects
(two empty arrays, two views and the emergency exception), stable repeated calls,
and release of exactly two views after clearing their static roots. The generic
oracle harness uses its existing persistent-root mode for this intentional cache.
The first fixture build used the wrong existing field name; the initial library
length branch and cache-unaware cleanup checks were corrected without changing
native GC, budgets or application code. All 54 empty-array, interface, static
storage and baseline regressions pass, including exact grant denials.

Review checked null-before-allocation ordering, cache publication after successful
construction, tracing, generic identity, source-array ownership and unchanged
intrinsic validation. Native v41 and interface plan v5 remain unchanged. The
ledger has 25 gaps and 22 controls. Copying/enumeration remain open and private
replay has not been executed.
