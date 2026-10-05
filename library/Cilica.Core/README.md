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
  --source library/Cilica.Core/TrialDictionary.cs \
  --source library/Cilica.Library/IntegerFormatting.cs \
  --intrinsic-requests library/Cilica.Core/intrinsic-requests.json \
  --output-directory artifacts/library-build/core
```

The output directory must be new. No platform references are passed. The request
file describes 71 exact string/exception/provider/numeric capabilities for review; it does not
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

`TrialDictionary.cs` adapts pinned dictionary storage/prime algorithms under a
Cilica.Library name. Five direct-core probes provide 1,470 repeated native
comparisons, including resize/collisions and mutable key callback semantics.
Private exact key grants select integer and IEquatable struct bodies. The public
System.Dictionary mapping still needs collection/array interfaces; reference keys,
enumeration, removal and other omitted members remain unsupported (ADR 0141).

This is a bounded core trial. General formatting/culture/provider members,
StringBuilder, library collection implementations, full reflection and general CoreLib compatibility
remain unimplemented. The image is metadata input to Cilica, never loaded into
the host CLR. See ADRs 0131–0138 for scope and review evidence.
