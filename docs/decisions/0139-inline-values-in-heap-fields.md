# ADR 0139: Inline values in heap fields

Status: Accepted

## Decision

The integer-object CIL profile admits closed inline structs in class fields.
Discover the combined class/value graph to a bounded fixed point, reading each
metadata snapshot once. Inline containment must remain finite; cycles through
ordinary object references are allowed. Bare object catalogs still reject logical
value storage unless composed with the validated value catalog.

Expand each logical heap field into scalar leaves using the existing value plan.
Retain its image, closed declaring owner and FieldDef token, adding a contiguous
component number for each derived leaf. Native symbols and checked field slots
use these identities. Embedded references become ordinary traced heap fields;
there is no extra allocation or observable box. Whole-value reads are snapshots,
and stores consume those snapshots before writing any leaf. Borrowed leaves retain
the owning heap object through the existing checked location carriers.

An empty inline field has one private zero slot so even discarded reads, writes
and addresses check the receiver. Logical catalogs reject expanded identities;
scalar boxed catalogs must match the independently derived heap expansion.
Interface plans retain the same transformed catalog. Existing aggregate field,
type, native layout and frame limits still apply.

Object layout advances to v6, value layout and scalar-copy profiles to v2,
location scalar lowering to v3, scalar boxed IR to v2 and native emission to v23.
These are private compiler/runtime profiles, not a public layout ABI.

## Evidence, review and remaining boundary

Five probes provide 1,470 repeated O0/O2 sanitizer comparisons covering nested
Int64/reference values, generic holders, inheritance, Guid fields, copies,
readonly fields, aliases, empty values, null receivers, reference cycles,
interface dispatch, collection before allocations and ownership cleanup.
The full local solution builds. The related layout/location/interface suites
pass 213 checks; the final heap/Guid/layout/boxing run passes 62 (overlapping).
Forged components and missing inline targets are rejected.

Review found a compound-assignment exception-order difference in the local .NET
oracle: `holder.Value = Make(input / mode)` with a null holder can report the
null destination before the division although the decoded IL places stfld last.
Direct struct-return call results stored into heap fields now fail explicitly
until destination evaluation is lowered. The reduced CompoundStore fixture is
retained as a rejection regression. Separate explicit byref assignments test
unambiguous destination-first semantics. No application rules were changed.

Review also checked expansion bounds, zero-sized receiver checks, inherited slot
identity, nested readonly views and roots across allocation. No remaining issue
was found in the admitted subset. Dictionary equality/storage, array helpers and
the complete native kernel transcript remain milestone gates.
