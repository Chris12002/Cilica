# Bounded Cilica core library

`CoreTypes.cs` contains Cilica-owned runtime declarations, compiled without a
platform CoreLib. Primitive declarations support the compiler's predefined type
identities; their presence does not implement every primitive operation. Strings
and the selected exception family use Cilica's existing native payloads and
tracing. Unsupported runtime members remain body/operation diagnostics rather
than returning placeholder values.

Build with the local evidence producer:

```sh
dotnet tools/Cilica.LibraryBuild/bin/Debug/net10.0/Cilica.LibraryBuild.dll \
  --compiler-directory /path/to/dotnet/sdk/10.0.400/Roslyn/bincore \
  --profile core --assembly-name Cilica.Core \
  --source library/Cilica.Core/CoreTypes.cs \
  --source library/Cilica.Core/RuntimeTypes.cs \
  --source library/Cilica.Core/Nullable.cs \
  --source library/Cilica.Core/IntegerText.cs \
  --source library/Cilica.Core/CollectionContracts.cs \
  --source library/Cilica.Core/DelegateContracts.cs \
  --source library/Cilica.Core/Guid.cs \
  --source library/Cilica.Core/KeyOperations.cs \
  --source library/Cilica.Core/Dictionary.cs \
  --source library/Cilica.Core/DictionaryContracts.cs \
  --source library/Cilica.Core/ArrayInterface.cs \
  --source library/Cilica.Core/StringBuilder.cs \
  --source library/Cilica.Core/List.cs \
  --source library/Cilica.Core/ReadOnlyCollection.cs \
  --source library/Cilica.Core/ValueTuple.cs \
  --source library/Cilica.Core/StringKeyOperations.cs \
  --source library/Cilica.Core/PairHash.cs \
  --source library/Cilica.Core/BoxedFormatting.cs \
  --source library/Cilica.Core/StringComparison.cs \
  --source library/Cilica.Core/StringWhitespace.cs \
  --source library/Cilica.Core/Math.cs \
  --source library/Cilica.Library/IntegerFormatting.cs \
  --intrinsic-requests library/Cilica.Core/intrinsic-requests.json \
  --output-directory artifacts/library-build/core
```

The output directory must be new. No platform references are passed. The request
file describes 103 exact string/exception/provider/numeric/array/key/type capabilities for review; it does not
approve them. The build records actual compiler/source/options hashes and emitted
declarations. Admission still requires an independently approved manifest digest.
No build output is automatically added to the trusted baseline.

`CoreLibraryBuildTests` builds this source and maps its admitted declarations
against the pinned .NET Standard 2.1 reference image. Eight native probes match
.NET at O0/O2 with ASan/UBSan, collection before allocations, repeated calls,
frame exhaustion and ownership cleanup. They cover UTF-16 strings, exception
messages/parameter names, stable invariant-provider identity, four integer hashes, invariant numeric APIs, heap receivers and the decimal helper.
NumericFormattingTests adds complete text and cache-boundary comparisons. Missing
approval and unsupported string hashing fail explicitly.

The four integer `ToString(IFormatProvider)` methods require directly proven
`CultureInfo.InvariantCulture` at each call. Their [0, 300) cache matches the
pinned source's shared numeric identity without aliasing literals. The private
cache helpers are callable only by the selected core image and need exact grants.

`CollectionContracts.cs` supplies read-only collection and enumeration interfaces
with their pinned inheritance and variance. Six native probes exercise exact
mapped calls on application collections, including Int64 elements and reference
enumeration. This does not implement List storage, array interface helpers or
variance-only dispatch (ADR 0135).

`DelegateContracts.cs` supplies Action<T>, Action<T1,T2>, Func<T,TResult>,
Func<T1,T2,TResult>, Predicate<T> and Comparison<T>. Exact Int32/reference
parameters and Int32/Boolean/reference/void results use the checked single-target
path in the integer-object profile (ADRs AC0007/AC0008). Declared variance flags
match the pinned reference; variance conversions and multicast invocation remain
unsupported. Boolean parameters, other narrow/wide/value/byref signatures and
additional delegate arities remain outside this bound. No new intrinsic grants.

`Guid.cs` supplies numeric constructors, Empty, typed equality/hash/comparison
and D/N/B/P/X formatting, adapted from the pinned source with four Int32 words.
The local suite covers 14,400 repeated native comparisons, including every output
code unit and null/default/empty exception messages. Parsing, random/byte/span
APIs remain unsupported (ADR 0138). Boxed Guid type tests are admitted by AC1b.3
([ADR AC0005](../../docs/decisions/AC0005-boxed-type-tests.md)).

`Dictionary.cs` maps the bounded public Dictionary constructor, Count, TryAdd,
TryGetValue, ContainsKey and indexer setter, plus AC3.3 Remove (with/without out
value), Clear and free-slot reuse, using the previously tested storage
algorithms. DictionaryContracts.cs preserves generic, nongeneric and serialization
interface declarations. Unimplemented members reach UnsupportedLibraryMember and
fail compilation; no default-valued or throwing runtime substitute is provided.
ArrayInterface.cs supplies separately granted managed Count/index readers, collection flags and SyncRoot.
The complete kernel/native equivalence gate remains pending.


This is a bounded core trial. General formatting/culture/provider members,
broader StringBuilder and collection members, full reflection and general CoreLib compatibility
remain unimplemented. The image is metadata input to Cilica, never loaded into
the host CLR. See ADRs 0131–0147 for scope and review evidence.

StringBuilder.cs adapts pinned chunk growth and append/snapshot behavior. It uses
private Int32 buffers for UTF-16, with one exactly granted string construction
helper. Default construction, Append(string/char), getters and ToString are covered;
other members remain build errors. No application canonicalization rules live here.

Array.Empty<T> uses an exact grant and per-instance descriptor-keyed cache for
Int32/reference vectors. Other element storage remains diagnostic.

List.cs and ReadOnlyCollection.cs adapt pinned managed storage/growth and live
views. AC3.1 adds concrete Insert, RemoveAt, Clear and ToArray for Int32, admitted
references and supported inline values, including traced reference-bearing values.
Empty snapshots share per closed type and remain distinct from Array.Empty<T>.
ArrayInterface supplies indexed writes and fixed-size Add/Insert/RemoveAt faults.
AC3.2 separately grants generic read-only Clear and nongeneric element-resetting
Clear; bounded list/read-only interface mutation compositions now execute.
AC3.4 adds Contains/IndexOf/Remove for admitted equality types, including live
read-only search. Value equality borrows the stored element; integer/reference
search uses copied receivers. Nullable/arbitrary-reference equality, range/copy/
bulk APIs and enumeration remain diagnostic (ADRs AC0009/AC0010/AC0013).


The exact TypeInitializationException(String, Exception), TypeName and base
Exception.InnerException members use granted, traced native payloads. Construction
eagerly formats the pinned invariant message; original names and inner exceptions
retain identity. Null names read as the canonical empty string. Pinned original
sources and hashes are recorded in provenance.json. Type initializer execution,
serialization, HResult and other exception constructor overloads remain separate
work (ADR 0150).

ValueTuple.cs supplies bounded two-field tuple construction, typed equality and
hashing. Equality copies component receivers; hashing targets the fields directly.
String keys use private managed ordinal equality and UTF-16 hashing. Hash values
are implementation-specific. Object equality, comparison, structural APIs, tuple
indexing and formatting remain diagnostic. Pinned sources and the hash combiner’s
additional BSD notice are retained in the provenance record.

BoxedFormatting.cs supplies the parsed single-argument String.Format path for a
directly boxed named value with an ordinary ToString override and no IFormattable
contract. Two exact grants retain the box and borrow its payload for each callback.
Alignment, escapes, ignored format text and diagnostic ordering are managed code.
Other overloads, providers, primitive/reference arguments and aliases are diagnostic.

StringComparison.cs preserves the pinned enum declaration. The exact granted
three-argument String.Equals overload requires a directly proven Ordinal constant
and reuses UTF-16 ordinal equality. Other modes and mode aliases remain diagnostic.

StringWhitespace.cs implements IsNullOrWhiteSpace as ordinary managed code with
pinned BMP whitespace ranges. Exhaustive UTF-16 and mixed-string differential tests
cover the implementation. No general Unicode classification API is supplied.

The exact readonly String.Empty field has its own field-token/signature grant.
Reads use the canonical per-instance empty literal; writes, explicit storage and
initializers are rejected. Identity with empty literals/Concat/StringBuilder output
is tested at O0/O2 with forced collection and sanitizers.

RuntimeTypes.cs adds bounded runtime identity: closed reference-type/vector
`typeof`, `Object.GetType`, copied `RuntimeTypeHandle` values, `TypeHandle`,
`GetTypeFromHandle`, Type equality/hash and the supported Dictionary operations
with Type keys. Each runtime instance lazily roots one canonical Type object per
closed identity. Type hashes are implementation-specific. Six exact grants bind
the layouts and native helpers; ordinary equality/handle/key wrappers stay managed.
General reflection, value/open/method/field tokens and arbitrary Object-slot
virtual calls remain diagnostic. See ADR AC0001 for receiver restrictions and evidence.


`Nullable.cs` adds closed nullable storage and the constructor, HasValue, Value
and both GetValueOrDefault overloads as owned managed code. Add the exact
``System.Nullable`1`` reference mapping when composing a core binding plan; it
requires no new intrinsic grant. Integer/Boolean/Char, public Int32 enum, Guid,
application-value and reference-bearing payloads have native differential evidence.
Ordinary value copying/return/rooting rules apply, including materialized heap
fields and value arrays. The same file now supplies managed conversion operators
and compiler-selected NullableBoxing<T> helpers for named-struct box/unbox.any.
Empty boxes are null; present boxes use the underlying type's ordinary box.
AC1b.2 also admits selected integer/Boolean/Char and public Int32-enum boxes with
exact nullable identity. Add the exact Boolean, Char, SByte, Byte, Int16 and UInt16
reference mappings alongside the existing 32/64-bit mappings when composing a
binding plan. AC1b.3 adds exact boxed isinst/castclass, including nullable targets,
for these scalars and admitted named structs. Successful tests preserve the box's
identity without allocating. AC1b.4 permits independent interface operations when
all scalar box uses are proven confined. Returned/escaping boxes, including general
nullable boxing helper results, are not proven in those compositions. No new
intrinsic grants are required. Raw scalar/nullable unbox, scalar interface views,
nullable key operations and equality/hash/text members remain diagnostics. See
[ADR AC0006](../../docs/decisions/AC0006-confined-scalar-box-uses.md).

Math.cs adds signed Int32/Int64 Abs and signed/unsigned 32/64-bit Min/Max.
CoreTypes.cs adds typed CompareTo with exact IComparable<T> declarations. These
are ordinary managed bodies, with no new grants. Narrow/floating/decimal Math
and object comparison overloads remain unmapped (ADR AC0012).

StringComparison.cs also supplies CompareOrdinal and IsNullOrEmpty as managed
code, preserving pinned UTF-16 comparison results (ADR AC0014). No new grants.

Numeric StringBuilder.Append(int/uint/long/ulong) uses fixed invariant culture
(AC7b.2); hosts comparing with .NET must configure the same culture. Ambient
culture, floating-point and object append are not implemented.

Array.AsReadOnly uses existing ReadOnlyCollection views and private per-type
empty caches on each runtime instance (AC3.5). No new intrinsic grants.

Concrete List<T>.GetEnumerator/Enumerator needs the exact nested type mapping
``System.Collections.Generic.List`1/Enumerator`` alongside ``List`1``. AC4.1 implements
Current/MoveNext/Dispose with mutation checks; interface factories, boxed method
dispatch and nongeneric Current remain diagnostic.

Dictionary Keys/Values require explicit nested KeyCollection/ValueCollection
mappings. AC4.2 supplies cached live views, read-only faults and bounded lookup.
ContainsValue uses copied receivers; nullable/unknown reference equality, view
copying and enumeration remain diagnostics. No new grants.

AC4.3 concrete dictionary enumeration requires the exact nested
``System.Collections.Generic.Dictionary`2/Enumerator`` mapping. New insertion
invalidates iteration; clear, remove and overwrite do not. Interface factories
and boxed nongeneric enumeration members remain diagnostic.

AC4.4 also maps the nested Enumerator inside each dictionary KeyCollection and
ValueCollection. Concrete MoveNext/Current/Dispose retain only the selected
current key or value; boxed interface factories remain unsupported.
