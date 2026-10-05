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
  --source library/Cilica.Library/IntegerFormatting.cs \
  --intrinsic-requests library/Cilica.Core/intrinsic-requests.json \
  --output-directory artifacts/library-build/core
```

The output directory must be new. No platform references are passed. The request
file describes 88 exact string/exception/provider/numeric/array/key capabilities for review; it does not
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

`DelegateContracts.cs` adds Action<T1,T2>. Exact Action<int,int> calls use the
checked single-target delegate path. Closed application delegates also retain
distinct specialization identities; reference/wide callback signatures and
delegate variance conversions remain unsupported (ADR 0136).

`Guid.cs` supplies numeric constructors, Empty, typed equality/hash/comparison
and D/N/B/P/X formatting, adapted from the pinned source with four Int32 words.
The local suite covers 14,400 repeated native comparisons, including every output
code unit and null/default/empty exception messages. Parsing, random/byte/span
APIs and boxed Guid type tests remain unsupported (ADR 0138).

`Dictionary.cs` maps the bounded public Dictionary constructor, Count, TryAdd,
TryGetValue, ContainsKey and indexer setter, using the previously tested storage
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
views for Int32/reference elements. ArrayInterface also supplies exact granted
indexed writes and fixed-size Add faults. Enumeration/search/copy/removal and
broader collection APIs remain diagnostic.


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
