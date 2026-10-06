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
