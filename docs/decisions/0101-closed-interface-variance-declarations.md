# ADR 0101: Closed interface variance declarations

Status: accepted for slice 6c.10 metadata admission. Recorded 2026-10-05.

## Decision

Closed generic constraint validation accepts a single covariant or contravariant
flag on an interface type parameter. It rejects conflicting variance directions,
unknown flags and variance on class, method or delegate parameters. Existing
class/struct/default-constructor constraints continue to apply independently;
variance alone does not demand argument traits or imply a reference constraint.

The closed snapshot preserves exact generic arguments, structural inheritance
and specialization identity. Reading an `out` or `in` declaration does not enable
variance conversions or dispatch. Executable generic interface layouts remain
unsupported until their complete assignment and dispatch semantics are lowered.
In particular, a type test that would depend on covariance must diagnose rather
than silently return an invariant-identity result.

## Validation and review

Authored metadata tests cover both directions, integer/reference arguments,
combined special constraints, conflicting/unknown flags and invalid class
declarations. A selected runtime `IReadOnlyList<long>` snapshot retains its closed
interface inheritance. An independent C# covariant type test confirms the .NET
result while asserting that current native translation rejects it. Review checked
the declaration owner, flag masks, unchanged constraint work accounting and
separation from executable assignment rules. All 84 focused checks pass. No runtime output changed.
