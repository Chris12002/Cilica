# ADR 0098: Int64 inline and boxed value payloads

Status: accepted for slice 6c.7 authored IR. Recorded 2026-10-05.

## Decision

Under the explicit wide catalog capability from ADR 0097, inline value fields
may use Int64 storage. Flattened copy plans retain Int32, Int64 and reference
leaves independently, including nested values. Default initialization creates
zero-valued Int64 leaves. Value copies, borrowed fields and caller-owned return
packets reuse the existing typed scalar lowering and phase A roots.

Private boxed payload runtime v2 emits UInt64 members and a distinct wide storage
kind. Box, copy and replacement packets use their wide-bit member, while only
reference leaves enter tracing maps. Packet validation completes before writes,
so an invalid later reference cannot partially overwrite earlier scalar slots.
Readonly and lifetime rules continue through the existing checked carriers.

## Validation and review

An authored nested struct contains an Int32 marker, an Int64 field and a reference.
Allocating factories return it through caller-owned packets; two boxes preserve
distinct identity, an unboxed snapshot remains independent of mutable unboxing,
and an allocating mutator returns the changed nested value. All 180 repeated
O0/O2 ASan/UBSan results match .NET under collection before allocation. Native
probes also verify full-width copy/replacement, atomic rejection of a bad final
reference, allocation failure, root cleanup and reclamation.

The focused run initially exposed a missing composition admission case; the new
wide probes pass after correcting it. The other 119 focused checks passed,
including existing boxed and struct-return native probes; 28 composition checks
also pass after the fix. Malformed payload
widths and omission of the explicit wide capability fail validation. Review
checked leaf order, return destinations, default initialization, reference maps,
packet kinds and failure atomicity. No actionable findings remain. No hosted
work was used. Wide CIL discovery and wide arrays remain subsequent slices.
