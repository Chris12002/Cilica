# ADR 0180: Bounded runtime type identity

Accepted 2026-10-06 for the integer managed-object profile.

## Context and decision

The unchanged Alchemy saved-game engine probe first required System.Type for
command dispatch. Implement the reusable identity operations in the owned core
and compiler; keep game rules, JSON decoding and replay adapters outside them.

Represent each closed metadata identity with one lazily allocated, fieldless
RuntimeTypeObject per runtime instance. Type and MemberInfo retain their abstract
reference-contract hierarchy. Explicit six-capability admission validates their
layouts, the traced single-field RuntimeTypeHandle layout, Object.GetType and two
private hash helpers. RuntimeTypeObject is private and sealed; application-defined
Type implementations are not admitted by this profile.

Lower ldtoken to an explicit canonical Type operation and a normal inline handle
containing that reference. Do not fuse an adjacent GetTypeFromHandle call: handles
can be copied, returned and traced through existing value lowering. GetType uses
the actual object descriptor, including array element specialization and closed
generic identity. Type operators, Equals, handle wrappers and dictionary key
helpers remain ordinary managed code. Default handles resolve to null.

The compiler emits a bounded identity table from the validated closure. Each
instance allocates its pointer cache on first use, accounts for it in heap bytes,
and traces cached Type objects as permanent roots. Destruction releases both the
cache and objects. Allocation/fault paths use existing rooted frames and pending
exceptions. No pointer or host reflection object represents a public Type handle.
Type hash values identify cache slots; ordinary class identity hashing is stable
under the non-moving collector. Neither promises numerical agreement with .NET,
stability across instances/builds, or a persistent serialization identifier.

Closed integer-object catalogs permit abstract class ancestry so the selected
Type hierarchy and normal inherited virtual bodies can participate in dispatch.
Abstract classes cannot be allocated and abstract method bodies remain unsupported.
Object Equals/GetHashCode supply ordinary class fallback bodies. Their virtual
calls require a statically known ordinary class receiver: Object-typed aliases,
strings, arrays, delegates and boxes must not silently use incomplete class maps.
Boxed class/interface uses also retain bounded confinement checks. Default value
structural equality/hashing remains diagnostic.

## Alternatives and limits

A fresh Type allocation on every query breaks reference identity and dictionary
lookup. A process-global cache breaks instance isolation and lifetime accounting.
Using native descriptor addresses as public handle values would bypass the traced
managed-value model. The selected cache keeps these invariants explicit.

Reference/interface literals, closed reference generic literals and admitted
vectors are supported. Value-type, open-generic, method and field tokens, general
reflection, dynamic loading and arbitrary Object-slot dispatch remain diagnostics.
A boxed value's GetType identity can be inspected without enabling typeof(value)
or structural boxed dispatch. Public object transfer through the embedding ABI
and untested platforms are not implied.

## Evidence and review

RuntimeTypeIdentityTests compares four authored fixtures with .NET at O0/O2:
1,176 repeated results, forced collection before allocation, ASan/UBSan, exact and
short execution-frame bounds, inheritance, generic specialization, handles, null
receivers, strings, arrays, boxed identity and Dictionary<Type,int>. Separate native
checks exercise cache-only/one-object heap limits, collection and corrupt foreign
cache entries. Negative cases cover six missing grants, private helper access,
abstract allocation, unsupported tokens/reflection/default value equality, special
Object-slot dispatch and forged detached targets/implementations/fault edges.

Review found and corrected two consequences of enabling Object slots: boxed class
dispatch could escape existing interface-only checks, and String.GetHashCode could
compile against a class map lacking the String descriptor. Both now fail at build
time with reduced regressions. Review also checked cache rooting/publication,
allocation failure accounting, C++ defined conversions, ownership, bounded native
frames and preservation of existing intrinsic capability numbers.

The unchanged private Alchemy metadata probe passes its previous System.Type
mapping blocker and next reports unmapped System.Nullable<T> (CIL3015). The local
core manifest and probe evidence are in ignored artifacts/alchemy-runtime-types;
this fixture approval does not alter a production trust baseline. Full saved-game
native replay remains unverified. No hosted jobs, services, training or pushes ran.

Final local validation passed all 241 targeted tests spanning identity, class
resolution/layout, boxing, core admission, dictionaries, arrays, collection,
virtual calls and both embedding ABI versions. The Release solution build has
zero warnings/errors. The complete expensive suite and private kernel transcript
were not rerun; this follow-on has its own reduced native differential evidence.
