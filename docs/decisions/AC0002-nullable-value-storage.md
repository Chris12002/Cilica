# ADR AC0002: Bounded nullable value storage

Accepted 2026-10-06 for AC1a in the integer managed-object profile.

## Decision

Implement the selected owned-core `System.Nullable<T>` as an ordinary closed
value with a readonly Boolean presence field and readonly inline `T` payload.
The constructor, HasValue, Value and both GetValueOrDefault overloads are managed
bodies. Default initialization clears both fields. An empty Value getter throws
the selected InvalidOperationException with the pinned invariant message.

Reuse existing specialization, value copies, managed locations, return packets,
field/array layout and reference-slot tracing. There is no new native payload,
C ABI, runtime intrinsic or capability grant. An always-initialized payload lets
the collector trace the ordinary reference map regardless of presence; cleared
nullable storage contains zero references. Value access returns a copy while
references inside that copy preserve their object identity.

Closed constraint checking recognizes children of the selected System.Enum as
value arguments, including the struct constraint. It still checks the declared
class/value signature and rejects reference arguments and nested Nullable<T>.
Enum executable storage keeps its separate public Int32-only bound; accepting a
metadata constraint is not a promise to execute every enum representation.

## Explicit boundaries

Nullable boxing differs from ordinary struct boxing: empty becomes null and a
present value boxes the underlying payload. Until AC1b implements those rules,
box, unbox and unbox.any of the exact selected Nullable definition fail during
metadata discovery. Same-named application structs remain ordinary values. An
ordinary struct that contains a nullable field can still be boxed and copied.
Type tests/reflection retain their existing bounded diagnostics.

Review also identified the key-helper assumption that every value type is
nonnull. Nullable key operations now reject before taking that path; equality,
hashing and null selection need their own implementation. Nullable Equals,
GetHashCode, ToString and direct operator-method bodies remain explicit
UnsupportedLibraryMember calls. C# lowering that uses the admitted constructor
and accessors can work without admitting those direct operator bodies. Do not
interpret this slice as full lifted-expression or nullable-conversion coverage.

Direct struct-return assignment to heap fields and static inline-value fields
retain existing diagnostics. Materialized nullable copies in class fields and
value arrays are supported. Internal or non-Int32 enums remain outside executable
enum storage. None of these limits is hidden by rewriting application code.

## Evidence and review

Thirteen authored native probes match .NET at O0/O2 with ASan/UBSan and collection
before allocations: 49 input pairs and three repetitions yield 3,822 comparisons,
plus frame-limit and collection cleanup checks. They cover signed/unsigned/narrow
integers, Boolean/Char, public Int32 enums, Guid, application IDs, closed generic
containers, copied returns, heap fields, arrays, reference-bearing payloads,
empty/present/default values, fallback argument evaluation and the Value fault.
They also cover ordinary boxes containing nullable references and a same-named
application value that must not acquire Nullable semantics.

Exact .NET Standard 2.1 member binding, invalid type arguments, raw unbox, direct
boxing, unsupported members and isolated nullable key-null selection have reduced
rejection tests. Generic constraint, owned-core, tuple, runtime identity and
embedding regressions pass. Generated storage uses the existing rooted copy/
return path; no new pointer conversion, allocation primitive or arithmetic
emission was introduced. The public gap baseline advances its two nullable
probes to native controls and preserves the original audit observations.

Unchanged private engine/state/hash roots pass the previous missing Nullable
mapping. Their next diagnostics concern a static application-ID field, an
internal Int32 enum, and SHA-256 respectively. They are not native replay passes.
The explicit local fixture manifest review and source/input hashes remain private;
no production trust approval, hosted work or platform compatibility is implied.
