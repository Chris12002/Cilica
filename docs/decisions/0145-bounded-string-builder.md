# ADR 0145: Bounded StringBuilder and immutable UTF-16 construction

Status: Accepted for default construction, char/string append and snapshots

## Decision

Adapt the pinned runtime's chunked StringBuilder append and snapshot algorithms
as ordinary managed code in the owned core. Int32 vectors temporarily hold
zero-extended UTF-16 units; narrow vectors are not implied. The selected public
mapping retains the serialization interface, but serialization, setters and
other unimplemented members fail during compilation.

One exact private InternalCall grant constructs an immutable string from the low
16 bits of an Int32 vector. The compiler authenticates its declaration, selected
image, private caller and grant. Typed IR validates its source and result and
requires an exception edge. Root assignment treats it as an allocation point.
Native code validates the source, copies it while rooted across non-moving GC,
returns canonical empty text, and publishes a fresh nonempty string. It never
aliases mutable vector storage. Profiles advance to typed-reference-ir-v6 and
object-frames-native-v26.

## Evidence and review

Reduced probes compare 4,362 native results with .NET across O0/O2, repeated calls
and allocation-time collection. They cover nulls, chunk growth beyond 8,000 units,
snapshot independence, identity, embedded NUL, unpaired surrogates, exact indexed
code units, truncation and bounds. Negative checks cover missing grants, external
private calls, unsupported setters and malformed detached IR.

Review checked chunk allocation and mutation order, checked/unchecked arithmetic,
source rooting, immutable publication and native ownership. Integration exposed
a pre-existing fixed 13-entry exception-resource cache limit after System and
Arithmetic resources were added. Both allocation and tracing now use the emitted
resource count. A sanitizer regression caches all 15 resources, collects them and
rejects a corrupt cycle at O0/O2.

The real kernel progresses to Array.Empty<T>; this slice does not establish
native kernel equivalence, full StringBuilder support or a public embedding ABI.

The full solution build and 44 composed local checks pass, including native string,
collection and exception-resource tests. No hosted work ran.
