# ADR 0126: Exact constrained value targets

Status: Accepted

## Decision

Add a metadata-only `ResolveConstrainedValueTarget` API for sealed ordinary values
directly derived from the selected ValueType. Authenticate the declaration and
closed receiver, using shared constraint, interface-closure and MethodImpl
validation. Select an exact explicit/implicit interface body or a matching
Object/ValueType override declared on that value. Retain the declaration,
constructed receiver, implementation and explicit-mapping fact.

Values have no derived class slots. Resolve their direct body without routing
them through the concrete-class hierarchy walker or creating a boxed receiver.
An Object override must reuse its slot; a same-signature newslot is not sufficient.
Missing bodies that require boxing or runtime/default-interface fallback remain
diagnostic. Class, enum, stack-only, variant-only and unsupported generic/default
interface targets remain outside this API's admitted cases. IL verification and
native execution are separate steps.

## Evidence and review

All 79 distinct focused checks pass, including twelve new target checks. Closed
Int32/Int64/reference interface mappings and a real IEquatable mapping match
.NET's maps; explicit private bodies and Object overrides retain exact tokens and
owners. Forged signatures, wrong contracts, newslot impersonation, fallback
receivers and exhausted metadata budgets are rejected. Existing class/interface
target selection and stack-only rejection tests remain green. Review found no
remaining actionable issue. Executable constrained CIL integration follows.
