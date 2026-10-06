# ADR AC0003: Nullable named-value boxing and conversions

Accepted 2026-10-06 for AC1b.1 in the integer managed-object profile.

## Decision

Translate box and unbox.any of the exact selected core Nullable<T> into ordinary
managed calls to the selected Cilica.Runtime.NullableBoxing<T> implementation.
Validate the helper's static copy signature and accessibility, and charge ordinary
method, metadata, argument and storage budgets. Same-named application types do
not acquire this behavior. No new intrinsic grants or public embedding ABI are
introduced.

An empty nullable returns null without allocating a box. A present nullable boxes
its underlying T through the existing allocator, nominal descriptor and tracing
map. Unboxing null returns the default nullable; another object must have exactly
T's existing boxed identity, then its payload is copied into a present nullable.
Copies preserve embedded reference identity while isolating value fields. Helper
parameters and caller-owned result packets use existing roots across collection.
Generated fault provenance follows the managed helper body, as with other owned
library calls; this slice does not implement managed stack traces.

The owned implicit operator constructs Nullable<T>; the explicit operator uses
Value, including its empty-value exception. C# usually emits constructor/accessor
calls instead of these operator methods, so authored IL separately exercises the
actual operators. Checked/unchecked integer conversions and representative lifted
arithmetic, relational and equality expressions use their existing CIL lowering.
This is evidence for those expressions, not every possible lifted operator.

## Payload and operation boundaries

The pre-existing boxing backend admits named inline values, including closed
generic and reference-bearing structs, Guid and empty structs. It does not admit
primitive or enum boxes. Reject those nullable payloads before helper discovery;
do not represent Int32 or enum boxes as anonymous/empty structs. Their nominal
payload descriptors and operations are the next AC1b prerequisite.

Raw unbox of Nullable<T> remains a build error: its temporary managed-location
lifetime is distinct from unbox.any's copy. Nullable isinst/castclass, ValueType
views, boxed interface dispatch, equality/hash/text and nullable key operations
retain their existing bounds. General collection compatibility is not claimed;
object-array storage is validated, while nullable-key selection remains AC3.
Full AC1b remains open until required remaining operations are implemented or
proved absent from the accepted replay closure.

## Cast messages and native representation

A local .NET probe established that a failed nullable unbox names Nullable<T>,
whereas a direct T unbox names T. Annotate the helper's exact underlying unbox
with a diagnostic-only nullable target flag. Preserve that flag through scalar
lowering, cast-text selection and the private exception payload. Runtime identity
checks still use T; ordinary and nullable cast messages coexist in one closure.
No managed payload or collector reference map changes are needed.

Boxed value IR v4, scalar boxed IR v5 and native profile v38 record this extension.
Resource selection is a separate non-inlined native helper: the first O0 sanitizer
build exposed a 2,112-byte exception-message frame against the existing 2,048-byte
limit. Splitting selection from validation/allocation passes that unchanged limit.

## Evidence and review

NullableLibraryTests passes 27 oracle/native probes at O0/O2 on macOS ARM64:
7,938 repeated comparisons, forced collection, ASan/UBSan, and frame/cleanup
checks. Coverage includes null/present roundtrips, fresh boxes, underlying GetType
identity, empty and wide structs, Guid, generic/reference-bearing payloads,
object-array storage, copy isolation, checked conversions, direct operator calls,
and exact ordinary/nullable invalid-cast messages. Separate heap-limit tests prove
empty boxes need no extra object slot, present allocation failure preserves clean
frames, and the runtime can recover and reuse the instance.

The focused local validation run passed 120 tests, including existing boxed IR,
boxed CIL, managed/argument/type-initialization exceptions, embedding exception
capture, and the public expected-gap ledger. Review checked exact identity,
resource charging, exception order/text, initialized copied storage, rooting and
private exception allocation size. No hosted work or private replay rerun occurred;
AC1a's private Alchemy diagnostics remain historical observations.
