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
  --source library/Cilica.Core/EnumQueries.cs \
  --source library/Cilica.Core/Environment.cs \
  --source library/Cilica.Core/Nullable.cs \
  --source library/Cilica.Core/IntegerText.cs \
  --source library/Cilica.Core/CollectionContracts.cs \
  --source library/Cilica.Core/DelegateContracts.cs \
  --source library/Cilica.Core/Guid.cs \
  --source library/Cilica.Core/GuidParsing.cs \
  --source library/Cilica.Core/KeyOperations.cs \
  --source library/Cilica.Core/Dictionary.cs \
  --source library/Cilica.Core/HashSet.cs \
  --source library/Cilica.Core/DictionaryContracts.cs \
  --source library/Cilica.Core/ArrayInterface.cs \
  --source library/Cilica.Core/ArrayEnumerator.cs \
  --source library/Cilica.Core/Enumerable.cs \
  --source library/Cilica.Core/EnumerableContains.cs \
  --source library/Cilica.Core/WhereIterator.cs \
  --source library/Cilica.Core/SelectIterator.cs \
  --source library/Cilica.Core/DistinctIterator.cs \
  --source library/Cilica.Core/ConcatIterator.cs \
  --source library/Cilica.Core/OrderedIterator.cs \
  --source library/Cilica.Core/MapSort.cs \
  --source library/Cilica.Core/ComparisonSort.cs \
  --source library/Cilica.Core/OfTypeIterator.cs \
  --source library/Cilica.Core/StringBuilder.cs \
  --source library/Cilica.Core/StringConcatenation.cs \
  --source library/Cilica.Core/StringSubstring.cs \
  --source library/Cilica.Core/StringCharacters.cs \
  --source library/Cilica.Core/Utf8Encoding.cs \
  --source library/Cilica.Core/List.cs \
  --source library/Cilica.Core/ReadOnlyCollection.cs \
  --source library/Cilica.Core/ValueTuple.cs \
  --source library/Cilica.Core/StringKeyOperations.cs \
  --source library/Cilica.Core/PairHash.cs \
  --source library/Cilica.Core/BoxedFormatting.cs \
  --source library/Cilica.Core/StringComparison.cs \
  --source library/Cilica.Core/StringComparer.cs \
  --source library/Cilica.Core/Comparer.cs \
  --source library/Cilica.Core/StringWhitespace.cs \
  --source library/Cilica.Core/Math.cs \
  --source library/Cilica.Library/IntegerFormatting.cs \
  --intrinsic-requests library/Cilica.Core/intrinsic-requests.json \
  --output-directory artifacts/library-build/core
```

The output directory must be new. No platform references are passed. The request
file describes exact string/exception/provider/numeric/array/key/type capabilities for review; it does not
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
hashing, plus five-field construction/storage and typed equality. Equality copies
component receivers; pair hashing targets the fields directly. Five-field hashing
remains diagnostic.
String keys use private managed ordinal equality and UTF-16 hashing. Hash values
are implementation-specific. Object equality, comparison, structural APIs, tuple
indexing and formatting remain diagnostic. Pinned sources and the hash combiner’s
additional BSD notice are retained in the provenance record.

BoxedFormatting.cs supplies one- and two-object String.Format overloads for directly
boxed named values with ordinary ToString overrides and no IFormattable contract.
Each actual argument must match its same-block box witness. Separate exact overload
grants and the render grant retain each box and borrow its payload per callback.
Both entry shapes share managed alignment, escape, format-suffix and error parsing.
The private renderer receives the original string plus a UTF-16 range without a
substring allocation; ordinary non-IFormattable receivers ignore that range.
The same bridge admits Int32 enums with unique values and no custom attributes.
EnumText<T> keeps default/G/D/X/F selection, eight-digit uppercase hexadecimal,
numeric fallback and unsigned greedy F decomposition in managed code. Four exact
private grants expose checked payload bits, constant count and bounded sorted
value/name table reads. Flags attributes, duplicate-value enum aliases, other
underlying types, general IFormattable dispatch, other overloads/providers,
primitive/reference arguments and object aliases remain diagnostic. Decimal
fallback uses the existing invariant culture contract. Name construction changes
allocation counts and does not promise .NET memory thresholds.

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


### Generic array enumeration (AC4.5)

Generic array GetEnumerator now uses an exact-granted managed helper and a traced
reference enumerator. It preserves live Current, reset/disposal, mutable per-T
empty identity and widened reference-array views. Seven authored probes plus the
public array probe supply 2,352 repeated O0/O2 native/.NET comparisons; four native
GC/cache-isolation runs, exact grant/packet boundaries and 60 existing regressions
pass with sanitizers and forced collection. Interface plan v6; native v41 unchanged.
The ledger has 21 gaps and 26 controls. Boxed collection enumerators, mixed variant
class dispatch, nongeneric Current and full replay remain unfinished.


### Parameterless LINQ cardinality (AC6.1)

Enumerable.Any/Count now preserve generic/nongeneric collection shortcuts and
reference-enumerator fallback disposal. Eight authored probes plus public LinqAny
supply 6,438 repeated O0/O2 native/.NET comparisons; 35 existing regressions and
the negative overload/factory check pass with forced GC and sanitizers. The ledger
has 20 gaps and 27 controls. Predicate/deferred operators, boxed collection
factories and full replay remain unfinished. No grants or profiles changed.


### Predicate LINQ terminals (AC6.2)

Any/All/Count predicate overloads now preserve null order, live array reads,
short circuit and enumerator disposal without using collection-count shortcuts.
Twelve probes supply 5,184 repeated O0/O2 native/.NET comparisons; 42 existing and
boundary checks pass with forced GC and sanitizers. Int32/reference callbacks are
supported; value-payload delegates, boxed collection factories, deferred operators
and full replay remain unfinished. The ledger stays at 20 gaps and 27 controls;
no grants or profiles changed.


### Single-thread yield iterators (AC4.6)

The selected core now exposes a stable positive managed-thread identity scoped to
each single-threaded runtime instance, independent of the host OS thread. Unchanged
Roslyn yield state machines pass 2,646 repeated O0/O2 native/.NET comparisons,
including reuse/clones, suspended values and finally cleanup. Two GC/serialized-host
thread runs, 24 existing core/baseline checks and the negative environment/thread
check pass with sanitizers. Other threading APIs remain unsupported; no async or
multithreaded profile is claimed. The ledger has 19 gaps and 28 controls. No new
grants/native profiles; full replay remains unfinished.


### Deferred Where (AC6.3)

Where now preserves eager validation, deferred source access, iterator reuse/clones,
Current/disposal state and nested predicate order. Query Any/Count leave an active
cursor unchanged and use a nongeneric owned contract to preserve struct-array
cardinality support. Ten probes plus public Where supply 4,614 repeated O0/O2
native/.NET comparisons; four precise query/cache GC checks and 36 existing
regressions pass. The public lambda's two static cache objects are explicitly
validated. The ledger has 18 gaps and 29 controls. Indexed operators, boxed
collection factories, value predicates and full replay remain unfinished.

### Selected KeyNotFoundException (AC8.6)

The owned core admits exact granted default and string-message constructors for
System.Collections.Generic.KeyNotFoundException. Default and null messages use the
pinned Arg_KeyNotFound resource; empty and arbitrary UTF-16 messages are preserved.
Typed catches, rethrow, finally and exception identity reuse existing rooted payloads.
Inner constructors, HResult and serialization remain unsupported. Managed exception
IR and scalar exception IR advance to v4; native v42/layout v17 remain unchanged.
Four probes pass 1,176 repeated O0/O2 native/.NET comparisons with ASan/UBSan and
forced collection. Forged resource/name/ancestry packets fail at both IR stages;
removing any exact layout/constructor grant fails admission. See ADR AC0038.
Frozen EngineStep advances to List<DomainEvent>(IEnumerable<DomainEvent>) in
FinalizeAccepted IL_0005. StateLookup still hits the unchanged 256-type bound.
Full engine execution and saved-game replay remain unverified.

### Int32 actual-value range messages (AC8.7)

Selected ArgumentOutOfRangeException(String,Object,String) construction admits
null or a directly boxed selected Int32 in the same block. Both IR stages verify
the exact box and exception identity. The exception retains the original box with
precise tracing; Message appends the pinned actual-value text after its parameter
text, including Int32.MinValue. The intermediate message is rooted across allocation.
Aliases, other payloads and ActualValue retrieval remain diagnostic. This is a
prerequisite for collection-copy range checks, not general actual-value dispatch.
Native profile is v43 and both managed-exception IR stages are v5; layout v17 stays.
Four probes pass 1,176 repeated O0/O2 native/.NET comparisons; additional O0/O2
native checks cover retained-box collection, intermediate allocation failure,
origin preservation and corrupted roots. Existing confinement and public baseline
checks pass. See ADR AC0039. Full engine/state/replay execution remains unverified.

### Direct list copies (AC3.6)

List<T>.CopyTo(T[]) and CopyTo(T[],Int32) support admitted integer, reference and
inline-value elements. Source fields are read before destination checks; validation
preserves null/range/space order, exact parameter messages and unchanged list version.
A separately granted private CopyStore operation preserves actual reference cast
names and completed writes when a covariant destination fails. Ordinary array stores
retain ArrayTypeMismatchException. Native v44, object operations IR v17 and
integer-object CIL v5 record the new operation; exception IR v5/layout v17 remain.
Six probes plus one raw null-receiver probe pass 2,058 repeated O0/O2 native/oracle
or explicit CIL-fault comparisons with ASan/UBSan and forced collection. Exact
grant/private-caller checks and four packet mutations in both IR stages pass;
47 selected copy/array-mutation/baseline checks pass. See ADR AC0040.
Interface CopyTo still requires array dispatch support; section/nongeneric copies
and List(IEnumerable) remain unsupported. Full engine/state/replay is unverified.

### Generic collection copies (AC3.7)

ICollection<T>.CopyTo now supports admitted arrays and lists. A shared managed
prefix helper preserves argument validation, whole-array compatibility, partial
writes and exact cast messages. Unrelated class arrays fail even when empty;
related or interface element types permit checked element copies. A private exact
CopyCompatible grant uses a bounded static descriptor table without allocation.
Both detached stages and array helper signatures remain validated. Native v45,
object IR v18, integer-object CIL v6 and interface plan v7; layout v17 unchanged.
Nine new probes and prior direct-list/null-receiver probes pass 4,704 repeated
O0/O2 native/.NET comparisons with ASan/UBSan, forced collection and strict cleanup.
58 adjacent regression checks and the final empty-list pair pass. General
Array.Copy, section/nongeneric copying and List(IEnumerable) remain unsupported;
full Alchemy engine/state/replay remains unverified. See ADR AC0041.

### Proven collection construction (AC3.8)

List<T>(IEnumerable<T>) supports sources statically proven to implement the exact
ICollection<T> contract, including admitted arrays/lists and custom collections.
An independently granted managed bridge preserves allocation-before-validation,
Count once, zero-count CopyTo suppression, exact capacity, copied identity and
version zero. Its narrower typed parameter is checked during translation and both
detached stages. IEnumerable/IReadOnlyList aliases and general iterator sources
remain diagnostic. Integer-object CIL v7; native v45 and layout v17 unchanged.
Eight authored probes plus the advanced public control pass 2,646 repeated O0/O2
native/.NET comparisons with ASan/UBSan and forced collection. The baseline now has
30 native controls and 17 known gaps. Frozen engine discovery reaches missing
Enumerable.ToArray; state discovery still exceeds 256 identities. Full replay is
unverified. See ADR AC0042 and artifacts/alchemy-list-construction.

### Array materialization (AC6.4)

Enumerable.ToArray supports admitted array/collection copies and reference-enumerator
fallbacks. Where's exact query path preserves cursor/current and first-enumerator
identity while traversing the source. Managed traced buffers replace private pooled
scratch storage, retaining callback/disposal order and canonical empty arrays.
Twelve authored probes pass 3,528 O0/O2 native/.NET comparisons with ASan/UBSan,
forced collection and strict cleanup; 29 adjacent query/boundary checks pass.
Compiler/native profiles and grants are unchanged. Boxed collection factories,
including List in the conservative enumerable closure, remain diagnostic; direct
List.ToArray remains supported. Frozen engine discovery next requires HashSet<T>;
state still exceeds its identity quota. Full replay is unverified. See ADR AC0043.

### Default HashSet operations (AC3.9)

HashSet<T> now supports default construction, Add/Contains/Remove/Clear/Count and
TryGetValue for admitted integer/string/inline equality keys, with generic set and
collection mutation dispatch. Traced inline entries retain null handling, prime
buckets, collisions, free slots and callback ordering. A private exact reference-
shape grant folds the validated layout so removal releases references while
preserving freed scalar values observable through reentrant equality callbacks.
Integer-object CIL v8; native v45/layout v17 unchanged. Nine authored probes plus
the public control pass 2,940 O0/O2 comparisons, two native GC checks pass and 42
adjacent checks pass. The baseline has 31 controls and 16 gaps. Comparers,
enumeration, copying, capacity/enumerable constructors and set algebra remain
unsupported. Frozen engine discovery next needs String.Concat(string[]); state
still exceeds its identity quota. Full replay is unverified. See ADR AC0044.

### String-array concatenation (AC7b.5)

String.Concat(string[]) preserves null faults, canonical empty strings, single-
element identity, fresh multi-element results and exact UTF-16. Managed length
checking and copying reuse the existing private UTF-16 construction grant, with an
extra bounded temporary vector. Six ordinary probes and a bounded oversized-total
probe pass 1,770 repeated O0/O2 native/.NET comparisons with ASan/UBSan and forced
collection. 34 adjacent checks and the corrected lexical-boundary assertion pass.
Profiles/grants are unchanged. Object-array/enumerable and three/four-string
concatenation remain unsupported; concurrent mutation is not validated. Frozen
engine discovery next needs predicate FirstOrDefault; state remains over its type
quota. Full replay is unverified. See ADR AC0045.

### Predicate first-element queries (AC6.5)

Predicate First/FirstOrDefault share managed search logic with source-before-predicate
null checks, loaded-element return, array traversal and reference-enumerator cleanup.
An internal Int32 found flag preserves matching zero/null without requiring narrow
by-reference support. Eleven probes pass 3,234 repeated O0/O2 native/.NET comparisons
with ASan/UBSan, forced collection and strict cleanup; 30 adjacent LINQ tests pass.
Nonpredicate overloads, boxed List factories and value-payload predicates remain
diagnostic. Profiles and grants are unchanged. Frozen engine discovery next reaches
Func<GameState,PlayerId,bool> at PrepareForfeit IL_0395; state still exceeds its type
quota. Discovery progress is not engine execution or saved-game replay. See ADR AC0046.

### Inline-value delegate arguments (AC2.3)

Single-target delegates now accept admitted named inline structs by value, including
nested/empty values, Int64 fields and traced references. Logical signatures retain
exact nominal types; scalar lowering expands both arguments and declarations through
one layout plan. Detached scalar validation checks the preserved logical catalog.
Existing parameter/root/frame quotas stay fixed. Fifteen probes pass 4,410 repeated
O0/O2 native/.NET comparisons under ASan/UBSan and forced collection; compiler-cached
predicate objects use the existing stable persistent-root checks. 76 adjacent and
boundary checks pass, including malformed packets and expanded signature limits.
Delegate IR/scalar profiles are v3, integer-object CIL v9, object IR v19, object
layout v18 and native v46; physical delegate storage is unchanged. Direct wide/narrow
or enum parameters, struct results, byrefs and multicast remain unsupported. Frozen
engine discovery next rejects internal IEffectScope metadata while resolving
GameStateScope.set_CurrentRound; state still exceeds 256 identities. Full replay
remains unfinished. See ADR AC0047.

### Top-level internal interface dispatch (AC8.8)

The explicit closed/integer-object profiles admit top-level internal abstract
interfaces with public instance members. Catalog/declaration visibility stays
consistent; calls and type tests require the caller's image to match an internal
contract at both logical and scalar stages. Public contracts retain external use.
Seven probes pass 2,058 O0/O2 native/.NET comparisons, including explicit/inherited
mappings, class slots, generic value payloads, nulls and interface casts/arrays.
119 selected checks pass initially; the corrected detached assertion and foreign
CIL check pass separately. Exact metadata work exhaustion and the old public-only
resolver boundary remain tested. Profiles advance to object layout v19, object IR
v20, interface plan v8, integer-object CIL v10 and native v47; physical storage is
unchanged. Closed interface/class/constrained metadata targets are v2. Nonpublic
nested contracts, friend assemblies, default/static interface bodies and cross-image
internal calls remain unsupported. Frozen engine discovery next needs protected
DomainEvent base construction; state remains over its type quota. See ADR AC0048.

### Protected immediate-base construction (AC8.9)

Protected constructors are admitted only through the existing immediate-base
initialization path on the original this receiver. Family/FamORAssem follow the
validated base relation; FamANDAssem also requires the same image. Owner visibility,
exact signatures, initialization ordering and ordinary allocation access remain
checked. Six probes pass 1,764 repeated O0/O2 native/.NET comparisons with ASan/UBSan,
forced collection and exact cleanup. 29 adjacent/forged-IR checks and six corrected
cross-image access checks pass. General protected members and constructor chaining
remain unsupported. Integer-object CIL v11, object IR v21 and native v48; layout v19
is unchanged. Frozen engine discovery next needs Select in ReleaseFinished, whose
selector returns SpellId; state still exceeds its identity quota. See ADR AC0049.


### Inline-value delegate results (AC2.4)

Single-target delegates can return admitted named inline values, including nested,
empty, nullable and reference-bearing structs. The logical signature retains the
exact value identity. Lowering appends writable caller-owned result slots and uses
the existing struct-return path; reference leaves stay rooted across the call and
no callee-frame address escapes. Empty results still invoke the target and preserve
null/fault behavior. Expanded arguments and results share the existing budget.

Eleven probes pass 3,234 repeated native/.NET comparisons at O0/O2 with forced GC,
ASan/UBSan and exact cleanup. Focused malformed-IR checks reject invalid nominal
results, hidden slots and fault exits; the public baseline remains 31 native
controls and 16 gaps. Native v49, object IR v22, object layout v20, integer-object
CIL v12 and both delegate profiles v4 record the new calling convention. Direct
wide/narrow/enum results, byrefs and multicast remain outside this slice. No core
mapping or intrinsic grant changes. Frozen engine discovery still needs Select
(and then Distinct); full saved-game execution remains unverified. See ADR AC0050.


### Deferred Select and terminal traversal (AC6.6)

Non-indexed Enumerable.Select now supports admitted arrays, reference enumerables,
custom IList sources and inline-value delegate arguments/results. Selection stays
lazy, reuses the first same-thread iterator, clones later traversals, retains
Current until replacement/disposal and preserves null/fault ordering. Owned
Where/Select traversal helpers preserve earlier query cursors without generic
virtual calls. Chained Select keeps per-item callback order. Array/IList Any skips
selectors; Count runs them. IList Count/ToArray snapshot Count once and use its
indexer, while ordinary iteration uses GetEnumerator.

Eighteen probes pass 5,292 repeated native/.NET comparisons at O0/O2 with forced GC,
ASan/UBSan and exact cleanup; two unsupported-shape checks and 42 adjacent/core/
public-baseline checks pass. No compiler profile or intrinsic grants change.
Boxed collection factories, indexed Select and nongeneric Current remain outside
scope. Frozen engine discovery advances to Distinct at ReleaseFinished IL_004e;
state remains 257/256 identities. Complete replay remains unverified. See ADR AC0051.


### Default-comparer Distinct (AC6.7)

Enumerable.Distinct now preserves lazy acquisition, first-iterator reuse/cloning,
cached Current, insertion/fault order and cleanup for admitted sources. Any uses
first-element query paths without hashing. Count/ToArray build independent sets;
materialization copies stored values, including mutable hash results. Public
HashSet and private query sets share one non-enumerable SetStorage helper, keeping
unsupported collection factories out of closures that only need internal storage.
The public wrapper owns one additional managed storage object.

Eighteen probes pass 5,292 repeated O0/O2 native/.NET comparisons with forced GC,
ASan/UBSan and exact cleanup, including the value Where/Select/Distinct/ToArray
pipeline. The composed run passes 59 tests; all 11 existing HashSet checks also
pass after the extraction. Explicit comparers, DistinctBy, boxed collection
factories and HashSet enumeration remain diagnostic. No compiler profile, grant
or limit changes. Frozen engine discovery now needs OfType in ZombieSiege.
AdvanceRound IL_0009; state still reaches 257/256 identities. Full replay remains
unverified. See ADR AC0052.


### Nongeneric vector cursors and OfType (AC4.7 / AC6.8)

Bounded vectors now expose nongeneric IEnumerator through an exact managed helper
contract. Current preserves reference identity and freshly boxes scalar/inline/
nullable values, with exact before-start/after-end errors and Reset behavior.
Generic array and owned reference-class query cursors also support nongeneric
Current; boxed List/Dictionary/HashSet factories remain unsupported.

OfType filters lazily over admitted nongeneric sources, excludes nulls, preserves
cursor reuse/cloning and conditionally disposes the source. Count/ToArray/Any
preserve the original query cursor. Non-nullable already-typed sequences retain
the upstream identity shortcut. A private exact type-kind grant distinguishes
reference and value/nullable projection paths. Reference-result OfType.Select
remains diagnostic at the existing variance-only dispatch boundary; value/nullable
projection is tested. No general Type reflection or variance support is claimed.

Fourteen OfType and twelve cursor probes pass 7,644 repeated native/.NET comparisons
at O0/O2 with forced GC, ASan/UBSan and exact cleanup. Grant denial, lexical access,
logical/scalar dispatch packet checks, adjacent boxing and the public baseline pass.
Integer-object CIL is v13 and interface plan v9; native v49/layout v20 are unchanged.
Two exact grants are added. Frozen engine discovery advances to predicate Single
in ZombieSiege.AdvanceRound IL_0063; state remains 257/256 identities. See ADR AC0054.


### Predicate Single and SingleOrDefault (AC6.9)

The two-argument predicate overloads now preserve eager null checks, first loaded
match identity/copies, remainder scanning and exact no/multiple-match messages.
Arrays use direct indexing; supported reference cursors retain using disposal,
including disposal faults that replace earlier results or exceptions. Named inline
and nullable results use existing rooted copy/return lowering. Parameterless and
explicit-default overloads, List span access and boxed factories remain diagnostic.

Sixteen probes pass 4,704 repeated native/.NET comparisons at O0/O2 with forced GC,
ASan/UBSan and exact cleanup. All 43 selected Single, adjacent First and public
baseline tests pass. No compiler profile, intrinsic grant or resource limit changes.
Frozen EngineStep advances to a metadata-classification limit in FinalizeAccepted
IL_0165; StateLookup remains at 257/256 discovered identities. These are partial
closure failures, not measured emitted layouts. Full replay remains unverified.
See ADR AC0055 and artifacts/alchemy-single.


### Typed ordinal comparer (AC7b.6)

StringComparer.Ordinal now provides typed string Compare and Equals through the
abstract class and exact IComparer<string>/IEqualityComparer<string> contracts.
The fieldless comparer uses existing exact UTF-16 operations. A separate managed
cache owns one singleton per runtime instance. Exact application comparer contracts
also support the existing scalar/inline interface-call path. Generic variance is
not added. Hash APIs, comparer-object equality, nongeneric operations, other
factories and default comparers remain diagnostic.

Seven probes pass 8,376 repeated O0/O2 native/.NET comparisons, including nulls,
embedded NULs, unpaired surrogates, exact numeric comparison results, distinct
string snapshots and inline argument copies. Two-instance tests prove one comparer
root per instance beyond the runtime emergency exception and collection after
clearing that root. All 38 selected comparer/core/text/baseline checks pass with
forced GC and ASan/UBSan. No new intrinsic, compiler profile or resource limit.
Frozen EngineStep still needs IOrderedEnumerable/OrderBy; StateLookup still reaches
257/256 discovered identities. Full replay remains unverified. See ADR AC0060 and
artifacts/alchemy-ordinal-comparer.


### Deferred concatenation (AC6.10)

Concat now supports admitted arrays and reference-class enumerable cursors, including
linked chains, deferred acquisition, cursor reuse/cloning and exact disposal order.
Count/Any and ToArray preserve pinned cheap-count, first-element, checked-overflow
and collection callback order. Mixed chains use traced managed segments with the
pinned scratch/copy thresholds. Select integration distinguishes effectful counting
from materialization counts; a known empty result leaves the source cursor untouched.
Boxed List/Dictionary/HashSet enumerator factories remain unsupported.

Twenty-one probes pass 15,372 repeated native/.NET comparisons at O0/O2, including
all authored get/move/current/dispose/count/copy fault modes, zero-count identity,
reverse collection-copy order and reference-bearing segmented values. Forced GC,
ASan/UBSan, exact cleanup and frame bounds remain enabled. All 59 selected Concat,
adjacent Select/First, core and public-baseline checks pass. No intrinsic, compiler
profile or resource limit changes. Frozen EngineStep still needs IOrderedEnumerable
for OrderBy; StateLookup remains at 257/256 discovered identities. Full replay is
unverified. See ADR AC0061 and artifacts/alchemy-concat.

### Default integer comparers (AC5a.2)

Comparer<T>.Default supports exact Int32, UInt32, Int64 and UInt64 with one cached
object per closed type per runtime instance. Two exact private helper grants select
managed factories and existing typed CompareTo bodies. Eight probes pass 2,352
repeated native/.NET comparisons, plus exact two-instance root checks and grant /
unsupported-scope tests; all 59 selected local checks pass. Culture/string, nullable,
enum and other defaults remain diagnostic. Compiler/native/layout profiles and
resource limits are unchanged. See [ADR AC0064](../../docs/decisions/AC0064-integer-default-comparers.md).

`EnumQueries.cs` adapts pinned Enum/RuntimeType membership rules for admitted
Int32 enums. It preserves null/type/value checks, exact ordinal names, aliases,
empty enums and mismatch messages. Two separately granted private metadata helpers
read validated constants or copy an error message in one native allocation.
Generic IsDefined, wide enum payloads, parsing and general reflection remain outside
this bounded contract. See ADR AC0076 and library/provenance.json.

`StringSubstring.cs` provides both integer-index overloads using managed nonvirtual
String bodies, exact UTF-16 copying and identity-preserving empty/full shortcuts.
Negative actual-value messages follow the core's invariant numeric formatting
policy. Copies allocate an Int32 buffer and the resulting string within existing
quotas; no new helper capability is required. See ADR AC0077.
