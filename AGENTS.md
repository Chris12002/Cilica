# Working on Cilica

Act as a senior developer and architect. Read `docs/design-specification.md`
before changing architecture, and present the intended design to the user for
feedback before implementation. Explain consequential choices in plain language.

## Project boundaries

- This repository has a metadata frontend, bounded Int32 native backend and
  bounded native object control flow with phase A roots, bounded non-moving
  collection and private retained handles. Bounded compiler-library profiles also
  implement values/borrows/returns, boxing, closed generics, single-target delegates
  and managed exceptions/type-test filters; see `docs/milestones/05-managed-semantics.md`.
  Milestone 6 adds the bounded owned core and verified real-kernel native transcripts;
  see `docs/milestones/06-library-and-kernel.md` for exact scope and evidence.
  AC1a adds bounded owned Nullable<T> storage/members through ordinary value
  copies and roots. AC1b.1 adds nullable conversions and named-struct box/copy
  unboxing. AC1b.2 adds integer/Boolean/Char and public Int32-enum box/copy
  unboxing, with strict nullable identity. AC1b.3 adds exact boxed isinst/castclass
  for those scalars and admitted named structs, including nullable targets. AC1b.4
  allows proven confined scalar boxes beside independent interface operations.
  Raw scalar/nullable unbox, scalar interface views/dispatch, unproven box escapes
  in those compositions and nullable keys remain diagnosed; see ADR AC0006.
  AC2.1 adds exact managed reference results to single-target application delegates
  through normal rooted returns. AC2.2 adds the required framework
  Func/Action/Predicate/Comparison declarations and Boolean results with exact
  scalar target checks before lowering. See ADRs AC0007/AC0008; private closed
  callbacks and full saved-game replay are not verified.
  AC3.1 adds concrete List Insert/RemoveAt/Clear/ToArray, preserving snapshot
  identity and releasing reference/value-element roots. AC3.2 adds exact array
  Insert/RemoveAt/Clear helpers, unblocking bounded list/read-only interface
  mutations. Generic array Clear throws; nongeneric Clear resets elements.
  AC3.3 adds Dictionary Remove/Clear and free-slot reuse, preserving mutating key
  callbacks and releasing value roots. Broader collections and replay remain
  incomplete; see ADRs AC0009–AC0011. AC5a.1 adds managed 32/64-bit integer
  Math Abs/Min/Max and typed CompareTo (ADR AC0012); object comparison and
  comparer-interface execution remain outside this slice. AC3.4 adds bounded
  list/array search and value removal. Value equality can mutate the stored
  element; preserve its managed borrow (ADR AC0013). AC7b.1 adds exact pinned
  ordinal comparison and IsNullOrEmpty (ADR AC0014); no culture-aware text claim.
  AC7b.2 numeric StringBuilder append has a fixed invariant-culture contract;
  differential hosts must select invariant culture (ADR AC0015). AC3.5 adds live
  Array.AsReadOnly views with per-type, per-instance empty caches (ADR AC0016).
  AC4.1 adds concrete List<T>.Enumerator with version/current/copy semantics
  (ADR AC0017); boxed interface enumeration remains unsupported. AC4.2 adds
  cached dictionary key/value views and copied value equality lookup (ADR AC0018).
  AC4.3 concrete dictionary enumeration invalidates only on new-key insertion;
  removal/clear/overwrite preserve its version (ADR AC0019). AC4.4 adds concrete
  key/value view enumerators with distinct current payload roots (ADR AC0020).
  Milestone 7 adds a bounded persistent Int32 C boundary and shared export modules; see
  `docs/milestones/07-embedding-and-phone-libraries.md` and `docs/embedding-abi.md`.
  V2/V3 exports share one validated closure; V3 adds typed retained object handles
  and void results; an optional capability copies exact UTF-16 through host-owned
  buffers, and Integer64 exports preserve all 64 bits. Independent multi-root
  discovery, structured value copying and phone support remain planned. Optional
  managed-exception capture retains a handle and copies type/message UTF-16 plus
  declaration identity; inner traversal and stack traces remain planned.
  The broader runtime/library and full public embedding SDK remain planned. Never describe
  planned features as implemented or claim untested language/platform compatibility.
- Cilica owns an independent IL-to-C++ compiler and managed runtime. Do not
  substitute NativeAOT, Mono or Unity's IL2CPP for its advertised implementation.
- Reuse existing compilers for C# input and native machine-code generation.
- Treat assemblies as data: do not execute them in the compiler process.
- Unsupported reachable semantics must produce actionable build errors.
- Do not change example game rules to conceal compiler or runtime defects.
- Keep application-specific game logic out of compiler/runtime packages.

## Performance priority

- For supported capabilities with established correctness, execution performance
  is the primary engineering priority. Preserve correct results while removing
  measured costs; do not defer known bottlenecks to add more capabilities.
- Historical "no performance claim" statements limit what past evidence proves;
  they do not authorize postponing optimization or preserving slow mechanisms.
- Treat internal ownership scans, diagnostic checks, helper dispatch and frame
  limits as revisable implementation choices. Distinguish public boundary and
  managed-semantic guarantees from private corruption diagnostics. Expensive
  internal verification should have an explicit diagnostic mode unless normal
  execution requires it for correctness; establish that distinction before removal.
- For performance changes, retain comparable before/after release measurements
  on representative workloads, with correct outputs, exact revisions and build
  settings. Use DiffPlex as the immediate execution benchmark and compare with
  ordinary .NET and Unity IL2CPP where available. Keep NativeAOT results distinct.
- Regenerate benchmark C++ with the compiler branch being changed. Check branch
  ancestry and compiler/library revisions before measuring; a recent benchmark
  report does not establish that its generated code represents the latest compiler.
  Retained-code replays are diagnostic experiments, not acceptance evidence for
  current-branch performance.
- Include Roslyn's emitted Release IL in performance investigations. Retain exact
  input DLL/PDB hashes, compiler options/version and method tokens/IL offsets;
  trace those through Cilica IR/C++, comparator output and native code. Separate
  source/library work from generated/runtime overhead. Consult matching Roslyn
  source when lowering needs explanation; disclose unavailable revision matches.
- Review correctness and speed together. Sanitizer/stress builds validate
  correctness separately from release timing. Revisit prototype limits using
  measured target budgets rather than treating their original values as permanent.

## Internal reference verification

Generated access and managed borrows trust live internal references by default.
`CILICA_VERIFY_INTERNAL_REFERENCES=1` restores heap-list membership diagnostics;
select it in native harnesses that inject arbitrary private pointers. Keep
`Instance::owns` and `traceable_reference` checked for public handles, exported
results and GC. Never substitute the internal shortcut at those boundaries.
Null/bounds/type checks and borrowed frame lifetimes remain required. See ADR 0196.

## Correctness and review

- Preserve managed arithmetic, exception order, object identity and ownership.
- Review every completed slice before committing. For generated C++, check
  undefined behaviour, rooting across allocations and native boundary ownership.
- Use focused local conformance tests and differential execution against .NET
  when implementing semantics. Add a reduced regression for each discovered bug.
- No tests are needed merely to mirror documentation edits.
- Keep the specification current. Add architecture decisions and a compatibility
  manifest as real implementation decisions and capabilities emerge.
- Commit coherent slices regularly; inspect main before integrating work and
  never overwrite another contributor's changes or rewrite shared history.

## Resource and output constraints

- No additional agents unless the user explicitly requests delegation.
- Hosted CI is disabled by default. Do not add/enable workflows, paid services,
  or spending increases without explicit authorization.
- Before an authorized push that can trigger hosted work, report aggregate
  runner minutes across every matrix job, repetition and retry.
- Prefer targeted local validation. Repeated expensive runs require a concrete
  new hypothesis or fix; inspect existing logs first.
- Present plans/reviews in chat. Do not create Canvas files unless requested.

## Long-running services

Use Cawtex MCP for development services; use the terminal for Git, builds and
bounded tests. Test-harness-owned disposable subprocesses may stay in that
harness. Inspect runtime/runs and confirm exact checkout paths before reuse.
Review detected commands, directories, environment, health checks and connections
before registration. Registration alone does not authorize execution. Use explicit
run IDs, inspect the cluster plan, and preserve one operation UUID and its full
arguments across retries until the result is known. Let Cawtex allocate ports.
Never kill unrelated processes or stop shared services merely because work ends.
If unavailable, report the required setup rather than starting an unmanaged server.


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


### Internal Int32 enums (AC5b.1)

Top-level internal Int32 enums now use scalar storage in admitted layouts and
same-assembly direct memory operands. Five probes pass 1,470 repeated O0/O2
native/.NET comparisons with sanitizers and forced GC. Cross-assembly operands,
nonpublic generic/box access and cross-owner internal fields remain checked.
The frozen state-lookup probe advances to a metadata-constant layout rejection;
it is not yet executable. No profiles/grants change; public counts remain 18 gaps
and 29 controls. See [ADR AC0026](docs/decisions/AC0026-internal-int32-enums.md).


### Metadata constant layouts (AC5b.2)

Validated primitive constant declarations no longer occupy class or struct storage.
Five probes pass 1,470 repeated O0/O2 native/.NET comparisons with sanitizers and
forced GC; 12 metadata boundaries, 97 layout/enum regressions and 21 baseline checks
pass. Direct field operations and malformed constants remain rejected. The frozen
state probe now reaches a private nested enum; replay remains incomplete. See
[ADR AC0027](docs/decisions/AC0027-metadata-constant-layouts.md).


### Direct string calls (AC7b.3)

Exact String.Length/Chars now accept Roslyn's direct call form as well as callvirt.
Existing declaration and intrinsic permissions remain enforced. New fault/UTF-16
probes and direct-call layout regressions pass 2,646 repeated O0/O2 comparisons;
150 string/admission checks pass with sanitizers and forced GC. No profile/grant
changes. See [ADR AC0028](docs/decisions/AC0028-direct-string-instance-calls.md).


### Nested enum storage (AC5b.3)

Exact nested Int32 enum payloads now use admitted layouts with separate lexical
operand access. Three probes pass 882 repeated O0/O2 native/.NET comparisons;
eight boundary checks and 30 previous enum/lexical checks pass. The frozen state
probe now reaches the unchanged 256-type catalog limit. Static value storage and
full replay remain unfinished. See [ADR AC0029](docs/decisions/AC0029-nested-enum-storage.md).


### Wide static storage (AC5b.4)

Int64/UInt64 static fields preserve all bits in per-instance storage. Five probes
pass 1,470 repeated native/.NET comparisons; two persistence/isolation runs, 18
static/default checks, 25 initialization/packet checks and 21 public baseline checks
pass. Native profile v42 and layout v16 account for the wider private slot. Inline
static structs and full replay remain unfinished. See
[ADR AC0030](docs/decisions/AC0030-wide-static-storage.md).


### Static inline-value copies (AC5b.5)

Whole static struct loads/stores now expand through validated value layouts into
per-instance scalar and traced reference slots. Eight probes pass 2,352 repeated
O0/O2 native/.NET comparisons; four GC scenarios, eight packet mutations, the
address boundary, 90 existing regressions and 21 baseline checks pass. Layout v17,
native v42. Frozen EngineStep advances to a nonpublic generic argument; state
lookup still reaches the type limit. Static addresses and full replay remain
unfinished. See [ADR AC0031](docs/decisions/AC0031-static-inline-value-copies.md).


### Generic source-type provenance (AC8.1)

Closed method snapshots now retain declaration signatures/locals, and a bounded
resolver API retains original operand syntax before generic substitution. This
separates explicit private names from opaque type parameters for the next access
slice. All 54 selected metadata/generic checks pass. No new execution capability
is claimed. See [ADR AC0032](docs/decisions/AC0032-generic-type-use-provenance.md).


### Private generic reference transport (AC8.2)

Original explicit type names are checked in their declaration scope before closed
substitution. Legal private reference arguments can pass through opaque generic
calls, locals, fields and delegates; foreign explicit names and member access stay
checked. Four probes pass 1,176 repeated O0/O2 native comparisons with forced GC
and sanitizers; 106 broad and 60 focused checks pass. Private T[] still requires
additional detached access evidence. Frozen EngineStep advances to String.Format;
StateLookup remains at the unchanged type limit. Full replay remains incomplete.


### Detached generic identity foundation (AC8.3)

Closed argument snapshots retain reader-free identity syntax and reproduce the
existing specialization hashes after bounded validation. All 28 selected checks
pass. This supplies identity evidence for later per-instruction array access;
it adds no new executable capability. See ADR AC0034.


### Private generic arrays (AC8.4)

Exact original T array uses now carry instruction-scoped context evidence through
logical and scalar IR. Both stages recompute specialization identities and check
the variable, element, offset and operation. Six probes pass 1,764 repeated O0/O2
native comparisons with sanitizers and forced GC, including List<private type>.
Eleven mutations fail in both stages; array permission does not authorize casts
or explicit foreign names. 117 broad and 46 post-review checks pass. Object IR v16
and integer-object CIL v4; native v42/layout v17 unchanged. Engine/state closure
and full replay remain incomplete. See ADR AC0035.


### Closure failure evidence (AC8.5)

Nested resolution now preserves inner IL locations and requesting calls. Type-limit
errors report bounded partial identity counts. 85 focused checks and 21 baseline
checks pass; limits are unchanged. Engine formatting is specifically the two-object
String.Format call in GameState.GetPlayer IL_0020. State discovery reaches 257
identities (143 distinct nominal declarations, 122 closed generic identities), not
a measured final runtime catalog. Capacity and full replay remain unresolved.
See ADR AC0036 and artifacts/alchemy-closure-diagnostics.


### Two-box composite formatting (AC7b.4)

The exact two-object String.Format overload now requires a direct same-block box
witness for each named ordinary value. Both overloads share the pinned parser;
repeated callbacks retain each boxed copy. Five new and five previous probes pass
2,940 repeated O0/O2 comparisons with sanitizers and forced GC; 71 formatting,
admission and baseline checks pass. Scalar/enum/provider formatting and aliases
remain diagnostic. Frozen GetPlayer advances to unmapped KeyNotFoundException;
full engine and replay execution remain unverified. See ADR AC0037.

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


### Scalar boxes beside typed interface operations (AC1b.5)

Scalar-box confinement is now required when a closure converts an unconstrained
reference to an interface. Already interface-typed receivers rely on ordinary IR
assignment/cast validation, so unrelated scalar boxes may be returned, stored in
locals/arrays/fields, forwarded recursively or passed through managed locations.
Object-to-interface tests/casts still require the existing confinement proof;
primitive interface contracts and boxed dispatch have not been added.

Twenty-one probes pass 6,174 repeated O0/O2 native/.NET comparisons with forced GC,
ASan/UBSan and exact cleanup. Logical/scalar checks reject escaping test aliases
when an unconstrained interface conversion exists and reject forged interface
parameter types. The proof uses block-local value identities. Boxed value IR v8
and scalar boxed IR v9 record the admission change; native v49 is unchanged.
See ADR AC0053. This prerequisite enables nongeneric cursor boxing; full replay
remains unverified.


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


### Type-classification budget evidence (AC8.10)

Snapshot-limit diagnostics now distinguish cached declarations/specializations,
field reads, remaining metadata work, explicit roots and validated Int32 enums.
Samples and per-image distributions are bounded; no limits or admission rules
change. Thirty-six focused metadata checks pass. Frozen EngineStep reaches 256
snapshots with 27,208 metadata work units left, 23 value/176 reference/5 vector
roots and 65 snapshots outside explicit value/reference roots. These are partial
counts, not emitted layouts. StateLookup remains at 257/256 discovered identities.
Next separate lexical visibility reads from structural layout reads; do not infer
capacity from either failure. See ADR AC0056 and artifacts/alchemy-type-classification.


### Declaration-only visibility reads (AC8.11)

Lexical source-type checks now read declaration nesting without loading fields,
interfaces or initializers. A shared owner-chain reader preserves structural and
lexical visibility facts. The visibility cache retains at most 256 entries;
uncached declarations charge the existing metadata ledger. Actual enum/layout
validation and all type, field, method and metadata limits remain unchanged.

Twenty-nine focused checks pass, including 294 O0/O2 native comparisons across
257 otherwise-unused generic identities; 70 existing access, array-evidence, enum,
core and public-baseline checks also pass. Frozen EngineStep gets beyond the
snapshot limit and now requires IOrderedEnumerable for OrderBy<Int32> in
ReconcileControlOpportunity IL_0079, followed by ordinal-string ThenBy and Concat.
StateLookup still reaches its separate catalog bound. Full replay is unverified.
See ADR AC0057 and artifacts/alchemy-lexical-reads.


### Abstract class slot metadata (AC8.12)

Closed class/interface target resolution now distinguishes a bodyless abstract
slot from its concrete implementation. Abstract declarations must belong to an
abstract owner, use ordinary managed IL flags, have no body and not be final.
Concrete receivers must resolve to a real override; re-abstraction and hidden
new slots preserve their original slot identities. The legacy resolver remains
restricted. Sixty-one focused metadata checks pass. Closed class and interface
target profiles advance to v3. This is metadata foundation only: executable
abstract class calls still require a bodyless IR declaration path. No placeholder
implementation is emitted. Full replay remains unverified. See ADR AC0058.

### Default-equality Contains (AC6.12)

The two-argument terminal preserves collection dispatch, pinned iterator shortcuts,
callback order, cursor state and disposal. Seventeen probes pass 12,240 repeated
O0/O2 native/.NET comparisons; reduced String/Object generic type-test operands add
588. All 66 selected slice/adjacent/baseline checks pass with forced GC, ASan/UBSan
and unchanged frame limits. Comparer/nullable and reference OfType.Select variance
compositions remain diagnostic. No new grants or profiles. Frozen engine passes
the missing declaration and reaches its metadata snapshot limit; state retains its
catalog limit. Full replay is unverified. See [ADR AC0068](docs/decisions/AC0068-default-contains.md).

### Generic classification without unused layouts (AC8.18)

Closed receiver classification now uses exact direct-base facts; generic argument
visibility uses declaration facts. Executable closed layouts keep full validation.
Two reduced probes (130 closed owners and 257 unused value arguments) pass 588
repeated native comparisons, and all 101 distinct selected checks pass. Malformed
actual storage, hidden arguments and excessive executable types still fail. No
resource bounds or profiles change. Frozen engine advances to physical-damage
selection before its snapshot limit; state remains at its catalog limit. Full
replay is unverified. See [ADR AC0069](docs/decisions/AC0069-generic-classification-facts.md).

### Non-enum classification without fields (AC8.19)

Enum detection first checks exact direct-base facts; ordinary internal classes no
longer decode unused fields during lexical access checks. Actual enum validation
and all bounds remain intact. The added internal-owner regression passes 294
repeated O0/O2 native comparisons; all 76 selected checks pass with forced GC and
ASan/UBSan. Frozen engine clears its snapshot blocker and reaches the four-string
Concat overload. State/replay remain incomplete. See [ADR AC0070](docs/decisions/AC0070-enum-classification-facts.md).

### Fixed-string concatenation (AC7b.7)

Three/four-string Concat preserves null/empty reduction, original identity and exact
UTF-16 through ordinary owned IL and existing construction grants. The array and
fixed overloads share the checked length guard. Six probes pass 11,664 repeated
O0/O2 comparisons; all 71 selected checks pass with forced GC, sanitizers and exact
cleanup. The ordinal Equals recognizer now filters its method name before applying
its unchanged exact contract checks. No profile or bound changes. Frozen engine
advances to a private containing-type helper call; state and full replay remain
incomplete. See [ADR AC0071](docs/decisions/AC0071-fixed-string-concat.md).

### Private containing-type calls (AC8.20)

Nongeneric nested types can call private members on a proven containing type in
the same image. Discovery and both IR stages require the complete matching lexical
suffix; sibling, enclosing-to-child, foreign, generic-owner and older-profile paths
remain rejected. Nine probes pass 2,646 repeated O0/O2 comparisons; all 80 selected
checks pass with forced GC and sanitizers. Integer-object CIL v17, object IR v25 and
native v51; layout/runtime algorithms and bounds are unchanged. Frozen engine now
reaches a nonpublic handler delegate; state/replay remain incomplete.
See [ADR AC0072](docs/decisions/AC0072-containing-type-private-calls.md).

### Lexically visible nongeneric delegates (AC2.5)

Private/internal nongeneric delegates retain lexical visibility through both IR
stages, including casts, arrays and expanded value signatures. Eleven probes pass
3,234 repeated native/.NET comparisons; all 181 selected checks pass with forced
GC, sanitizers and exact cleanup. Hidden generic/foreign access remains rejected.
Integer-object CIL v18, delegate IR v7, scalar delegate IR v5, object IR v26,
layout schema v21 and native v52; runtime payloads and bounds are unchanged.
Frozen engine passes handler construction and reaches the metadata snapshot limit
in forfeit preparation; state/full replay remain incomplete.
See [ADR AC0073](docs/decisions/AC0073-hidden-delegate-visibility.md).

### Scalar-enum classification facts (AC8.21)

Unboxed Int32 enum validation retains bounded positive facts instead of structural
layout snapshots. Catalog classification no longer counts those scalar aliases as
executable layouts; boxed enums retain full validation. Two 257-enum reductions
pass 588 repeated native comparisons, and all 99 selected checks pass, including
malformed declarations, field/work limits, actual layout limits and the public
baseline. Bounds and profiles are unchanged. Frozen engine advances to missing
Enum.IsDefined in PlayerState construction; state/full replay remain incomplete.
See [ADR AC0074](docs/decisions/AC0074-scalar-enum-classification-facts.md).

### Nominal scalar runtime type tokens (AC5b.6)

Admitted scalar/Int32-enum type literals share the canonical per-runtime identity
of boxed GetType while preserving distinct nominal types. Seven probes pass 2,058
repeated native/.NET comparisons, and all 52 selected checks pass, including both
IR stages, cache ownership/quotas and the public baseline. General value, Nullable,
floating and open tokens remain diagnostic. Integer-object CIL v19, object IR v27,
scalar boxed IR v10 and native v53; no new grants, payloads or bounds. Enum.IsDefined
and full engine/replay remain unfinished. See [ADR AC0075](docs/decisions/AC0075-scalar-runtime-type-tokens.md).

### Bounded enum membership (AC5b.7)

The selected `Enum.IsDefined(Type,Object)` accepts admitted Int32 enum values,
exact boxed Int32 values and exact ordinal UTF-16 constant names. Aliases, empty
enums, negative values and flags preserve .NET membership rules. Argument order,
exception types, parameter names and mismatch text are differential-tested.
Validated constant tables and exact private query/message grants preserve nominal
identity and native instance ownership; allocation uses compiler-owned text spans.
Integer-object CIL v20, object IR v28, scalar boxed IR v11, value-layout v4 and
native v54 record the new operation and metadata. Existing runtime limits remain.
The public baseline advances to 32 native controls and 15 diagnostic gaps. Frozen
engine admission passes this call, then reaches a later snapshot bound in
UnitRoster.WithViews. State lookup and full saved-game replay remain unfinished.
See [ADR AC0076](docs/decisions/AC0076-enum-defined.md).

### Managed String instance bodies and Substring (AC7b.8)

The integer-object profile admits ordinary nonvirtual managed String methods
through the existing call/access/fault path. String constructors, virtual slots,
initializers and direct storage access remain outside this admission. Both
Substring overloads preserve exact UTF-16, identity shortcuts, overflow-safe
range checks and parameter/fault order. Negative-value messages use the existing
invariant numeric-text policy. Copies use the existing rooted Int32 buffer and
FromCodeUnits helper; there are no new grants or allocation limits.
Six probes plus a raw direct-call/null shortcut pass 4,650 repeated native/.NET
comparisons. Both validators reject virtual/constructor/initializer alterations.
Integer CIL v21, object IR v29 and native v55 record admission; physical layout
remains v21. The public baseline is 33 native controls and 14 diagnostic gaps.
Frozen engine/state still stop at their type limits; saved-game replay is unfinished.
See [ADR AC0077](docs/decisions/AC0077-string-substring.md).

### Five-component tuple storage and equality (AC5b.8)

The owned core maps ValueTuple<T1,T2,T3,T4,T5> with all five public fields,
construction and left-to-right typed equality through existing closed values.
Component equality receives a fresh copy before each callback, preserving mutation,
short-circuit and exception order. Ten probes cover scalar/wide/reference storage,
copies, returns, heap fields, arrays, boxing, nullable values and typed equality.
Five-component hashing, comparison, formatting and boxed/object tuple APIs remain
explicit diagnostic bodies. Constructor receiver borrows across branch joins retain
the existing diagnostic; computing arguments before construction remains supported.
No compiler/runtime profile, intrinsic grant or resource bound changes. The public
baseline advances to 34 native controls and 13 diagnostic gaps. Frozen engine/state
remain at their type limits; full saved-game replay is not yet executable.
See [ADR AC0078](docs/decisions/AC0078-five-component-tuples.md).

### Byte and Char vectors (AC7a.1)

The integer-object profile admits distinct Byte/Char vector identities, allocation,
unsigned loads, narrowed stores, generic element copies, heap fields and shared
empty arrays. Runtime backing uses existing UInt32 slots, not a packed host buffer;
all allocations retain existing quotas. Exact casts/GetType and interface type
tests preserve nominal rules, including Byte/SByte list compatibility and distinct
Char/UInt16 behavior. Narrow vector interface calls and element borrows still
reject explicitly; wider/Boolean/signed-byte array storage remains unsupported.
Eleven probes and raw store instructions pass 6,198 repeated native/.NET comparisons.
All 142 selected checks pass; the public baseline is 35 native controls/12 gaps.
Integer CIL v22, object layout v22, object IR v30 and native v56 record the new
array identities and store narrowing. Frozen engine/state type limits remain;
full saved-game replay is unfinished. See [ADR AC0079](docs/decisions/AC0079-byte-char-vectors.md).

### Character-array String construction (AC7b.9)

The exact selected String(Char[]) constructor uses a granted managed copy factory
through the existing constructor-factory path. Null/empty inputs return canonical
Empty; nonempty inputs preserve every UTF-16 code unit in an independent snapshot.
Existing Int32-buffer string construction retains rooting and allocation quotas.
Six probes pass 1,980 repeated native/.NET comparisons; 54 selected checks pass,
including List construction, array/string regressions and the public baseline.
A metadata-read filter received seven further focused checks. Grant removal, wrong
call/source forms and altered IR remain diagnostic. Integer CIL v23 records the
admission; native v56, object layout v22 and object IR v30 are unchanged. The public
baseline advances to 36 native controls and 11 gaps. Section/pointer constructors
and full saved-game replay remain unsupported; frozen engine/state type limits
still apply. See [ADR AC0080](docs/decisions/AC0080-character-string-construction.md).

### Nested-type cast diagnostics (AC8.22)

Cast errors now use a separate bounded declared-type display name: enclosing
names are omitted at the outer level, while generic arguments keep full runtime
names. Runtime type identity and Enum.IsDefined text retain their prior names.
Six nested class/generic/array/box reductions add 1,764 native/.NET comparisons;
all 77 selected metadata, cast and enum checks pass. Native v57 records the
diagnostic correction. No layout, admission or resource limits change. UTF-8
work exposed this defect; its library slice and full replay remain unfinished.
See [ADR AC0081](docs/decisions/AC0081-nested-cast-names.md).

### Default UTF-8 byte encoding (AC7b.10)

Encoding.UTF8.GetBytes(String) now runs as owned managed IL over existing byte
vectors. Two passes preserve exact UTF-8 bytes, valid surrogate pairs and U+FFFD
replacement for isolated surrogates. Null/fault order, empty-array identity,
independent results, encoder identity and exact cast messages match .NET. Ten
probes pass 15,360 repeated O0/O2 comparisons; two-runtime ownership and allocation
quota recovery checks pass. Other encodings, decoding, sections and configurable
fallbacks remain diagnostic. No new intrinsics, layouts or resource limits.
The baseline now has 37 native controls and 10 gaps, compiled in smaller batches
under the existing subprocess limit. Frozen engine/state still hit type limits;
full replay is unfinished. See [ADR AC0082](docs/decisions/AC0082-default-utf8.md).

### Ordinal String prefix checks (AC7b.11)

Exact granted StartsWith(String,StringComparison) sites require a directly proven
Ordinal constant, then call an ordinary managed String prefix helper. callvirt
preserves receiver-first null checks; direct call requires a proven non-null
string literal. Other direct receivers remain diagnostic because the installed
.NET JIT differs for literal versus dynamic empty prefixes with a null receiver.
Seven probes pass 5,220 repeated native/.NET comparisons; denied grants, modes,
raw-null calls and malformed IR remain checked. All 43 distinct selected checks
pass across final focused runs. Integer CIL v24 records admission; native v57,
layouts and quotas are unchanged. The public baseline is 38 controls/9 gaps.
Frozen engine/state remain at type limits; full saved-game replay is unfinished.
See [ADR AC0083](docs/decisions/AC0083-ordinal-string-prefix.md).

### GUID text construction (AC5a.3)

The owned Guid(String) constructor parses D/N/B/P/X forms, Unicode whitespace and
pinned legacy prefixes using managed IL. Four words are copied into the receiver
only after successful parsing; exact null/format/overflow behavior follows the
pinned constructor contract. Shared whitespace classification covers all UTF-16
units. Six probes add 6,972 repeated O0/O2 native/.NET comparisons; all 29 selected
checks pass across final runs with forced GC and sanitizers. The public baseline
is 39 native controls and 8 diagnostic gaps. No compiler/runtime profiles, grants
or limits change. Static parsing APIs and byte/span constructors remain unsupported.
Frozen engine/state still reach type limits; full saved-game replay is unfinished.
See [ADR AC0084](docs/decisions/AC0084-guid-text-construction.md).

### Isolated engine capacity probe (AC8.23)

A disposable compiler copy with a 1,024-type diagnostic bound advances frozen
EngineStep to missing List.Sort(Comparison<T>) in GameState construction.
StateLookup translates to eight methods with 36 value layouts, 92 reference
layouts, 166 interfaces and two reference arrays. This is metadata/IR evidence
only: no C++ emission or native execution. Production limits remain unchanged;
full engine size and saved-game replay remain unverified.
See [ADR AC0085](docs/decisions/AC0085-capacity-only-engine-probe.md).

### Comparison-based List sorting (AC3.10)

List.Sort(Comparison<T>) uses pinned managed introsort over captured backing
storage, preserving callback order, partial mutation, fault wrapping and version
changes. Int32, reference and reference-bearing inline-value cases pass 3,324
repeated native/.NET comparisons at O0/O2 with forced GC and sanitizers. Reentrant
sorting exposed and fixed Clear/RemoveAt: reference-bearing slots are cleared,
scalar slots remain intact. Exact sort-type diagnostic names use a private granted
helper lowered to bounded string literals; general Type.ToString stays unsupported.
All 34 selected checks pass across final focused runs. Integer CIL v25 records
admission; native v57, layouts and resource limits remain unchanged. The public
baseline stays 39 controls/8 gaps because parameterless Sort is still unsupported.
An isolated larger-type diagnostic copy advances the engine to a metadata-work
limit in ApplyFallingUnconscious. Full replay remains unfinished.
See [ADR AC0086](docs/decisions/AC0086-comparison-list-sort.md).

### Diagnostic metadata-work audit (AC8.24)

An isolated 1,024-type/262,144-work copy reaches missing Enumerable.ToList in
AdvanceSlots after 67,822 charged metadata units; the next call is List.FindIndex.
StateLookup translates eight methods with 9,917 units. These partial measurements
leave full engine capacity unknown. Production limits remain unchanged, and this
audit neither emits nor executes native code. Full replay remains unfinished.
See [ADR AC0087](docs/decisions/AC0087-metadata-work-audit.md).

### AC3.23: Capacity plus ordinal dictionary construction (2026-10-07)

The exact Dictionary<string,TValue>(Int32, StringComparer.Ordinal) overload now
uses the checked ordinal factory and existing managed capacity constructor.
Separate exact grants, direct comparer proof, capacity-before-getter evaluation
and negative-capacity faults remain enforced. Six probes supply 1,764 repeated
O0/O2 native/.NET comparisons with forced GC and ASan/UBSan; exact root counts
also verify the skipped getter on argument failure. All 37 distinct selected
checks pass, including ordinal dictionary/set, allocation recovery and the
45-control/two-gap public baseline. Integer CIL v55; object IR v41, native v67,
layout v25 and production limits unchanged. See ADR AC0135 and
artifacts/alchemy-ordinal-dictionary-capacity. Full engine/replay remains open.

### AC8.36: Measured diagnostic instruction capacity (2026-10-07)

The frozen engine exhausts 65,536 aggregate instructions after 2,777 method
declarations and 327,295 metadata units. Raising only the isolated diagnostic
copy to 131,072 instructions reaches Array.IndexOf<string> after 327,656 units;
StateLookup remains eight methods/9,925 units. All six frozen input hashes match.
Production limits and public-baseline claims are unchanged. No engine emission or
execution. See ADR AC0136 and artifacts/alchemy-engine-instructions131072.

### AC3.24: Whole-vector Array.IndexOf (2026-10-07)

The exact two-argument generic Array.IndexOf now reuses existing granted search
and equality helpers after its null check. Strings, Int32/Int64, Int32 enums and
admitted IEquatable values preserve first-match results, live value mutation,
allocating/throwing equality and argument order. Eight probes supply 2,352 repeated
O0/O2 native/.NET comparisons with forced GC and sanitizers. All 28 search and
public-baseline checks pass. Nullable/reference fallback equality, range overloads
and narrow byte/char borrows remain diagnostic. No new grants, profiles, native
operations or production limits. See ADR AC0137 and artifacts/alchemy-array-indexof.
Full engine execution, fingerprint execution and saved-game replay remain open.

### AC7b.16: Proven strings and three-position formatting (2026-10-07)

Selected String.Format now supports typed String/null alongside directly boxed
admitted enums and ordinary values across one, two or three positions. A bounded
producer scan supplies candidates; translation independently checks actual typed
strings and same-block boxes. The shared managed parser preserves alignment,
UTF-16, repeated callbacks and exception order. All 43 selected formatting/enum
and public-baseline checks pass, including 5,376 new O0/O2 native/.NET comparisons
with forced GC and sanitizers. Arbitrary object aliases, joined boxes, numeric
boxes, IFormattable and flags remain diagnostic. Integer CIL v56; object IR v41,
native v67, layout v25 and limits unchanged. See ADR AC0138 and
artifacts/alchemy-mixed-format. Full engine/fingerprint execution and replay remain open.

### AC7b.17: Literal default formatting for boxed integers (2026-10-07)

Directly boxed Int32/UInt32/Int64/UInt64 now use owned invariant decimal rendering
in one/two/three-position String.Format, with a proven literal and no placeholder
format suffixes. Discovery and translation both check the literal; actual boxes
retain same-block identity proof. Existing alignment, callbacks and parser errors
are preserved. All 60 distinct selected checks pass, including 3,234 new native/.NET
comparisons and 294 for the advanced public probe, at O0/O2 with forced GC and
sanitizers. The public baseline is 46 controls/one gap. Dynamic formats, suffixes,
object aliases and other scalar kinds remain diagnostic. Integer CIL v57; object
IR v41, native v67, layout v25 and limits unchanged. See ADR AC0139 and
artifacts/alchemy-boxed-integer-format. Full engine/fingerprint execution and replay
remain open.

### AC8.37: Constrained reference calls (2026-10-07)

Closed reference receivers now load from an exact managed location after argument
evaluation and use existing null checks and class/interface dispatch. The value
receiver path remains separate; no receiver boxing or new native operation.
All 64 distinct selected checks pass, including 2,058 new repeated O0/O2 native/.NET
comparisons, forced GC, ASan/UBSan, malformed prefixes/locations and the public
baseline. Roslyn receiver-replacement shapes that cross managed-location joins
remain explicitly diagnostic. Integer CIL v58; object IR v41, native v67, layout
v25 and limits unchanged. See ADR AC0140 and artifacts/alchemy-constrained-reference.
Full engine/fingerprint execution and saved-game replay remain open.

### Bounded shallow-clone copies (AC8.106)

Clones above four scalar storage fields use noinline typed copy helpers with at
most four assignments each; allocation/root/header rules remain unchanged.
An owned 20-field regression reproduces the previous 2,672-byte O0 stack failure.
All 33 focused clone/module/boxed checks pass, including 354 additional repeated
native/.NET comparisons, O0/O2, forced GC, sanitizers and wide quota recovery.
Native profile 90; managed profiles, grants and production limits are unchanged.
See ADR AC0250. Full engine linking/execution and saved-game replay remain open.

### Exact string-array joining (AC8.107)

Join(string,string[]) validates null with parameter value and reuses the existing
managed JoinStorage helper. Twenty distinct focused checks pass across the main
run and corrected quota follow-up, including 3,072 new repeated O0/O2 native/.NET
comparisons with forced GC and sanitizers. No compiler/profile/grant/limit changes.
The unchanged captured Alchemy reference adapter next fails CIL2001 at Run
IL_013b: implicit rejection-text concatenation loses the class receiver proof
across its null-check stack edge. A local/explicit call translates; do not rewrite
the adapter or relax Object receiver guards. See ADR AC0251 for pins and scope.
