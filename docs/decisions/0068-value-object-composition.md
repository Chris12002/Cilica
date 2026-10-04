# ADR 0068: Value and class-construction IR composition

Status: accepted for authored IR validation and lowering. Recorded 2026-10-04.

`ValueObjectIrProgram` extends the explicit value/vector composition boundary with
class allocation, field loads/stores and existing scalar field borrows. It reuses
`ObjectIrRules` for constructor initialization, access and readonly rules. Static
value methods remain supported; instance methods and value/byref constructor
parameters are rejected. The public boundary stays Int32-only.

`ValueScalarLowering.LowerObjects` remaps object allocation arguments and field
operands, expands ordinary value calls/return packets, then revalidates the scalar
result. Source root planning marks both object and vector allocations. Constructors
retain their receiver root across base calls and their metadata bindings through
expansion. No constructor bypass or alternate native allocator is introduced.

Manual review checked source and target constructor verification, immediate-base
calls, readonly receiver checks, fault edges and allocation/root mapping. All 80
selected local checks pass, including reduced missing/repeated-base and invalid
readonly/allocation cases. Earlier value/vector profiles still reject class
allocation. CIL discovery and live object-reference stress are the next slice;
this authored-IR checkpoint does not establish them.

ADR 0069 supplies that CIL/native evidence and tightens the constructor boundary
to scalar/reference operations throughout the body, including locals and results.
