# ADR 0105: Closed interface assignment rules

Status: Accepted

## Context

The kernel uses closed generic interfaces. Their declared variance must become
an exact type relation before dispatch can safely select implementations.
Equal native storage widths do not imply equal managed argument types.

## Decision

The explicit integer-object profile retains closed argument keys, reference
identities and declared variance in interface catalogs. Metadata validation checks
variance positions through nested interface declarations before using their flags.
Unsupported variant delegates and dependent method constraints remain diagnostics.

Assignment follows a bounded fixed point over declared inheritance, reference
interface variance and vector covariance. Value arguments require exact keys;
invariant parameters never widen. This admits nested reference conversions while
preserving exact closed identities. The existing 256-type bound is supplemented
by 4,096 argument facts and 1,048,576 relation-work steps. Detached catalogs check
argument consistency, visibility, capability and bounds before computing relations.

String arguments use the selected intrinsic identity. Catalogs that mention an
interface implemented by String remain diagnostic until those conversions are
modeled; matching the generic definition also catches closed specializations.
Extra runtime generic flags can produce an earlier diagnostic.

## Validation and consequences

Four C# probes match .NET in 1,176 repeated O0/O2 ASan/UBSan results with forced
collection, exact/insufficient frames and cleanup checks. They cover covariance,
contravariance, invariant/value negatives, nested interfaces and array arguments.
Authored metadata tests reject illegal variance positions and nested declarations;
detached catalog tests cover malformed facts and JSON round trips.

This slice enables assignments and type tests, not interface method dispatch.
Legacy execution profiles retain their earlier admission boundaries.
