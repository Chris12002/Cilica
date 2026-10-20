# ADR 0217: optimized primitive array and string reads

Accepted 2026-10-20. Native v110, following committed v109.

The [v109 investigation](../performance/v109-profile-20261020.md) found six
one-read runtime helpers still forced out of line: array_length,
load_integer_element, load_wide_element, load_reference_element, string_length
and string_code_unit. Roslyn emits ordinary ldlen/element operations; Unity
exposes direct length fields and inline element reads to its native compiler.

Use CILICA_PRIMITIVE_READ to request always_inline under __OPTIMIZE__, retaining
noinline at O0. The macro supplies attributes only; the existing shared-module
builder still converts namespace static to inline for one cross-unit identity.
Helper bodies, all caller null/bounds/type checks, roots, borrowed lifetimes,
fault order and stores remain unchanged. This does not widen typed eligibility.

Review checked the six exact replacements, native pointer provenance, absence of
allocation/safepoints, preserved checked callers, O0 attributes and shared linkage.
Fresh DiffPlex emission is byte-identical to v109 after normalizing only those
attributes, the macro and profile label. All 54 focused native checks pass,
including O0/O2, shared modules, typed execution off/on, UTF-16, wide integer
values and ordered faults. All 523 DiffPlex observations match in release and
both forced-GC ASan/UBSan modes on a checked 512–576 KiB stack. Maximum native
frames are 432 bytes release and 384 bytes sanitized. No unresolved review issue.

Paired release measurements improve 30.193 to 28.071 us/diff, 7.03% less time.
See [measurements and retained evidence](../performance/v110-primitive-reads-20261020.md).
