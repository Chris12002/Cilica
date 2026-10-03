# ADR 0047: Selected String metadata and bounded intrinsic translation

Status: Accepted for metadata/CIL translation; ADR 0048 subsequently adds bounded native execution.
Recorded 2026-10-03.

## Identity and shared storage

ADR 0046 defined detached immutable strings. This slice connects actual assembly
metadata and CIL to those contracts without importing the runtime library's String
fields, interfaces or method bodies.

Resolve String through the explicitly selected runtime library, including its
normal type forwarding. Require a public sealed nongeneric top-level implementation
definition directly extending the selected Object. Reject String type/module
initializers. Selection asserts the library's intrinsic semantics; this is not
authentication of an arbitrary library's method bodies. Equal names or tokens in
another image do not establish this identity. Resolve lazily so primitive-only
programs and small test libraries need not supply String.

Use the existing shared signature grammar for String parameters, returns, locals,
fields, vectors and class/interface dispatch signatures. Bare selected String
type operands also work for reference-vector allocation and casts/type tests.
Catalogs retain the selected identity separately from mutable class layouts and
count it toward the existing type budget. String still widens only to Object;
framework interfaces and ordinary String method/field access remain unsupported.

## Literals and exact member recognition

The metadata reader exposes a bounded numeric UTF-16 user-string snapshot. Check
the token, compressed byte length, payload extent and terminal flag; check the
caller-supplied unit budget before allocating/copying. Read ushort units directly,
preserving embedded zeroes and lone surrogates without a replacement encoder.
`ldstr` produces the existing throwing StringLiteral operation at its IL origin.

Translation counts every literal occurrence across reachable methods, including
repeated references to one heap token, against the existing 262,144-unit budget.
Each literal remains limited to 65,536 units and distinct contents to 256. Cache
by method/IL site so stack-inference revisits do not charge the same occurrence
again. The detached validator independently enforces the final program's budgets.

Resolve calls normally, then recognize only the selected String owner with these
exact nongeneric public signatures and call forms:

| CIL call form | Member | IR result/operation |
| --- | --- | --- |
| `callvirt` | `Int32 get_Length()` | StringLength |
| `callvirt` | `Char get_Chars(Int32)` | StringCodeUnit, zero-extended Int32 |
| `call` | `Boolean Equals(String, String)` | StringEquals, ordinal 0/1 |
| `call` | `Boolean op_Equality(String, String)` | StringEquals |
| `call` | `Boolean op_Inequality(String, String)` | StringEquals followed by comparison with zero |
| `call` | `String Concat(String, String)` | StringConcat |

Read declaration facts without reading intrinsic bodies. Retain operand evaluation
order and the call site's fault origin. Snapshot/recognition work uses the shared
metadata budget; intrinsic declarations share the 256-method limit with executable
bodies and interface declarations/facts. Calls still consume the argument ledger.
The Char/Boolean return signatures normalize only these operations to the existing
Int32 stack representation; general Char/Boolean storage is not enabled.

Unsupported overloads, instance Equals, constructors, direct-call getters,
callvirt static members, generic/native/access-invalid declarations, modifiers,
String.Empty and other field operations remain diagnostics. No name-only fallback
or framework-body discovery is permitted for the selected String owner.

## Validation and next boundary

Authored C# exercises literal/operation translation, null and index failures,
fields and arrays, casts/tests, reference joins, class virtual overrides and
interface signatures. A bounded test-only evaluator checks literal/intrinsic
operand wiring against authored .NET calls. Raw fixtures cover exact members
with deliberately unsupported bodies, wrong signatures/call forms/operands,
cross-image lookalikes, malformed user strings, initializer rejection and
exact/excess literal budgets across methods and inference revisits.

These are translation and detached-validation results. At this checkpoint the native emitter still
rejects all String catalogs, including programs containing only String locals.
The IR/native version identifiers stay unchanged because this slice produces the
already defined operations. Slice 3f-c must implement native immutable storage,
instance-local literal identity, checked operations, roots and .NET/native
differential execution. Existing native array/object/primitive execution remains
part of regression validation. No hosted workflows or additional agents are needed.


Review completed with a warning-free solution build and 1,020 selected local
regression checks passing, including existing native O0/O2 sanitizer execution.
A final 102-check string-focused run also passed after the API argument guard;
these runs overlap and are not additive coverage. Review checked image-scoped
identity, initializer gates, literal processing before allocation, combined
metadata/method budgets, operand order, phase A roots and the native rejection
gate. Local main remains an ancestor; no push, hosted runs or extra agents ran.
