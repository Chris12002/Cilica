# ADR AC0005: Exact boxed type tests

Accepted 2026-10-06 for AC1b.3 in the integer managed-object profile.

## Decision

Admit isinst/castclass for existing scalar and named-value box descriptors,
including public nongeneric Int32 enums, closed/reference-bearing structs and
Guid. A selected Nullable<T> operand tests the exact underlying T descriptor.
Validate its closed arity, struct constraints, access and metadata budgets even
when no nullable storage is used by the method. Same-named application types do
not acquire nullable behavior. Unsupported widths, enum visibility and layouts
retain their existing diagnostics.

Extend the existing IsInstance/CastClass IR with an explicit BoxedTarget flag.
A boxed test returns the selected Object reference view, not inline T storage or
a fabricated reference-class view of T. Validate the target against the payload
catalog at both value and scalar stages, and count test-only descriptors as used
roots. Ordinary class/interface tests retain their existing reference rules.
This reuses operand remapping, root assignment, fault edges and constructor checks
instead of creating another family of otherwise identical reference operations.
Boxed IR v6, scalar boxed IR v7 and native v40 identify the extension.

Native tests check instance ownership before inspecting a non-null descriptor.
A match returns the original reference; isinst returns null for a mismatch or null
input, while castclass preserves null and throws InvalidCastException for a
non-null mismatch. Success does not allocate, copy or reinterpret payload memory.
Existing exception/root machinery covers allocation on the failure path. There
is no public ABI, allocator, core source, binding or intrinsic grant change.

## Exact identity and fault names

A .NET oracle confirms type tests require exact nominal identity. An Int32 enum
box fails both Int32 and Nullable<Int32> tests, even though ordinary unbox.any
permits the existing Int32/Int32-enum compatibility group. Do not share that
compatible-unbox predicate with type tests.

Direct authored IL exposed a second distinction: a failed castclass Nullable<T>
names T in its exception message, while failed unbox.any Nullable<T> names
Nullable<T>. Lower the nullable test to T completely, including cast resources.
The nullable-unbox diagnostic flag remains specific to unboxing. Raw cast probes
check the exact message against .NET and distinguish successful identity results
from caught failures so an incorrectly admitted conversion cannot pass the test.

## Evidence and review

Nineteen C# probes and ten direct IL probes provide 8,526 repeated native/.NET
comparisons at O0/O2 on macOS ARM64 with forced collection and ASan/UBSan. They
cover all admitted scalar widths, enum identity, null/present nullable forms,
Guid, empty/closed/reference-bearing structs, type-test-only descriptors, original
box identity, strict cast failures and exact error messages. Existing frame-budget
and ownership-cleanup checks apply. Native compile timeout and sanitizer frame
limits remain unchanged; C# bundles contain six probes each.

Detached mutation tests reject missing/wrong payload targets, forged result views
and invalid operands at both stages. Metadata probes reject invalid nullable
constraints/arity. General filters, scalar interface casts/dispatch and raw scalar
unbox retain diagnostic coverage. Focused existing cast/box/exception/embedding
and public gap-ledger regressions also pass; see the milestone outcome for totals.

Review checked null short-circuiting, owner validation before descriptor access,
no payload alias casts, retained roots/identity, exact versus compatible matching,
closed constraints, descriptor-only reachability and unchanged class access rules.
The first raw nullable-cast run caught the fault-name distinction above; the
reduced probe passes after correcting it.

## Remaining scope

Raw scalar/nullable unbox locations, complete primitive interface contracts or
narrower use proofs, nullable key/equality/hash/text semantics, ValueType/Enum
views and general exception filters remain follow-on work. Existing conservative
boxed dispatch restrictions remain in force. Full AC1b and Alchemy saved-game
replay are not complete; required deferred operations need implementation or proof
of absence from the accepted replay closure. Private Alchemy roots were not rerun.
No hosted work, services, extra agents or additional platform validation occurred.
