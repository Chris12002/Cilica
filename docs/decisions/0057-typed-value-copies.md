# ADR 0057: Typed inline copies and root planning

Status: Accepted for detached IR validation and root plans. Recorded 2026-10-04.

Extend the shared IR with an explicit Value kind and three pure expressions:
default value, field extraction and field replacement in a copied value.
Replacement produces a new value; it does not mutate its operand or alias a
local. Reference fields preserve object identity. A struct can contain another
struct, but assignments/calls/joins require the exact declared value identity.
There is no implicit boxing, inheritance conversion, arithmetic, reference
comparison or truth test on a struct.

`ValueIrProgram` uses the existing ordered temporary, local, block-edge, direct
call and return verifier. `ValueIrValidator` adds layout/field checks and complete
used-type closure. A shared reference-catalog usage check serves both reference
and value profiles. The existing reference, primitive and native object profiles
still reject Value storage. This is detached flow, not CIL declaration/access or
readonly-field authorization.

`ValueRootAssignment` plans a distinct phase A root for every reference leaf in
every parameter, local, temporary and block input. Each root retains its nested
FieldDef path. Copies across joins have separate roots. Calls remain explicit
safepoints and fault edges. Pure copying/defaulting/extraction allocate nothing.
The existing 1,024-root method bound applies after expansion; an additional
65,536 scalar-slot program bound prevents small typed IR from demanding unbounded
expanded execution storage. Native emission must separately bound copying work,
argument packets and target byte sizes.

Review checks exact value identity, ordered operands, pure/fault-edge separation,
null reference field stores, no accidental boxing, closure completeness, root
paths and exact/excess root/scalar limits. Managed storage locations, lifetime
checks, native value lowering and CIL value translation remain subsequent slices.
This records no executable struct or managed interior-pointer capability.
