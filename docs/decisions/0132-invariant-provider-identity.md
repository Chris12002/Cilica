# ADR 0132: Invariant provider identity

Status: Accepted

## Decision

Add an explicit selected CultureInfo.InvariantCulture operation to the integer-
object profile. Resolve the exact nongeneric public declaration in the selected
core image; mapped compilation requires both CultureInfoLayout and InvariantCulture
capabilities against the independently approved build. Normalize its native layout
to a fieldless Object-derived shell, preserving declared interface relationships.
Other CultureInfo members and constructors remain diagnostic.

Allocate the provider lazily once per runtime instance and publish it without an
intervening allocation. Its cache is a permanent root visited by collection and
ownership inspection. Allocation uses ordinary heap bounds and fault routing;
cache hits preserve identity without allocating. Instances never share the object.
This changes the private native runtime profile to object-frames-native-v20.

Detached IR checks the result type, selected core image, exact declaration name,
fieldless public layout, direct Object ancestry, unique identity, absence of
ordinary bound methods, explicit integer profile and fault edge. The source
IFormatProvider declaration only supplies the tested type relationship; GetFormat
and general provider dispatch are not implemented.

## Evidence and review

Four mapped core probes match .NET in 1,176 repeated O0/O2 ASan/UBSan results,
including 294 new identity results. Collection before allocations and after
execution retains the provider. Native cache tests cover independent instances,
invalid/foreign pointers and object-budget failure/recovery. Missing individual
grants, unsupported members and malformed detached IR fail explicitly.

Review found the added root exceeded the unoptimized sanitizer stack-frame budget;
a small cached-root helper retains the existing 2 KiB bound. Platform CoreLib
CultureInfo also implements an interface shared by String: the pre-existing
unsupported String/interface gate remains, rather than erasing that relationship.
The tested composition uses the actual owned core build. No numeric formatting,
general globalization, hosted validation or production approval is claimed.
