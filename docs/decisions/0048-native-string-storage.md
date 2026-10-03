# ADR 0048: Native immutable UTF-16 strings

Status: Accepted for bounded retained-heap execution.
Recorded 2026-10-03.

## Storage and identity

Native v9 implements the five String operations from ADR 0046 and their selected
metadata/CIL inputs from ADR 0047. String remains an intrinsic nominal identity,
with an exact descriptor and Object as its sole modeled ancestor. It participates
in normal reference fields, roots, vectors, type tests and dispatch signatures;
it has no ordinary mutable class fields or callable framework method bodies.

Use a standard-layout header with Int32-bounded code-unit length and an owned
`uint16_t` buffer exposed through a const pointer. Fill the buffer before publishing
the object into the instance heap. Charge both header and payload against the
existing byte/object limits. Check combined length with subtraction and check
available bytes by division before multiplication or copying. Failed header or
buffer allocation leaves the heap counters/list unchanged and releases unpublished
storage. Instance teardown destroys every header and buffer. Embedded zeroes and
unpaired surrogates never pass through a Unicode scalar encoder or C++ text literal.

Deduplicate emitted literal payloads by ordinal numeric contents across methods.
Use a small linked cache inside each instance, with a reserved ID for canonical
empty and stable IDs for other literal contents. This avoids a large inline cache
on the native stack. The compiler allows 256 distinct authored contents; an implicit
empty entry can add one cached object when the program did not author an empty
literal. Cache entries consume ordinary heap budgets, survive until instance
teardown and cannot leak identity across instances. They are not process globals.

Two nonempty concat operands create a fresh noninterned object. A sole nonempty
operand is reused; two null/empty operands return canonical empty. Cache hits and
operand reuse need no allocation and work when the allocation budget is exhausted.
Ordinal equality handles null separately and compares lengths and numeric units.
Reference equality remains the existing pointer identity operation.

## Checked execution and roots

Generated methods validate ownership and exact String descriptor membership before
reading the payload. Null precedes bounds faults; unsigned index comparison rejects
negative Int32 indices. Code-unit loads zero-extend into Int32. Operand evaluation
still precedes the intrinsic operation, including a failing index calculation on
a null receiver. String index faults use status 10; null uses 4, bad private native
references/types use 6, and size/heap allocation exhaustion uses 5. Every managed
fault records its method/image/IL origin and leaves the packed Int32 output alone.
No String reference crosses that boundary.

Literal and concat results publish into existing phase A root slots. Both concat
operands remain published when allocation validates roots and invokes the private
observer. Separate generated steps and noinline payload helpers preserve bounded
O0 sanitizer frames. Execution-arena cleanup applies on success and failure.
The retained heap does not collect: the literal cache must be added to future
collector tracing, and these tests do not establish roots across reclamation.

## Unsupported String interfaces

Review identified a cross-feature hazard: String's current model widens only to
Object, but .NET String implements interfaces. Enabling native execution without
another gate would give wrong results for `object value = "cat"; value is
IComparable` or for storing that value through an Object[] view of IComparable[].

Conservatively reject metadata catalogs combining String with any interface
implemented by the selected String. Read only its bounded interface ancestry,
including nongeneric ancestors of generic contracts, using the shared metadata
ledger. Do not import interface bodies or enable generic execution. Cache this
negative-capability information per translation. Unrelated user interfaces with
String parameters/results remain valid. Detached catalogs continue to assert the
explicit Object-only String contract; they do not authenticate runtime assemblies.
This gate can be removed only when actual String interface ancestry and required
dispatch semantics are implemented together.

## Validation and review

Local review checked numeric payload preservation, C++ object/subobject access,
checked length/byte arithmetic, failure publication, literal ownership, descriptor
membership, operand roots and fault cleanup. Reduced regressions cover the interface
cast/store hazard and exact/short interface-scan budgets.

A warning-free build and 739 selected local regression checks passed, including
object, array, interface/virtual dispatch and native execution regressions. The
String harness compares authored .NET and native contents/identity/fault outcomes
at O0/O2 with ASan/UBSan, warning errors and static stack reports. It also checks
exact/one-short heap and arena budgets, object limits, instance separation, cache
hits at exhaustion, failed-allocation retries, foreign/unreadable references and
copied descriptors. The final focused native run passed 150 .NET/native outcomes at each optimization
level, additionally verifying every unit across concat boundaries, including a
surrogate pair and lone surrogates. Maximum measured static frames were 784 bytes
at O0 and 720 at O2, below the 2 KiB baseline. The final build had no warnings.
These focused checks overlap the regression suite and are not additive coverage.

Local main remains an ancestor. No hosted CI, push or additional agents ran.
The next slice is the composed example/review (3g); collection and broader String
APIs remain separate work.
