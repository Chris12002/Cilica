# ADR AC0098: Bounded Int32 enum composite formatting

Recorded 2026-10-07 for AC7b.13.

## Decision

The frozen engine's missing-victory-evaluator error directly boxes an Int32 enum
into String.Format. Admit one/two directly boxed Int32 enums with unique values
and no custom attributes. Keep the ordinary named-value bridge and its independent
same-block operand proof. Do not interpret every enum as an ordinary ToString
override: format suffixes affect enum behavior. Attributed enums (including Flags),
duplicate-value aliases and other carriers fail explicitly before execution.

EnumText<T>.Render is ordinary owned managed code selected by the authenticated
BoxedText<T> bridge. It supports default/G/g names, D/d decimal, X/x eight-digit
uppercase hexadecimal and F/f unsigned descending greedy decomposition. Exact
values take precedence; zero/unknown values use decimal fallback. A first pass
proves complete decomposition before any names are copied. Managed concatenation
replaces pinned span buffering, so allocation-count/memory-threshold equivalence
is not claimed. Numeric fallback uses the existing invariant culture contract.

Four exact private EnumFormatting grants admit Bits, Count, ValueAt and NameAt.
Bits becomes existing exact scalar UnboxValue, Count becomes a constant, and the
two indexed reads become EnumEntry. Discovery shares the bounded constant cache
with Enum.IsDefined; its separate formatting reader rejects attributes/aliases
without changing IsDefined's accepted metadata. Assemblies remain data.

EnumEntry retains enum identity and a typed index; both value and scalar validators
require complete Int32 enum metadata, String storage, an earlier Int32 index, the
exact result type and exceptional edge. Root/safepoint planning and scalar lowering
retain the operation. Native tables are sorted by unsigned value (declaration order
for ties); bounds checks reject negative/out-of-range indices, and name reads copy
immutable UTF-16 through the existing quota-aware allocator. No user object pointer
is retained across name allocation. Native v60/object IR v33/integer CIL v30;
layout and production type/method/metadata limits are unchanged.

## Validation and review

Six probes each cover 221 input pairs, three repetitions, O0/O2: 7,956 native/.NET
comparisons with forced GC, ASan/UBSan, 2,048-byte frame warnings and unchanged
30-second subprocess caps. Sparse/empty enums, Unicode names, Int32 limits, unknown
values, all suffix forms, alignment/escapes, mixed struct callbacks, repeated
placeholders and malformed-format/argument order are covered. Additional tests
check exact grants, hostile private callers, excluded shapes, raw metadata budgets,
invalid detached IR at both stages, native table bounds/descriptors and quota
recovery. Existing ordinary formatting, Enum.IsDefined and 40 public native
controls pass; seven diagnostic gaps remain.

The first build exposed an invalid cross-project metadata-reader access and a
local-name collision; both were corrected before tests. The first 18 enum checks
passed. The 62-check regression run had 61 passes and one stale scalar diagnostic
expectation; only that wording changed. A final 18-check admission run passes,
including all seven expected gaps and the shared-cache bound review. Together
these cover 74 distinct checks. All 63 pinned upstream originals still hash-match.
Review checked scalar confinement, actual box proof, suffix/error order, unsigned
arithmetic, name lifetime, allocation faults, cache bounds and table validation.
Local main remains an ancestor at 48ecf14.

Evidence: artifacts/alchemy-enum-format, including ac7b-13{a,b,c}-tests.log, build
logs and hashes.json. Core SHA-256:
a5e821fc5565d9c49c651b67b43d80292a450648d690dbfb8f1bdc8eb6225822.
Manifest SHA-256:
d9f5fa9012da20eadab4218d40517a12c16f3701a90d93c06e733642602fe434.
Fixture SHA-256:
ef12594c81f14beb4e3b2053bcc0f4153ebea5766c810ca36d6fdf4d8630bf6e.
Full engine/replay completion remains unverified; refresh the frozen diagnostic
closure after this committed slice.
