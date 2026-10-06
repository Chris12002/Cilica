# ADR AC0058: Abstract class slots in closed metadata resolution

Accepted 2026-10-07 for AC8.12.

Ordinal comparer implementation exposes a missing compiler layer: StringComparer
uses abstract methods. First make the metadata resolver distinguish declarations
from implementations. Do not give an abstract method an invented managed body.

The explicit closed class resolver admits public nongeneric virtual abstract
methods on abstract owners, including reuse-slot re-abstraction. They must have
no RVA/body, exact managed IL implementation flags and no Final flag. Ordinary
slot identity/name/signature checks, final override checks, owner validation,
closed argument validation and metadata budgets still apply. A concrete receiver
whose nearest selected slot remains abstract is rejected. A new slot cannot fill
an older abstract slot accidentally. The legacy nongeneric resolver stays closed
to abstract hierarchies.

An implicit interface mapping may name such an abstract class slot in the closed
profile. It still follows class slot resolution to a concrete receiver's actual
override. Explicit private-final mappings and other interface restrictions are
unchanged. Closed class/interface target profiles advance from v2 to v3.

Sixty-one focused metadata checks pass, including exact budget exhaustion,
abstract declaration receivers, concrete overrides, re-abstraction, missing
implementations, hidden new slots, invalid owner/body/runtime/final flags and
interface-to-class mapping. Initial authored abstract fixtures used the test
builder's default Runtime flag; the builder now permits an explicit implementation
flag and Runtime remains a negative regression. A separate initial interface
failure identified its concrete-only name-mapping gate; that gate now authenticates
abstract mappings through the same class declaration validator.

Review checked that no successful concrete selection can return an abstract body,
metadata budgets remain unchanged, and malformed abstract declarations fail before
selection. Logs are under artifacts/alchemy-abstract-slots. This slice provides no
new native execution capability. Next add explicit bodyless class declarations to
logical/scalar IR, preserving complete dispatch maps and rejecting direct calls,
abstract allocations and declaration-as-body forgeries. The pending ordinal
comparer prototype is not yet an implemented compatibility claim.
