# ADR 0196: Trust internal references during generated execution

Status: accepted. Recorded 2026-10-19. Extends ADR 0195; supersedes historical
requirements to reject arbitrary pointers passed directly to private generated
methods during normal execution. Public embedding contracts remain checked.

## Decision

Generated receiver/argument/field guards, array guards, casts/type tests,
reference-array stores and managed storage borrows no longer search the entire
allocation list during each normal access. A non-null reference in these paths
must already identify live managed storage. `CILICA_VERIFY_INTERNAL_REFERENCES`
defaults to 0; native compilation with 1 restores membership scans and their
status-6 rejection before dereferencing a forged pointer. Other flag values are
rejected. The switch is independent of allocation-root verification, NDEBUG,
optimization level, observers and sanitizers. Use one setting across the build.

The shared `internal_reference_owned` helper only gates the membership scan.
`managed_reference` additionally retains owner-header and known-descriptor checks
for borrows and stored payloads. A header check cannot authenticate an arbitrary
pointer: these are trusted execution helpers, never boundary validators.

## Reference provenance and boundaries

- Translation and detached IR validation constrain reference construction,
  assignments, call signatures, field layouts and casts. Reference arithmetic,
  unsafe pointer fabrication and unrestricted native calls are outside this profile.
- Runtime allocations publish instance-owned objects. Generated roots keep live
  receivers, arguments, locals, return values and borrow owners alive across allocation.
- Public V3 calls validate tokens and parameter type schemas before creating
  argument packets. Strong handles stay rooted under instance admission; foreign,
  released or forged tokens do not enter generated execution. Results are checked
  with `traceable_reference` before handle publication.
- `Instance::owns`, `traceable_reference`, handles and all GC visitors remain
  unchanged. They compare membership before inspecting a possibly invalid pointer.
  Invalid or incomplete collection does not authorize sweeping.
- Borrow frame registration, lifetime tokens, readonly checks, descriptor identity,
  field/array extents, covariance and type predicates remain. The changed
  `location_reference` helper is also used by internal boxing/delegate payload
  operations; it is not used for host handle admission or collector edge validation.

Null checks, array bounds, actual-element store compatibility, cast failures and
exception ordering for admitted managed programs are preserved. Deliberately
corrupting private pointers is now a diagnostic-mode test contract. An allocation
observer enables allocation-root verification only; it does not automatically
turn on internal-access verification. Private corruption may cause undefined
native behavior in normal mode; it is not a supported host entry mechanism.

## Scope and review

The allocation list is retained as the collector inventory. No heap index,
allocation overhead, object-layout change, root elision or calling-convention
change is introduced. Native profile 90 and the public ABI are unchanged.
Other runtime checks, including exception/cache validation and boxed/delegate
receiver validation, still use checked membership and can be profiled separately.

Review traced every changed call site and verified that handles, exported results,
GC roots/edges and inventory validation never use the shortcut. Short-circuit
ordering in diagnostic mode still compares membership before reading a header.
Reference-bearing roots and managed array store type checks are unchanged.

## Validation

The object and array conformance harnesses run at O0/O2 with ASan/UBSan in both
modes, including managed faults, identity, covariance, roots and budgets. Existing
private forged-pointer harnesses explicitly select diagnostic mode. New checks
cover generated field mutation and borrowed access, cyclic/live retention, forged
and foreign handle payloads, stale/cross-instance tokens and collector rejection
in both modes. Temporary detachment of readable storage distinguishes actual
membership verification from a header check without performing allocation/GC
while detached. Ordinary managed boxing/delegate regression runs exercise the
shared borrow helper without enabling diagnostics.

All 104 targeted checks pass. Fresh DiffPlex release medians improve from
15.481 ms to 0.533 ms (29.030x); all four 523-observation transcripts and two
forced-collection operations pass. Emission, validation and timing are recorded in
[the performance report](../performance/internal-references-20261019.md).
