# ADR AC0133: Nominal Int32 enum vector storage

Recorded 2026-10-07 for AC7a.5.

After committed AC7c.2 b1443cd, the frozen engine reaches ShieldZoneState's enum
array after 288,533 metadata work units. StateLookup remains eight methods/9,923
units. All six frozen input hashes match; no native engine execution is claimed.

Use a distinct Enum32 array element kind carrying the exact nominal enum identity.
Retain bounded enum declaration and lexical facts in the object catalog. Discovery
requires the existing exact nongeneric Int32-enum metadata shape. The detached
logical/scalar validators check identity, layout, lexical visibility, duplicates,
collisions, profile, bounds and agreement with any included scalar-box facts.
Separate descriptors preserve GetType identity and Array.Empty<T> cache identity.
Native storage uses the existing four-byte integer array path, without boxing or
reference scanning. The existing constant-data pattern supports Enum32 at width
four. No unsafe reinterpretation or native integer pointer punning is introduced.

A separate .NET 10.0.11 oracle confirms Int32-enum, Int32 and UInt32 arrays allow
compatible enum/integer array and generic-interface views while retaining their
actual runtime type. Generic enumerator Current boxes the interface's declared
element; nongeneric enumeration boxes the actual array element. Therefore this
slice admits compatible Int32/enum array casts and integer-interface type tests,
but retains explicit errors for actual enum-array interface dispatch and generic
enum contracts. UInt32 vectors remain unsupported. Do not remove those guards
without typed enumeration, boxing and alias evidence. Enum element borrows and
non-Int32 enum vectors also remain unsupported.

Eleven native probes cover direct/heap/static storage, constant data, arbitrary
bit patterns, public/internal enums, shared aliases, null/bounds faults, invalid
cast text, nominal GetType identity and exact empty-array caching. Six malformed
catalog mutations are rejected by both IR stages. Four unimplemented interface or
width probes remain diagnostic. O0/O2 differential checks use forced GC, ASan/UBSan,
exact static roots, unchanged frame quotas and unchanged subprocess budgets.

Review covers lexical access, actual descriptor identity versus compatible views,
four-byte loads/stores, constant endianness, precise tracing and type-work bounds.
Integer CIL v53, object IR v40, object layout v25 and native v67 record the change.
Production discovery limits and library grants are unchanged. Evidence:
artifacts/alchemy-enum-array-audit. Full engine/replay and enum interfaces remain open.

All 76 selected checks pass. Eleven probes plus the advanced public enum-array
control supply 3,528 repeated native/.NET comparisons. The baseline now has 45
native controls and two diagnostic gaps. Review also added a detached-plan guard
and a forged enum receiver regression for the deferred interface-dispatch boundary.
