# Cilica design specification

Status: proposed design, version 0.2. Recorded 2026-09-28.

Cilica is a working name, pending a naming check. This specification describes
planned work, not implemented capabilities or compatibility guarantees.

## 1. Purpose and success criteria

Build an independent IL-to-C++ compiler and managed runtime, with Expo as its
first application integration. The portfolio value comes from implementing
language semantics, memory management, interoperability and cross-platform
execution, supported by reproducible evidence of correctness.

Cilica compiles supported .NET assemblies into readable C++, then builds native
libraries or WebAssembly modules. Applications embed those outputs through a
stable API. Its four independently useful deliverables are:

- An IL-to-C++ compiler.
- A runtime providing objects, collection, exceptions and type information.
- An embedding SDK for other languages.
- An Expo package with generated TypeScript bindings.

The flagship demonstration is Alchemy's existing C# turn engine running offline
on iOS, Android and web, producing equivalent results from identical starting
state and command transcripts. Alchemy is an external validation target; its
code and assets are not incorporated by this specification. Independent public
examples must remain usable without access to it.

## 2. Architecture and ownership

```mermaid
flowchart TD
    CS["C# source"] --> ROS["Existing C# compiler"]
    ROS --> IL[".NET assemblies and portable PDBs"]
    IL --> READ["Metadata reader and IL verifier"]
    READ --> IR["Typed intermediate representation"]
    IR --> ANALYSE["Reachability, generics and optimisation"]
    ANALYSE --> CPP["C++ code generator"]
    CPP --> NATIVE["Clang: native library"]
    CPP --> WASM["Emscripten: WebAssembly"]
    RT["Cilica runtime and supported class library"] --> NATIVE
    RT --> WASM
    NATIVE --> ABI["C embedding API"]
    ABI --> EXPO["Expo native module"]
    WASM --> WEB["JavaScript web adapter"]
    EXPO --> APP["Shared TypeScript API"]
    WEB --> APP
```

Roslyn supplies C# parsing and IL generation. Clang supplies machine-code
generation. Cilica owns semantic translation, its runtime and the supported
library surface. NativeAOT may serve as a benchmark comparator; it must not
silently compile code advertised as Cilica output.

Web uses Cilica's generated C++ and runtime compiled to WebAssembly. It does not
switch to Microsoft's .NET WebAssembly runtime, so native and web test the same
implementation.

## 3. Compatibility contract

Cilica is a closed-world ahead-of-time compiler: dependencies and entry points
are known at build time. Its semantic reference is
[ECMA-335](https://ecma-international.org/publications-and-standards/standards/ecma-335/),
with documented choices where the standard permits variation.

Compatibility is expressed as IL operations, runtime behaviours and library
members, not an unqualified C# version claim. New syntax can work if it lowers
to supported operations; an ordinary method can fail on an unsupported API.

| Area | Initial production-quality profile |
| --- | --- |
| Values | Integers, booleans, characters, enums, structs, nullable values |
| Objects | Classes, inheritance, interfaces, dispatch, casts |
| Collections | Arrays, lists, dictionaries, sets, required interfaces |
| Generics | Reachable closed generic types and methods |
| Functions | Static/instance methods, delegates, closures |
| Control flow | Branches, loops, switches, supported exception regions |
| Metadata | Type identity, `typeof`, `GetType`, required enum operations |
| Memory | Precise collection including cyclic object graphs |
| Embedding | Explicit exports, structured values, errors, retained objects |
| Execution | Single-threaded instances; `Task`-based async on an event loop |

This profile is a milestone goal, not an initial prototype promise. General
reflection, dynamic assembly loading, Reflection.Emit, unsafe code and
unrestricted native calls are outside it. Unsupported reachable behaviour fails
compilation; no silent fallback or semantic approximation.

Execution profiles are staged and chosen at build time
([ADR 0001](decisions/0001-execution-and-threading-model.md)): single-threaded,
then single-threaded async for the first public release, then multithreaded.
Multithreading is outside the initial profile, but the IR, object header, type
initialization and generated calling convention are designed so that adding it
does not require a rewrite. Code that starts threads fails compilation with a
diagnostic naming the multithreaded profile.

Floating-point and decimal operations require separate documented semantics and
conformance before support is claimed. Cross-platform bitwise game determinism
is an application guarantee to test, not an automatic property of the compiler.

## 4. Frontend and diagnostics

The compiler is written in C#. Input assemblies are data: never execute them or
their initializers inside the compiler. Use System.Reflection.Metadata and
PEReader to read metadata and method bodies
([API documentation](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.metadatareader)).

The frontend resolves a pinned dependency graph; decodes signatures, attributes,
generic parameters and exception regions; constructs basic blocks; validates
branch targets and stack consistency; resolves referenced members; and reads
source locations from portable PDBs where available.

The implemented resolver foundation uses explicit directories and strict
manifest identity, with forwarding and conflict diagnostics
([ADR 0005](decisions/0005-explicit-assembly-resolution.md)). A bounded structural
signature and declaration binder handles generic and inherited members
([ADR 0006](decisions/0006-structural-member-binding.md)). Ordinary dependency
graphs retain generic instances, inclusion reasons and explicit analysis gaps
([ADR 0007](decisions/0007-dependency-graph-foundation.md)). Bounded conservative
dispatch revisits included named receiver types to a fixed point and retains
unsupported cases as issues ([ADR 0008](decisions/0008-conservative-dispatch-expansion.md)).
Ordinary nodes and dispatch pairs take alternating turns within existing budgets
([ADR 0012](decisions/0012-interleaved-reachability-scheduling.md)).
Closed named constrained instance calls retain their constraint and receiver
evidence ([ADR 0010](decisions/0010-constrained-instance-dispatch.md)).
The `cilica audit` command reports feature observations, image hashes, inclusion
reasons and incomplete-analysis status ([ADR 0009](decisions/0009-audit-evidence-contract.md)).
Explicit library selection maps primitive constraints and implicit string receivers
to named definitions ([ADR 0011](decisions/0011-explicit-runtime-type-mapping.md)).
Selected-library structural array receivers follow inherited class and declared
interface bodies ([ADR 0013](decisions/0013-structural-array-receivers.md)).
Broader framework retargeting, array intrinsics/generic helpers and ported-library
closure evidence remain subsequent work;
these foundations do not establish executable compatibility or clear the
general-filter gate.

Exact class virtual target resolution is now a separate metadata operation
([ADR 0033](decisions/0033-exact-class-virtual-targets.md)). For a public nongeneric
virtual declaration and a concrete class receiver, it identifies the canonical
slot and one implementation, preserving newslot hiding and final overrides.
An iterative hierarchy walk and the caller's shared metadata ledger bound its
work. Interface receivers, abstract/generic hierarchies and class MethodImpl remapping
remain unsupported in this exact profile. Validated interface-only MethodImpl rows
can coexist with ordinary class slots ([ADR 0041](decisions/0041-mixed-class-interface-dispatch.md)).
The conservative audit walker retains
its existing contract. The object translator now uses exact choices to expand
class virtual method closure to a fixed point and validates detached dispatch
maps before native consumption
([ADR 0034](decisions/0034-native-class-virtual-dispatch.md)).

Exact nongeneric interface target resolution is a separate metadata operation
([ADR 0035](decisions/0035-exact-interface-method-targets.md)). It preserves inherited
mappings, starts a new search on reimplementation and validates explicit interface
MethodImpl bodies before selecting the effective class slot. Iterative interface
closure handles diamonds within bounded type/work ledgers. Default/static interface
members, generics, abstract class hierarchies and class slot remapping remain
unsupported. Interface catalogs and detached reference flow are implemented
as described in section 7. Detached declarations/maps are implemented under
ADR 0037. Interface `callvirt` translation discovers selected executable bodies
([ADR 0038](decisions/0038-interface-call-translation.md)), and native interface
dispatch has local O0/O2 differential execution evidence on macOS ARM64
([ADR 0039](decisions/0039-native-interface-dispatch.md)). Interface casts and type
tests are also implemented ([ADR 0040](decisions/0040-interface-casts-and-type-tests.md)).
The conservative audit contract remains unchanged; collection, broader interface
semantics and other-target execution remain separate gates.

Verification covers the supported execution profile. Cilica is not initially
a complete CLI verifier or a sandbox for hostile programs. Native compiler
execution and dependency restoration are explicit build steps, not consequences
of merely inspecting an assembly.

Diagnostics include the unsupported feature, source/IL location and reachability
path. Illustrative output:

```text
CIL2041: Runtime assembly loading is unsupported.
Export: MatchService.Create
  -> ContentLoader.Load
  -> Assembly.Load(byte[])
Game.Content.dll, ContentLoader.cs:84, IL offset 0x0032
```

Diagnostic identifiers and command names in this document are proposed APIs.

## 5. Intermediate representation

Convert IL's implicit evaluation stack into typed values and explicit control
flow. Distinguish managed references, native pointers, boxed values, value
types, checked/unchecked arithmetic, direct/virtual/interface calls, potentially
throwing instructions, GC safepoints and managed references to storage.

The initial straight-line Int32 translation profile is implemented in
`Cilica.Compiler` ([ADR 0014](decisions/0014-straight-line-int32-ir.md)), with
ordered typed temporaries and explicit wrapping arithmetic. The
[compatibility manifest](compatibility.md) distinguishes translation from native
execution. C++ emission and local native differential execution now cover that
profile ([ADR 0015](decisions/0015-int32-cpp-emission.md)). The control-flow profile
adds initialized Int32 locals, comparisons, jumps, conditional branches and typed
stack inputs across joins/backedges ([ADR 0016](decisions/0016-int32-control-flow.md)).
The program profile adds a complete bounded closure of direct static Int32 calls
([ADR 0017](decisions/0017-bounded-static-call-closure.md)). Bind exact scoped
declarations through explicit reference directories; keep method identity as image
content hash plus MethodDef token in detached IR. Validate all reachable bodies
before exposing a program. Generic, virtual/indirect calls, broader access rules,
inherited MemberRef owners and required initialization remain unsupported.
The Int32 profile now also supports bitwise AND/OR/XOR/complement and left,
signed-right and unsigned-right shifts ([ADR 0018](decisions/0018-int32-bitwise-and-shifts.md)).
A unary expression captures complement's one-operand stack effect. Mask shift
counts with 31, including raw IL counts outside 0..31; this is a defined C#/.NET
profile choice for cases CIL leaves unspecified. Signed/unsigned Int32 division
now has explicit method-exit failure edges, with zero-divisor and signed-overflow
faults ([ADR 0019](decisions/0019-primitive-pending-faults.md)). Calls also retain
explicit failure edges. This primitive profile remains Int32-only; the bounded
object checkpoint is described in section 7, with the broader runtime still planned.

Checked signed/unsigned Int32 add, subtract and multiply also use these edges
([ADR 0020](decisions/0020-checked-int32-arithmetic.md)). Their exact wider result
must fit the selected 32-bit range before conversion; otherwise an overflow
fault propagates through the existing context.

Signed/unsigned Int32 remainder also uses the pending-fault path
([ADR 0021](decisions/0021-int32-remainder.md)). Zero divisors fault, and the
selected profile raises Overflow for signed MinValue rem -1, independently
observed on the pinned .NET runtime. Unsigned remainder interprets operand bits
and has no signed overflow case.

Begin with typed basic blocks and temporaries. Introduce SSA after baseline
semantics are established. Optimisations preserve exception order and observable
side effects. Planned passes include constant folding, unreachable-block removal,
devirtualisation and proven bounds-check elimination. Every pass is individually
disableable for diagnosis and differential testing.

## 6. C++ generation

Generated methods have stable names and mappings to original methods and IL
offsets. Use explicit managed layouts and dispatch tables rather than relying
on C++ inheritance or RTTI matching CLI semantics.

The implemented backend emits a validated Int32 program as private C++20 helper
functions and explicit C-linkage entries. Declarations precede bodies, including
recursive calls. Each program has a hash/token namespace; shared helpers in
separately generated roots retain private linkage. Call arguments are immutable
temporaries already evaluated in IL order, avoiding native argument-evaluation
ordering dependencies. Temporaries hold unsigned bit patterns; arithmetic
widens to unsigned 64 bits, then retains the low 32 bits. `std::bit_cast` preserves
signed parameter/result representation. Source comments map the image hash,
MethodDef token and IL offsets; input display names never become C++ syntax.
The compiler emits source only. An independently owned, bounded local test harness
compiles and executes authored fixtures at `-O0` and `-O2` with sanitizer checks.
This proves the small primitive profile on the recorded local target, not the
planned managed runtime or embedding ABI.

Control-flow lowering uses a C++ scope for each IR block and explicit labels.
Local storage is zero-initialized once per invocation. Stack inputs are captured
as immutable block-local temporaries before outgoing edge copies; copies cannot
overwrite their own sources. Signed comparisons reinterpret bits as Int32,
unsigned comparisons retain UInt32 bits. Verification rejects incompatible joins,
invalid targets, unreachable blocks and missing local initialization. Bound aggregate
statements, stack inputs and edge arguments as well as method bytes/instructions.
Bitwise operations use unsigned 64-bit intermediates and narrow to the low 32
bits. Shifts mask their count to 0..31 before unsigned native shifts. A private
signed-right helper zero-fills the value and explicitly supplies its sign bits,
with an empty fill at count zero. Native signed overflow and oversized shifts
are never used to reproduce managed results.

Division guards zero and signed MinValue/-1 before native evaluation. Signed
division widens to Int64 and narrows by unsigned conversion; unsigned division
uses operand bit patterns. Internal helpers share a private pending-fault context;
a reverse-call analysis determines which calls need an immediate failure check.
Each boundary uses a fresh context. A distinct `_try` entry returns a primitive
status and writes result/origin outputs; only proven pure roots also expose the
old value-only entry. See ADR 0019 for the pointer/lifetime contract. These tags
are a propagation foundation, not managed exception objects or handlers.

Checked signed arithmetic uses Int64 intermediates, whose range contains all
Int32 sums, differences and products. Unsigned add/multiply use UInt64 to retain
the full product; unsigned subtraction uses Int64 to represent negative differences.
Check the selected signed or unsigned 32-bit range before narrowing. The same
operation classification drives translation's exceptional edges, IR validation
and conservative may-fault analysis.

Remainder shares division's zero guard and the selected signed MinValue/-1
overflow guard. Widen signed operands to Int64 or unsigned operands to UInt64
before native `%`, then preserve the resulting low 32 bits. Nonzero signed
remainders have the dividend's sign. Do not substitute a widened zero result
for the selected profile's signed overflow fault.

- Unchecked integer arithmetic wraps with defined behaviour.
- Checked arithmetic raises the proper managed exception.
- Division, shifts and conversions explicitly handle edge cases.
- Null references and bounds failures produce managed exceptions.
- Struct copies preserve embedded managed references.

Valid managed programs must not acquire C++ undefined behaviour during lowering.
Pin a C++20 toolchain. Develop on macOS first, then Android/iOS ARM64 and browser
WebAssembly. Expand portability claims only after conformance passes per target.

## 7. Objects, types and initialization

The bounded logical layout foundation is implemented
([ADR 0022](decisions/0022-detached-object-layouts.md)): a detached class-field
closure preserves content-scoped TypeDef/FieldDef identities and distinguishes
Int32 fields from class references. Reference maps use logical field slots,
not native byte offsets. This describes automatic-layout nongeneric classes
derived directly from an explicitly selected object base. Layout descriptors alone
do not provide byte offsets, allocation, method roots or object execution.

The subsequent typed value-flow contract is implemented
([ADR 0023](decisions/0023-typed-reference-value-flow.md)). Shared IR values carry
Int32, exact class-reference identity or the null stack type. A separate
`ReferenceIrProgram` validates declared locals, joins, direct-call arguments and
returns against a bounded class catalog. Null can enter reference storage;
concrete references can widen to the selected object base. Loads/call results
cannot change their declared type, and ordered reference comparisons or reference
arithmetic fail validation. This checks caller-constructed IR, not CIL method
access, constructor/field semantics or reference execution. The primitive emitter
accepts only the existing Int32 profile. Object emission is a separate validated
profile with phase A roots, described below.

The bounded straight-line object-operation translator is now implemented
([ADR 0024](decisions/0024-straight-line-object-operations.md)). Its explicit entry
and selected runtime library produce an `ObjectIrProgram` with exact method/field
bindings, allocation, field operations and void calls. Constructors initialize
their direct receiver through the selected trivial Object constructor before
other work. Readonly/access checks precede consumption. Nonvirtual `callvirt`
retains a null check after argument evaluation; a direct instance `call` does not
insert one. Allocations, calls and fields have explicit method-exit fault edges.
A test-only evaluator agrees with authored .NET probes. The original straight-line
profile is now extended by ordinary-method reference control flow below.


The first bounded native object checkpoint is implemented
([ADR 0025](decisions/0025-native-object-ownership-and-roots.md)). A separate root
assignment pass allocates distinct slots for reference arguments, locals and
temporaries before C++ emission. Generated frames publish references before
allocation and allocating calls and pop on every explicit fault/normal return.
C++ standard-layout checks provide native sizes, alignments and reference-field
offsets. Each packed invocation owns a bounded heap. The initial checkpoint retained
objects until teardown; milestone 4 now collects it as described in section 8.
The public persistent object embedding API remains planned. Native O0/O2
ASan/UBSan differential probes cover construction, mutation, reference returns,
allocating calls, null/arithmetic faults, ownership and frame/heap lifetime.
The initial limits are 1,024 roots per method, 64 active calls, 1 MiB of object
storage and 4,096 allocations per invocation.


Ordinary object methods now support bounded branches and loops
([ADR 0026](decisions/0026-reference-control-flow.md)). Stack type inference revisits
blocks until structural stack types converge; mixed integer/reference values or
unsafe narrowing fail. After complete catalog discovery, a bounded refinement pass
intersects common ancestor sets across branches/backedges and selects a unique
most-specific shared class or interface ([ADR 0042](decisions/0042-reference-join-inference.md)).
Ambiguous intersections require explicitly typed locals or casts before the join.
The emitter uses block-scoped temporaries and distinct
rooted input storage, preserving parallel stack transfers on backedges. Inference
has an aggregate 262,144-unit work budget in addition to existing IR/metadata
bounds. Constructors remain straight-line; switches,
argument stores and exception regions remain unsupported. Authored C# and raw
stack-carrying IL match .NET under native sanitizers. The original control-flow checkpoint retained objects until teardown; section 8
records the subsequent collector and its stress evidence.

Object execution storage now uses a separate context-owned, non-moving arena
([ADR 0027](decisions/0027-budgeted-execution-frames.md)). The default 1 MiB
execution budget includes typed parameter/local/value/input slots, roots,
argument packets, scratch and frame metadata with target alignment. The 64-call
limit remains; storage exhaustion returns status 9. Scope guards unlink roots
and release reservations on all exits. Internal calls pass argument-storage
pointers, and bounded non-inlined C++ helpers share each heap frame through a
small dispatch loop. Merely moving explicit locals did not bound Clang's O0
spills, so target native-stack measurements are part of this checkpoint.
The separate primitive backend retains its earlier storage model. No collector,
performance or additional target guarantee follows from execution frames.

The object boundary now uses a fixed-size native signature with an Int32 input
pointer and exact element count, including for wide managed entry methods
([ADR 0028](decisions/0028-packed-boundaries-and-arena-diagnostics.md)). Its
`_objects_packed_try` symbol distinguishes the incompatible experimental ABI.
Invalid counts or required null pointers return status 3 without output writes.
ASan builds poison unused/released arena storage and unpoison reservations;
ordinary builds retain the same layout. Native validation includes wide exports
and complete finite/mutual recursion on a bounded pthread stack at O0/O2 on the
recorded macOS ARM64 target. Other targets still require their own measurements.

Local opt-in benchmarks now compare execution overhead with the primitive
backend and .NET ([ADR 0029](decisions/0029-measured-helper-batching.md)). A
four-statement candidate reduced generated code for nonallocating workloads but
exceeded the 2 KiB native-frame ceiling in reference-field sanitizer tests.
The emitter retains two-statement helpers. Throughput results are inconclusive
because of measurement variability; future batching must account for the cost
of reference checks as well as ordinary arithmetic.

The detached layout and reference-IR APIs now support concrete nongeneric class
hierarchies ([ADR 0030](decisions/0030-detached-class-hierarchies.md)). Catalogs
include every ancestor, reject inheritance cycles and sealed bases, and retain
fields on their declaring class. Derived references can flow to ancestor-typed
locals, arguments, returns and declared joins. The existing type/field budgets
bound this work. Native object operations now compose ancestor fields into each
concrete allocation ([ADR 0031](decisions/0031-native-inherited-fields-and-constructors.md)).
Descriptors retain base links, measured field offsets and inherited reference
maps; base methods access the actual allocation without casting it to a base
C++ struct. Single-block constructors call their immediate base once, preserving
declared-field initializers and argument evaluation before that call. The derived
receiver stays rooted with its original dynamic type throughout allocating base
bodies. Physical field copies have a separate 65,536-slot bound; field-operation
helper isolation retains the O0/O2 native-stack gate. Abstract classes,
broader access rules remain subsequent slices.

Detached catalogs now include public nongeneric fieldless interface contracts
separately from class layouts
([ADR 0036](decisions/0036-interface-catalogs-and-reference-flow.md)). Classes retain
their declared interface edges; contracts retain parent interfaces. Class/base,
interface/parent and field closure share the 256-type budget. Iterative validation
rejects cycles and edges of the wrong kind. Typed reference assignments support
class-to-implemented-interface, inherited contracts, interface-to-parent and
interface-to-Object widening, including locals, direct-call arguments, returns
and declared joins. Interfaces use the existing logical reference storage kind
without receiving instance layouts. Names do not establish identity. This is
detached type flow only: catalogs alone do not validate interface method contracts
or implementations. Object operations and root assignment now accept contract
references; native emission also accepts those catalogs and validated call plans.
Interface casts/tests are described below; inferred joins with a unique common
class or interface are supported under ADR 0042.

Interface dispatch preparation now retains bodyless signatures separately from
class method facts ([ADR 0037](decisions/0037-detached-interface-dispatch-plans.md)).
Plans cover every compatible catalog class for each requested interface member,
preserving the initial mapping and effective override as distinct identities.
Detached validation checks exact signatures, inherited mapping stability,
canonical class slots, nearest included overrides and final/private slot rules.
One shared ledger bounds declaration/catalog/target inspection, and declarations
plus class facts share 256 method identities and 65,536 parameter slots. Class
facts assert that a managed IL body exists but do not read its instructions.
This preparation API does not authenticate caller-forged metadata assertions,
translate bodies, grant private access, assign roots or emit executable calls.

Interface `callvirt` translation now uses those bodyless signatures while
discovering only the selected implementation bodies
([ADR 0038](decisions/0038-interface-call-translation.md)). Each body can add
calls or receiver types; translation revisits dispatch maps until that closure
stops growing. Declaration, type and exact receiver-choice caches share the
existing metadata ledger. Initial mappings and canonical slots remain metadata
unless a call actually needs their bodies. A null-only call can have an empty
map, while retaining argument evaluation before its null check and method-exit
fault edge. Executable signatures/bindings must agree with the plan; declarations,
mapping facts and bodies share the 256-method cap, and class/interface target
entries share 65,536 entries. Phase A assigns interface reference slots and call
safepoints using the existing reference storage kind. Ordinary class virtual
resolution now validates interface-only MethodImpl rows through the same bounded
interface hierarchy reader; class slot remapping still requires later lowering.

Native interface calls now use exact descriptor membership and typed method
tables ([ADR 0039](decisions/0039-native-interface-dispatch.md)). Contracts receive
no physical layout. A reference retains the original object pointer and dynamic
class descriptor through interface locals, fields, arguments and returns.
Ownership is checked before reading a receiver descriptor; membership compares
the descriptor address with the compatible catalog classes, including inherited
and parent contracts. Each interface call then chooses its validated effective
body using the same typed function pointers and arena argument packets as class
dispatch. Null and ownership/type faults retain their method/IL origin, with
argument faults occurring first. Callee depth/frame checks and root cleanup are
unchanged. O0/O2 native comparisons, allocation observers, exact/short budgets,
fault cleanup and constrained-stack recursion prove this retained-heap slice;
collection and other target toolchains remain separate gates.

Class `castclass` and `isinst` now use those descriptor base links
([ADR 0032](decisions/0032-class-casts-and-type-tests.md)). Targets are exact
TypeDef/TypeRef identities in the bounded concrete catalog, including types
reached only by a test. Both accept reference/null operands and preserve the
original reference on success; null remains null. A failed cast reports status
7 with its originating method/IL offset; a failed type test returns null.
Ownership checks precede descriptor reads. Results occupy rooted arena slots,
and casts/tests cannot expose an unfinished constructor receiver. Value-type,
generic, array and TypeSpec targets still require later lowering.

Interface `castclass` and `isinst` now use exact descriptor membership
([ADR 0040](decisions/0040-interface-casts-and-type-tests.md)). Named public
nongeneric contracts enter the same bounded catalog even when used only as a
target. Nested contracts require public visibility through every enclosing type;
the bounded metadata walk retains this fact for detached validation.
Membership includes inherited implementations and transitive parent/diamond
contracts; an empty table still accepts null. Both operations preserve the
original reference on success and use the existing cast/type-test fault behavior,
rooted arena slots and constructor restrictions. Ownership is established before
reading an object descriptor, and membership never dereferences unknown descriptor
addresses. Native O0/O2 comparisons cover composed class/interface casts,
identity, argument fault order, allocation roots, exact/short budgets and cleanup.

Public nongeneric class virtual calls now select one body from the receiver's
exact descriptor. Override bodies can introduce new classes and virtual sites;
closure expansion revisits all compatible catalog classes until no new bodies
remain. Detached maps cover every compatible receiver and preserve canonical
slots, nearest overrides, newslot hiding and final implementations. Direct
`call` remains a direct invocation even when its declaration is virtual.
Virtual `callvirt` checks null, ownership and ancestry after argument evaluation.
Typed lookup helpers finish before the selected body executes; their function
pointers live in the caller's budgeted heap frame, retaining the existing
native call-chain bound. Reference results and allocating overrides use phase A
roots and normal/fault cleanup. Abstract classes, generics, covariance
and class MethodImpl slot remapping remain unsupported. Mixed class/interface calls
on owners with validated interface mappings share the existing native call path
([ADR 0041](decisions/0041-mixed-class-interface-dispatch.md)).
The native evidence is O0/O2 on the recorded macOS ARM64 target; no wider platform
or collector claim follows.

Helper batching remains deferred until representative workloads justify it.
The current 2 KiB warning is a tested baseline, not a permanent production
requirement. Revisit it using speed measurements and complete native call-chain
budgets for each target, with sanitizer overhead considered separately.

Managed objects contain a runtime header and fields. The header reserves a
synchronization word for identity hash codes and lock state. Type descriptors
record base types, interfaces, dispatch tables, size, reference-field maps,
array element metadata, generic identity and required reflection data.

Static fields belong to runtime instances, not uncontrolled process globals.
Thread-static fields are reached through the thread's execution context.
Model static constructor ordering, failure caching and `beforefieldinit`;
initialization state records the initializing thread for the later
multithreaded profile.
Strings contain UTF-16 code units; arrays retain element type and bounds.
The detached string contract is implemented
([ADR 0046](decisions/0046-detached-string-contracts.md)): one selected intrinsic
String identity is sealed, fieldless and assignable to Object, sharing the catalog
limit with classes, interfaces and vectors. Numeric UTF-16 literal payloads preserve
embedded zero and unpaired surrogates. Literal, length, code-unit access, ordinal
equality and two-operand concat operations validate types and fault edges; literals
and concat receive allocation safepoints. Program-wide literal budgets bound data
and hashing work. String metadata and CIL now produce these contracts through
exact selected-library identity and member signatures
([ADR 0047](decisions/0047-string-metadata-and-cil.md)). Bounded literal decoding
preserves numeric UTF-16; fields, method/dispatch signatures, vectors and type tests
share the same selected identity. Length/indexer calls, static ordinal equality
and two-string concat lower directly without importing framework bodies. Type/module
initialization, unsupported overloads and broader String members remain diagnostics.
Native v9 stores immutable numeric UTF-16 buffers in the retained instance heap
([ADR 0048](decisions/0048-native-string-storage.md)). Header and buffer bytes share
existing limits. Literal identity, including canonical empty, is cached per instance;
nonempty concat creates a fresh object after checked sizing. All references use
phase A roots, and checked operations preserve fault order/origin. The selected
String still widens only to Object: metadata catalogs combining it with one of
its actual framework interfaces fail until that ancestry is modeled. Unrelated
user-interface dispatch with String signatures remains supported. The literal
cache is retained until teardown; its roots must join future collection tracing.
O0/O2 local sanitizer conformance is not evidence of collection or other platforms.
The detached array foundation is implemented
([ADR 0043](decisions/0043-detached-array-contracts.md)): zero-based one-dimensional
Int32/reference vector identities retain their element type separately from nominal
class layouts and share the 256-type catalog limit. Reference vectors support
nominal element covariance and widening to Object. Detached allocation, length,
load and store operations validate exact types and explicit fault edges; array
allocation uses the existing phase A root/safepoint contract. Runtime stores must
check the actual allocation's element type after null/bounds checks. SZARRAY metadata,
fields/signatures and bounded array CIL translation are implemented
([ADR 0044](decisions/0044-array-metadata-and-cil.md)), including exact array signatures
through class/interface dispatch. The `ldlen; conv.i4` pair stays within Int32;
general native integers remain unsupported. Element access after an unequal-reference
stack merge requires an explicitly typed array local. Native v8 stores vectors in
an owned header and a separate typed element buffer, charges both to the retained
heap budget, and checks null, bounds and the actual reference element type before
mutation ([ADR 0045](decisions/0045-native-array-storage.md)). Dynamic reference-slot
callbacks support inspection/future tracing. Phase A roots cover arrays and loaded
references across allocations; actual collection remains planned.
Structs with references have generated tracing descriptions.

The first detached value-layout contract is implemented
([ADR 0055](decisions/0055-detached-value-layouts.md)). It distinguishes inline
values from object references, rejects inline containment cycles, and derives
nested scalar copy paths and embedded-reference paths within explicit expansion
and depth limits. Bounded metadata discovery now supplies these layouts through
the exact selected ValueType identity and a shared metadata ledger
([ADR 0056](decisions/0056-value-layout-metadata.md)). Later milestone 5 slices supply
CIL struct translation. Inline value fields in heap objects remain a subsequent
compatibility gate outside the bounded milestone 5 profile.

Detached value flow now validates exact-type copies, pure default/field
operations, calls and joins using the shared IR verifier. Phase A planning
enumerates every embedded reference path and bounds expanded scalar storage
([ADR 0057](decisions/0057-typed-value-copies.md)). Mutable managed locations and
native lowering have separate lifetime, copying and byte-budget obligations.
Pure value copies now lower to the existing scalar/reference backend
([ADR 0058](decisions/0058-scalar-value-lowering.md)). Authored IR for copies,
nested updates and scalar-returning calls/joins has local O0/O2 sanitizer evidence.
This does not supply CIL value translation, struct-return packets, native mutable
managed locations or live embedded-reference collection stress.

Detached managed locations now name frame locals/by-value parameters, object
fields and nested inline fields ([ADR 0059](decisions/0059-managed-storage-locations.md)).
They retain an invariant element type and readonly restriction. Calls can borrow
and synchronously reborrow locations, but returning/storing locations or carrying
them through block joins is rejected pending further lifetime analysis. Loads copy
values; stores and initialization require writable storage and explicit fault
edges. Phase A plans root each location's heap owner while frame locations borrow
active backing storage. The original native entry points still reject locations. Native
location emission and CIL translation remain separate gates.

Private native v15 helpers now authenticate scalar carriers
([ADR 0060](decisions/0060-native-scalar-location-carriers.md)). Frame carriers use
nonreused registration tokens and bounded slot indices; heap carriers retain an
owner and exact field selector. Resolution checks active lifetime or ownership
before typed access, including readonly and reference compatibility. Frame slots
remain rooted by their lending frame, and heap owners require explicit roots.
Local O0/O2 sanitizer probes cover frame reuse, nested borrows and collection through
both origins. These runtime helpers do not yet emit location IR, whole-struct
loads/stores or struct-return packets.

Native v16 adds an explicit scalar location wrapper and emitter path
([ADR 0061](decisions/0061-native-scalar-location-ir.md)). Int32/reference locations
compose with existing allocating object operations while retaining field visibility,
readonly and synchronous lifetime rules. Carriers and argument packets live in
budgeted frames; each carrier's owner is separately rooted. Only lending frames
register borrow tokens. Local O0/O2 sanitizer tests compare authored borrow IR with
C#, stress collection in allocating callees and check fault cleanup. Mutable inline
value expansion, whole-struct returns and CIL translation remain separate work.

Mutable value expansion now uses the same scalar backend
([ADR 0062](decisions/0062-mutable-value-scalar-expansion.md)). Nested locals and
by-value parameters expand into typed slots; struct borrows expand into checked
leaf carriers. Whole loads capture snapshots before stores, preserving overlapping
copies. Explicit readonly views retain restrictions through nested field selection.
Expansion and output validation both enforce resource limits. O0/O2 sanitizer
probes compare authored mutations with C# and verify live embedded reference
identity/rooting in a private harness. Source value IR still excludes allocating
object operations; CIL translation, inline heap values and struct-return packets
remain unimplemented.

Authored value IR now supports caller-owned struct-return packets
([ADR 0063](decisions/0063-caller-owned-value-return-packets.md)). The caller reserves
typed result locals and lends their locations as hidden output arguments; the
callee copies its result into those slots before its frame ends. Embedded references
therefore have rooted destinations, and the caller snapshots the result only after
a successful call. Empty returns preserve calls without output storage. O0/O2
sanitizer probes cover chained/nested returns, live reference identity, ordered
faults and exact/insufficient arena budgets. CIL translation and allocating value
source programs remain separate gates.

Bounded CIL value discovery now supplies static method snapshots and resolved
operand identities ([ADR 0064](decisions/0064-value-cil-discovery.md)). Method and
layout discovery share a metadata budget, and field/call visibility is checked
against exact declaring owners and enclosing-type visibility. Int32/value signatures
and synchronous byref parameters are admitted; this metadata result is not yet
stack-verified or executable CIL.

The compiler-library `value-cil-v1` entry now translates bounded static value CIL
into the tested copy/borrow IR and native lowering
([ADR 0065](decisions/0065-bounded-value-cil-translation.md)). It supports local and
by-value parameter mutation, nested fields, synchronous byref parameters, whole
copies, exact stack joins, Int32 branches and struct returns. O0/O2 sanitizer
probes compare real C# return/borrow/loop programs with .NET. Type access, readonly
locations, original fault offsets and expanded IR budgets are checked. Borrowed
locations across joins, instance struct methods, standalone reference signatures
and allocations in value source programs remain outside this profile. Existing
CLI profiles are unchanged; live-object allocation/collection composition for
CIL values is the next gate. Earlier value paragraphs record the staged contracts.

An opt-in value/vector IR wrapper now composes existing array operations with
inline copies, borrows and rooted return packets
([ADR 0066](decisions/0066-value-array-composition.md)). Shared object rules validate
array operations and static calls against value signatures; allocation points
retain embedded roots. This authored-IR checkpoint precedes CIL allocation and
native live-reference stress. Class allocation/constructors remain separate.

The compiler-library `value-arrays-cil-v1` gate now adds Int32 vector allocation,
access and signatures to static value programs
([ADR 0067](decisions/0067-value-array-cil-and-collection.md)). It authenticates
allocation element identity against the selected runtime and checks exact array
operands, fault edges and paired length conversion. O0/O2 sanitizer probes collect
before each allocation while nested struct copies, borrows, parameters and return
packets retain live arrays. Sole-root transfer, final reclamation, insufficient
heap/arena capacity and nested allocation failure/recovery have local evidence.
Class allocation/constructors composed with values remain a separate gate.

An explicit value/object IR profile now composes class construction and fields
with static value methods ([ADR 0068](decisions/0068-value-object-composition.md)).
It reuses base-initialization, readonly and access checks, plans receiver/embedded
roots and preserves constructor bindings through scalar expansion. Instance
methods and value/byref constructor arguments remain gated; CIL object/value
composition and native live-reference evidence follow this IR checkpoint.

The compiler-library `value-objects-cil-v1` gate now supplies class/value CIL
composition ([ADR 0069](decisions/0069-value-object-cil-and-collection.md)). Static
value methods can allocate class hierarchies, access inherited scalar/reference
fields and borrow checked scalar heap fields. Shared source and target validation
checks base initialization, access and readonly writes. Constructor bodies require
scalar/reference operations throughout; their value/borrow flow remains gated.
Discovery shares bounded metadata snapshots with layout construction. Local O0/O2
sanitizer probes preserve live object graphs through nested struct copies, borrowed
replacement and caller-owned returns, including allocation inside constructors,
sole-root transfer and failure cleanup. Existing CLI profiles are unchanged.

The detached `boxed-value-ir-v1` gate now distinguishes fresh box allocation,
exact unbox copies and synchronous mutable payload borrows
([ADR 0070](decisions/0070-detached-boxed-value-ir.md)). It reuses named-struct
layout paths and embedded-reference maps. Boxes retain exact type/image identity,
including empty payloads, and payload borrows retain an owner root. All operations
have explicit fault exits; box operations are allocation safepoints. Shared
readonly, escape and resource checks apply. This is authored IR validation and
root planning only; native storage, executable copy/identity/fault semantics and
CIL boxing remain planned. Boxed interface dispatch must preserve the distinction
between modifying boxed storage and copying its value.

Private native boxed-payload helpers now provide typed storage and exact descriptor
checks ([ADR 0071](decisions/0071-native-boxed-payload-helpers.md)). Their allocator,
reference tracing and scalar borrows reuse the existing runtime. Whole payload
copies validate all reference slots before any write. Local O0/O2 sanitizer probes
compare copy isolation and mutation with .NET and verify fresh empty identity,
borrowed-owner collection roots, cyclic graphs, failure cleanup and heap budgets.
`EmitBoxedRuntime` is a helper conformance entry point with a shared scalar/value
reference catalog. It does not lower boxing IR. Connecting operations to these
helpers, preserving empty borrowed owners during expansion and generated fault
provenance remain planned; so do CIL boxing and ValueType/interface views.

Authored boxed IR now lowers through `LowerBoxes` and executes through `EmitBoxes`
([ADR 0072](decisions/0072-native-boxed-ir-emission.md)). Exact scalar packets and
typed payload locations use the private helpers. Every unbox retains a rooted
checked owner, including empty payloads forwarded through allocating calls.
Expanded storage/root budgets are rechecked, packets use execution-arena storage,
and generated faults preserve original method/IR offsets. Local O0/O2 sanitizer
evidence covers copy/mutation parity with .NET, embedded reference identity,
null/type faults, allocation recovery and exact/insufficient arena capacity.
CIL boxing, ValueType reference views and boxed interface dispatch remain planned.

The compiler-library `boxed-values-cil-v1` gate now translates named nongeneric
struct `box`, `unbox` and value-form `unbox.any`
([ADR 0073](decisions/0073-bounded-boxed-value-cil.md)). Exact type operands and
access checks feed the same copy/borrow IR and native path. Fieldless sequential
C# structs with the standard one-byte size record map to empty logical payloads;
custom sized or packed layouts remain rejected. Local O0/O2 sanitizer probes
verify graph lifetime, copy/return semantics, borrowed mutation, fault provenance
and allocation recovery. Primitive/enum/nullable boxing, reference-form unboxing,
generic TypeSpec operands, ValueType views and boxed interface dispatch remain
unsupported. Existing CLI profiles are unchanged.

## 8. Garbage collection

The bounded baseline collector is implemented as precise, non-moving mark-and-sweep with one managed execution
thread per runtime instance. The compiler identifies actual references; the
collector does not guess from arbitrary memory. Before the multithreaded
profile, parallelism uses independent runtime instances
([ADR 0001](decisions/0001-execution-and-threading-model.md)). Each thread
context owns its shadow stack, and the IR marks poll points, native transitions
and heap reference stores so stop-the-world collection and write barriers can
be added later.

Generated methods register reference-bearing locals and temporaries in a shadow
stack, first for every reference and later only for references live across
safepoints ([ADR 0002](decisions/0002-gc-root-tracking.md)). Trace statics,
retained host handles and pending exceptions too. Collect at controlled
allocation points; values needed across allocating calls must already be
rooted. Reference-bearing structs require recursive field tracing.

Private native v10 visitors now enumerate the current context's published slots,
instance-local literal roots, inherited reference fields and reference-array
elements ([ADR 0049](decisions/0049-precise-reference-visitation.md)). They return
borrowed provenance, validate contained reference ownership/descriptor identity,
and distinguish complete, stopped and invalid walks without changing runtime state.
They allocate no memory and do not recurse into child objects. Only complete walks
can feed a reclamation decision. The allocation list, integer storage and
string code units are not roots. Native v11 adds bounded iterative marking using
separate header scratch ([ADR 0050](decisions/0050-bounded-native-marking.md)). It
validates the inventory, marks each discovered object once and reports reachability
only after a complete trace. Object/edge limits and scoped cleanup handle failure;
no marks remain valid across a return to managed execution. Larger headers consume
the existing heap byte quota. Native v12 adds guarded sweeping, exact allocation charges and one collection/retry
on allocation pressure ([ADR 0051](decisions/0051-native-reclamation.md)).
Native v13 adds collection before allocation and poisoned reclamation checks
([ADR 0052](decisions/0052-collection-stress.md)); v14 adds instance-owned strong
handles with nonreused token generations ([ADR 0053](decisions/0053-instance-retained-handles.md)).
Active handles join frame and literal roots, with at most 1,024 lazy native slots.
The [milestone 4 record](milestones/04-collection-and-handles.md) consolidates the
bounded completion evidence. Multiple active contexts within one instance require
a future registration/coordination contract.

Interior managed references retain their owner. Non-moving collection preserves
addresses but does not eliminate owner tracking or stack-lifetime checks.

Required diagnostic modes: collect before every eligible allocation, verify the
heap after collection, report roots/retention paths, count allocations by type,
and reject stale host handles. Later generational collection, write barriers
and root-liveness improvements must pass the same stress suite. Finalizers,
weak references and concurrent collection are separately staged features.

## 9. Exceptions

The primitive profile currently propagates division/remainder and checked-arithmetic faults
through a private context and returns status/origin at its native boundary
(ADRs 0019–0021). The opt-in bounded managed exception profile now supplies
rooted objects, messages and handler execution; the broader design below remains staged.

First support try/catch/finally, throw and rethrow. Exceptions propagate
explicitly through a pending-exception slot in the execution context, with the
same mechanism on native and WebAssembly targets and no C++ exceptions
([ADR 0004](decisions/0004-exception-handling.md)). Managed identity, handler
selection and finally execution are Cilica responsibilities. Root registrations
must unwind correctly.

Managed exception metadata now retains validated bounded region inputs and catch
identities ([ADR 0081](decisions/0081-managed-exception-inputs.md)). Its type-test
classifier requires acyclic flow and local assignment before every read on all paths.
Detached exception IR now defines checked throw/rethrow/leave, handler/cleanup/filter
regions and saved-identity roots ([ADR 0082](decisions/0082-managed-exception-ir.md)).
Native v17 now traces pending and emergency exception roots, with checked private
payload/message and suspend/restore helpers ([ADR 0083](decisions/0083-native-exception-storage.md)).
An emergency allocation is reserved within the instance quota. Native dispatch and
lazy invariant messages are implemented by ADR 0084. Bounded CIL translation now
composes those handlers with values, generic boxes and delegates (ADR 0085), including
verified forwarding constructors and contextual class-cast/unbox messages. General
filters, broader exception APIs and the public structured-error boundary remain planned.

Exception filters are staged. Filters that only test the exception's type are
supported with basic exceptions, because their evaluation order is
unobservable; Alchemy uses them. General filters need full two-pass search and
unwind ordering and are required for the first public release. Until then they
produce diagnostics. Catch managed exceptions at every exported boundary;
return structured errors rather than unwinding into Swift/Kotlin/JavaScript.

The authored `delegate-values-ir-v1` contract now defines intrinsic single-target
delegate identities, exact static/instance captures and Int32/void invocation
([ADR 0078](decisions/0078-detached-delegate-ir.md)). Captured receivers remain
managed references, target methods enter the reachable closure and allocation/call
safepoints retain the necessary roots. `LowerDelegates`/`EmitDelegates` now provide
checked native storage and invocation ([ADR 0079](decisions/0079-native-delegate-execution.md)).
O0/O2 sanitizer probes cover .NET parity, captured roots, ownership checks, bounded
argument packets and fault cleanup. The explicit CIL delegate profile now binds
public named nongeneric declarations, adjacent ldftn/newobj and callvirt Invoke
([ADR 0080](decisions/0080-bounded-delegate-cil.md)), including storage in boxed generic
values and class fields. The v2 CIL profile now also admits exact closed generic
delegates and the owned Action<int,int> contract, retaining distinct specialized
identities and checked declaration variance. Exact type tests/casts are supported
in the integer-object composition ([ADR 0136](decisions/0136-closed-generic-delegates.md)).
Private compiler-generated closures, virtual capture, multicast, variance
conversions and broader signatures remain unsupported.

## 10. Generics and reachability

Initially specialise each reachable closed generic type and method. Distinct
implementations for List<int> and List<Card> trade code size for inspectability.

Roots are exports and explicit preservation declarations. Expand through calls,
constructors, static initialization, virtual/interface dispatch, delegate
targets, generic instantiations and preserved metadata until reaching a fixed
point. New types can make new virtual implementations reachable.

The compiler-library `closed-generic-values-cil-v1` gate now specializes
unconstrained static methods on nongeneric owners in value-containing closures
([ADR 0074](decisions/0074-closed-generic-method-specialization.md)). Closed caller
environments substitute signatures, locals and operands before existing validation
and lowering. Native symbols use an additional specialization key while faults
retain original image/MethodDef/IL provenance. Closed generic layouts, constraint
validation and broader generic operations remain later gates.

The same opt-in generic profile now adds unconstrained closed struct layouts and
static methods on closed owners ([ADR 0075](decisions/0075-closed-generic-value-layouts.md)).
Field and box identities include the closed owner, and nested layouts retain the
shared scalar/root and expansion limits. O0/O2 native probes establish generic
struct copy/return/borrow/boxing behavior under collection. Constraints and generic
class allocation/dispatch remain separate gates.

Scalar/reference-only generic closures and the common `class`, nonnullable
`struct`, and public parameterless-constructor constraints now have bounded
metadata validation and native differential evidence
([ADR 0076](decisions/0076-generic-special-constraints.md)). Constraint admission
never executes constructors and does not implement generic activation helpers.
Exact nominal interface constraints are now validated after closed substitution
through bounded interface and base ancestry; primitive unmanaged constraints
require the selected marker. Class/dependent constraints, array helpers and
variance-only constraint conversions remain diagnostics
([ADR 0128](decisions/0128-exact-interface-constraints.md)).

Concrete closed class hierarchies, scalar/reference fields and exact constructors
now share the same specialization and runtime path
([ADR 0077](decisions/0077-closed-generic-reference-layouts.md)). Their native probes
compose cyclic generic objects with struct returns, boxing and borrowed fields.
Inline struct fields in classes, generic interfaces/dispatch and broader generic
constraints remain unsupported by this bounded gate.

Pathological generic expansion yields a diagnostic with an instantiation chain,
not a compiler hang or omitted code. Generic sharing and runtime generic
dictionaries are later optimisations. Closed interface snapshots now validate
variance flags independently from special constraints
([ADR 0101](decisions/0101-closed-interface-variance-declarations.md)). The explicit
integer-object profile now admits closed reference variance and nested array
assignment through a bounded type-relation closure, after declaration-position
validation ([ADR 0105](decisions/0105-closed-interface-assignments.md)). Value
arguments remain exact. An opt-in metadata resolver selects exact closed
interface mappings and class slots ([ADR 0106](decisions/0106-exact-closed-interface-targets.md));
variance-only targets and array helpers remain pending.
Closed scalar/reference dispatch plans now preserve those mappings and signatures
through the shared preparation session
([ADR 0108](decisions/0108-closed-interface-plans.md)). The explicit integer-object
CIL profile now repeatedly discovers exact scalar/reference interface calls and
their executable implementations through shared metadata plans
([ADR 0109](decisions/0109-closed-interface-cil.md)).
Exact closed class target selection now shares the bounded slot algorithm and
closed owner validation
([ADR 0110](decisions/0110-closed-class-virtual-targets.md)). Scalar/reference
class virtual calls now retain exact maps through composed discovery, value
lowering, rooting and managed fault handling
([ADR 0111](decisions/0111-composed-class-dispatch.md)).

Selected UTF-16 indexing, two-string concatenation and inequality also compose
through this profile's value and exception lowering, using the existing runtime
operations ([ADR 0112](decisions/0112-composed-string-operations.md)).

Selected argument-exception constructors and parameter/message access now retain
traced parameter names and exact invariant resources in private native v19
([ADR 0113](decisions/0113-argument-exception-payloads.md)). Inner exceptions,
actual-value constructors, localization and broader exception APIs remain pending.

Exact class virtual calls also carry inline copies, rooted returns and bounded
borrow parameters in the explicit integer-object profile; by-value starg writes
use the existing checked parameter storage
([ADR 0114](decisions/0114-class-virtual-value-signatures.md)). Exact interface
calls now use the same signature expansion, with checked value layouts retained
in their dispatch plans ([ADR 0115](decisions/0115-interface-value-signatures.md)).
Same-image public/internal calls also use bounded declaring-chain visibility
facts; both discovery and detached validation enforce the same access rule
([ADR 0116](decisions/0116-same-assembly-method-access.md)). Broader protected,
friend-assembly, field and type-operand access remain separate gates.
Closed generic memory operations now preserve exact substituted storage for
integer, reference and inline-value operands; defaults clear the existing checked
locations ([ADR 0117](decisions/0117-closed-generic-memory-operations.md)). Narrow
memory operands still require width-aware carriers.
Class constructors also carry inline arguments and borrows through the same
lowering. Value computations before the base call must remain independent of the
uninitialized receiver; straight-line initialization and the immediate-base check
remain ([ADR 0118](decisions/0118-class-constructor-value-signatures.md)).
Closed reference boxing preserves identity and unbox.any uses checked casts,
including interface casts with bounded diagnostic identities
([ADR 0119](decisions/0119-closed-reference-boxing.md)). This does not admit
reference operands to unbox or make interfaces allocatable.
Direct nonvirtual struct instance calls now borrow their exact inline receiver,
preserving mutation, copied returns and bounded receiver flow through branches
([ADR 0120](decisions/0120-direct-struct-instance-calls.md)). Readonly receivers and
value virtual/interface dispatch remain separate capabilities.
Interface-bearing values now retain exact declared contracts for direct calls to
their admitted bodies, including methods marked virtual/final/newslot. This does
not admit constrained calls or boxed value dispatch; boxing such values remains
a diagnostic ([ADR 0121](decisions/0121-direct-interface-value-declarations.md)).

## 11. Class library and assembly identity

Explicit library mappings preserve generic constraint sets structurally before
closing type/method arguments. Projection retains named identities and modifiers,
checks both variable environments and rejects changed or malformed obligations
([ADR 0129](decisions/0129-library-constraint-correspondence.md)). Matching
declarations does not replace executable constraint proof or grant intrinsic
authority.

Closed generic declarations may carry AllowByRefLike when their actual arguments
are ordinary supported types. Stack-only values/arguments remain explicitly
rejected through bounded marker inspection, and library mappings cannot erase
their marker ([ADR 0125](decisions/0125-ordinary-arguments-with-byreflike-permission.md)).
This permits direct IEquatable value bodies against the selected runtime without
claiming ref struct or constrained-dispatch support.
Exact constrained value targets can now be selected as metadata evidence, using
shared interface/constraint validation and direct Object/ValueType override rules
([ADR 0126](decisions/0126-constrained-value-targets.md)). Exact adjacent
constrained./callvirt CIL now calls admitted bodies through checked writable
receiver borrows, without boxing or class slots
([ADR 0127](decisions/0127-constrained-direct-value-cil.md)). Boxing/default
fallbacks, private bodies across owners, readonly and primitive/reference
receivers remain diagnostic.

Milestone 6's `integer-arithmetic-ir-v1` profile adds authored Int64 scalar IR
and C++20 arithmetic with typed calls, locals and block edges
([ADR 0093](decisions/0093-int64-scalar-ir.md)). UInt64 storage, guarded signed
operations and explicit widening/truncation preserve integer bits and faults.
O0/O2 UBSan conformance matches .NET. `integer-arithmetic-cil-v1` now translates
static mixed-width scalar closures with explicit conversions and checked stack
widths ([ADR 0094](decisions/0094-integer-cil-closures.md)). Checked and unchecked
integer conversions, including narrow stack results, have boundary conformance
([ADR 0095](decisions/0095-integer-conversion-semantics.md)). Narrow primitive
parameters, locals and returns normalize to their declared storage width
([ADR 0096](decisions/0096-narrow-scalar-storage.md)). Enum identities and
array integration remain pending. Explicit wide object catalogs
now admit Int64 class fields, typed call frames and checked borrows under native
v18 ([ADR 0097](decisions/0097-wide-object-storage.md)). Int64 leaves also survive
inline copies, return packets and boxed
payloads ([ADR 0098](decisions/0098-wide-inline-and-boxed-values.md)). Catalogs
without that capability retain their existing admission rules. The explicit
`integer-managed-objects-cil-v1` API composes those capabilities with wide CIL,
closed generic scalar fields and managed exceptions
([ADR 0099](decisions/0099-wide-managed-object-cil.md)); wide arrays and narrow
heap storage remain pending. Closed nominal reference vectors now compose with
generic fields, value flow, managed faults and array casts in that explicit profile
([ADR 0100](decisions/0100-composed-reference-array-cil.md)). Exact struct
constructors now initialize checked inline receiver locations; readonly writes
are limited to the declaring constructor before private scalar-call lowering
([ADR 0102](decisions/0102-inline-value-constructors.md)). Narrow scalar
parameters, locals and returns also normalize in the composed profile, with
direct constructor receiver reloads across conditional branches
([ADR 0103](decisions/0103-composed-narrow-scalar-boundaries.md)). Narrow
heap/borrow storage remains separate. Nonvirtual class instance calls compose
value/borrow signatures and caller-owned returns while preserving `callvirt`
null checks ([ADR 0104](decisions/0104-composed-class-instance-calls.md)).
Explicit composed IR can also retain scalar/reference interface plans through
value lowering and managed exception routing
([ADR 0107](decisions/0107-composed-interface-ir.md)). Exact scalar/reference
closed CIL calls use this path; struct dispatch signatures remain pending.
Array catalogs that mention interfaces declared by the selected Array base or
runtime-supplied generic vector contracts now fail before execution until those
relationships and helpers are implemented. Exact runtime identities prevent
application lookalikes from acquiring intrinsic treatment (slice 6c.16a).

The class library is a major subsystem. Managed compatibility-library code
compiles through Cilica; allocation, runtime type inspection and essential
operations use explicit compiler intrinsics. Shared library source is ported
from a pinned dotnet/runtime release, with runtime-coupled types written for
Cilica ([ADR 0003](decisions/0003-class-library-strategy.md)). `Unsafe`
operations and interior references are permitted only in trusted Cilica
library assemblies.

An initial audit determines members needed by the kernel and Alchemy. Inspection
of the existing game showed dictionaries, sets, delegates, type-based dispatch,
enum validation and custom equality. This is not a complete dependency audit.

Milestone 6a adds explicit library boundaries for planning
([ADR 0088](decisions/0088-explicit-library-contract-inventory.md)).
`audit --contract-only` retains declarations, signatures, ancestry and dependency
reasons while deferring selected images' bodies, layout fields and initializers.
It selects full identities and hashes, without granting intrinsic trust or
implementation compatibility. The separate inventory profile never clears
general-filter absence; the trial ported implementation closure remains required.

Slice 6b.1 adds explicit type-correspondence plans and a pinned
[reference/source baseline](library-baseline.json)
([ADR 0089](decisions/0089-library-type-binding-foundation.md)). Plans validate
exact image/type identities, headers and immediate bases, and project structural
types without changing ordinary member binding, audits or compilation. They grant
no intrinsic privileges. Slice 6b.2 adds explicit method/field correspondence
([ADR 0090](decisions/0090-library-member-binding.md)), comparing structural
declarations before generic substitution and retaining both source and target
evidence. Slice 6b.2a provides an explicit compilation view over these pinned
snapshots, leaving its source resolver strict
([ADR 0091](decisions/0091-library-compilation-views.md)). A small native reference-
call probe passes. Slice 6b.3 validates independently approved build attestations
and exact string/exception intrinsic grants at the compiler boundary
([ADR 0092](decisions/0092-library-build-admission.md)). The host must pin the
manifest digest independently of compiler input. A separate local build tool now
captures actual Roslyn inputs/arguments and emitted image evidence without granting
approval ([ADR 0123](decisions/0123-library-build-evidence-producer.md)). Its explicit
core profile builds without platform references; bounded intrinsic requests are
hashed into build options and resolved to exact emitted declarations, still
requiring independent approval
([ADR 0130](decisions/0130-core-build-and-intrinsic-requests.md)). Explicit
mapped implementation audits now retain both declarations, constructed signatures,
image hashes and any approved build digest while expanding implementation bodies
([ADR 0124](decisions/0124-mapped-library-implementation-audits.md)). The actual
required-library closure remains a milestone gate. The first source import now adapts
the pinned decimal integer formatting algorithm into a checked managed helper
([ADR 0122](decisions/0122-managed-decimal-formatting-trial.md)). Differential
native evidence covers text only; System numeric member mapping and string-cache
identity are not claimed.

The zero-reference Cilica.Core bootstrap now supplies owned runtime declarations
and explicit intrinsic requests. Exact mappings to the pinned reference image
execute string/exception probes and the managed formatter through native v19
([ADR 0131](decisions/0131-owned-core-library-bootstrap.md)). The selected invariant
provider now has stable per-instance identity in native v20, with an independently
authorized fieldless layout, permanent GC root and checked ownership
([ADR 0132](decisions/0132-invariant-provider-identity.md)). Selected 32/64-bit integer instance bodies now use checked scalar receivers;
owned GetHashCode overrides execute as ordinary managed code
([ADR 0133](decisions/0133-primitive-instance-bodies-and-hashing.md)). The four integer
ToString(IFormatProvider) overloads now execute that managed decimal helper when
calls directly prove the invariant provider. Native v21 preserves shared [0, 300)
string-cache identity with private exact grants and instance-owned traced storage
([ADR 0134](decisions/0134-invariant-integer-formatting-apis.md)). Other culture
members, general formatting and provider proofs through aliases remain pending. The image is never
loaded into the host CLR. Unsupported declarations, including string hashing,
remain diagnostic; general CoreLib and kernel compatibility are not claimed.

The owned core also preserves the pinned read-only collection and enumeration
contracts, including interface inheritance and covariance. Exact mapped dispatch
is tested on application collections with Int64 and reference elements; library
collection storage, array helpers and variance-only dispatch remain pending
([ADR 0135](decisions/0135-owned-read-only-collection-contracts.md)).

The integer-object compiler admits readonly static reads only when exact metadata
proves zero initialization: no type/module initializer or explicit field storage.
Reads become ordinary scalar zero, null or fresh inline defaults; instance layouts
omit these static declarations. Mutable/initialized statics, addresses and writes
remain unsupported ([ADR 0137](decisions/0137-default-readonly-static-fields.md)).

Recognised framework types map to canonical runtime types. Resolve supported
facades and type forwarders deliberately. User types retain assembly-qualified
identity; never match solely by a short type name.

Pin a tested reference-assembly profile. .NET Standard 2.1 is important because
the existing kernel and game engine target it, but that label does not promise
every API. Each supported member needs behavioural tests or a documented
dependency on tested primitives. Record source provenance for reused library
code; do not assume an existing entire CoreLib can be transplanted unchanged.

## 12. Native embedding API

Expose a versioned C ABI; keep C++ implementation details private. Conceptual
operations for the full SDK (not yet implemented as a complete API):

```text
runtime_create(config)
runtime_invoke(runtime, export_id, request)
runtime_release_handle(runtime, object_handle)
runtime_destroy(runtime)
buffer_release(buffer)
```

Milestone 7a now implements a bounded persistent Int32 C boundary
([ADR 0178](decisions/0178-persistent-scalar-c-abi.md)). `EmbeddingCppEmitter`
emits a C header and C++ source for one validated export, with version negotiation,
create/invoke/destroy, nonreused opaque runtime tokens and copied fault origins.
Instances reuse the existing heap/static roots; each call has fresh execution
frames. A bounded registry rejects overlapping calls and destruction. The
[ABI contract](embedding-abi.md) records exact widths, ownership, status, version
and resource rules. A separate C11 consumer links an emitted shared library and
passes local O0/O2 sanitizer lifecycle tests. Subsequent slices below extend this
foundation; structured value/error copying and device evidence remain planned. [Milestone 7](milestones/07-embedding-and-phone-libraries.md) puts iOS first,
then Android; neither phone platform is yet verified.

Milestone 7b adds a v2 shared scalar export module
([ADR 0179](decisions/0179-shared-export-module.md)). An explicit manifest assigns
stable application IDs to public static Int32 methods in one validated closure.
All exports share the instance's heap/statics; expected interface fingerprints,
logical signature checks, capability bits and lowerable per-instance quotas are
implemented. Separate packet helpers keep dispatch stack storage bounded at 64
exports. V1 is preserved. Independent multi-root discovery, value/string copying,
structured exception copying and phone execution remain separate gates.

Specify widths, calling conventions, buffer lifetime/ownership, structured
errors, handle validity, thread entry, version negotiation and capabilities.
Milestone 7c.1 integrates the existing strong-handle roots into a typed v3 API
([ADR 0180](decisions/0180-public-retained-object-handles.md)). Exports admit Int32,
reference handles and void results. Handles identify independent runtime-owned
leases; zero means null. The shared admission guard covers invoke, retain, release
and destroy. Inputs are checked for ownership and assignability before managed
execution. Reference results reserve a handle slot before execution and publish
without a managed allocation after return. Released/foreign tokens fail; cloning
creates a distinct lease to the same object. V1/V2 remain available. Local C11 and
O0/O2 sanitizer checks cover collection, aliases, cycles, quotas and release races;
this does not establish phone support or a general value/exception SDK.

Milestone 7c.2 adds optional UTF-16 import/copy to v3
([ADR 0181](decisions/0181-host-owned-utf16-copying.md)). A declared alias must bind
the closure's exact String type; enabling it changes the contract fingerprint and
adds a capability bit. Imports copy host-owned UInt16 units into managed immutable
storage and return retained handles. Copies query length or fill a caller-owned
buffer without partial writes or a terminator. Embedded zero and lone surrogates
are preserved; null handles remain distinct from empty strings. Both operations
use the same instance admission and quotas. No additional external buffer-release
API is needed. Local C11/O0/O2 sanitizer and race evidence does not establish phone
support. Wide/value copying and richer exception transfer remain open.

Milestone 7c.3 adds Integer64 export shapes and capability bit 512
([ADR 0182](decisions/0182-exact-integer64-embedding.md)). The existing tagged record
carries all 64 bits through native wide argument slots and bit-cast results. Logical
widths are checked before lowering; no Int32 or struct coercion is implied. The IR
uses the same storage type for signed and unsigned integers, so the public kind
specifies bits rather than declaration signedness. Managed operations keep their
signed/unsigned semantics. Local C11 O0/O2 comparisons cover wide limits, values
above 2^53, overflow, division and mixed handle/scalar calls. Structured values,
rich exception copying and phone execution remain open.

Milestone 7c.4 adds optional managed-exception capture
([ADR 0183](decisions/0183-retained-exception-capture.md)). `invoke_capture` returns
the ordinary result plus a strong exception lease for supported escaping managed
failures. One pre-execution reservation serves either a returned object or an
exception; successful scalar/void calls cancel it. Fatal faults may have no
managed object and still retain copied code/origin. `exception_copy` uses caller-
owned UTF-16 buffers and a rooted existing formatter to copy type/message and
source declaration identity without reexecuting an export. Formatting failures
leave the original host fault report intact. Enabling capture changes the manifest
hash and adds capability bit 1024; ordinary invoke behavior remains available.
Local C11/O0/O2 sanitizer and race evidence covers ownership and quota failures.
Structured application values, inner traversal, stack traces and phone execution
remain outside this slice.

Serialize calls on a dedicated runtime worker. Queue callbacks to the host;
initially reject reentrant managed calls. In the async profile, the worker
drains the instance's continuation queue, and timers ask the host to schedule
wake-ups rather than blocking the worker. Runtime destruction settles pending
work and releases owned resources; it does not promise safe dynamic library
unloading by the operating system.

## 13. Expo and web integration

Swift and Kotlin Expo modules invoke the C ABI using small native adapters.
[Expo Modules](https://docs.expo.dev/modules/overview/) supplies host integration;
Cilica supplies managed execution. Native integration requires a custom Expo
build rather than adding arbitrary native code to stock Expo Go.

Exports generate stable identifiers, dispatch metadata, TypeScript types,
serialization and validation. Proposed application API:

```typescript
const runtime = await createRuntime();
const match = await runtime.matches.create(settings);
const update = await match.submit(command);
await match.dispose();
await runtime.dispose();
```

JavaScript promises initially represent native work scheduling. In the async
profile, an exported method returning `Task` or `Task<T>` returns a promise that
settles when the task completes. Define exact encodings: never silently round
64-bit integers into JavaScript numbers. Specify strings, enums, nullable fields
and schema versions. Start with inspectable JSON; add binary encoding only after
measuring overhead.

On web, a worker owns the WASM runtime behind the same contract. Rendering,
animation and camera movement stay outside managed execution. Bundle all runtime
and content assets needed for offline startup; persistent application state is
separate from the runtime heap and from retained object handles.

## 14. Alchemy validation

The validation scope is everything an offline game needs: Alchemy's
.NET Standard 2.1 assemblies, covering the turn kernel, rules engine, content
model, player-view projection and bots. The game server stays a separately
hosted .NET service and does not set Cilica requirements.

Use the existing engine boundary:

```text
Step(state, command, rules, catalogue) -> new state, events, rejection
```

Execute fixed transcripts and compare canonical state/events with .NET. Progress
from a synthetic kernel example to real TurnEngine.Core handlers, a selected
Alchemy rules scenario, hotseat/player-view projection, then phone termination
and restore. Host setup/content/serialization adapters are allowed. Altering
rules to hide runtime defects is not; record compatibility accommodations.

Game save formats remain application-owned. Object handles and heap snapshots
are not durable game persistence contracts.

## 15. Correctness and measurements

Differential tests run the same program on .NET and Cilica and compare results,
mutations, exception ordering/types, dispatch, generics and specified identity.
Do not compare unspecified details such as raw addresses or assume universally
guaranteed dictionary enumeration order.

Generate small valid programs and shrink failures to minimal reproducers.
Exercise optimisation levels and allocation stress against the same corpus.
Use native sanitizers as additional evidence, not a replacement for semantic
tests. Every fixed compiler bug gains an independent reduced regression case.

Benchmark appropriate .NET configurations and NativeAOT where supported. Report
cold start, steady-state execution, peak memory, collection pauses, binary size,
build duration and bridge overhead separately. Record inputs, hardware and
toolchain versions. Set performance targets after baselines; do not invent
speedup claims.

The proposed [benchmarking and improvement plan](benchmarking-and-improvement-plan.md)
extends the existing local execution-frame harness with NativeAOT and Unity
IL2CPP adapters, artifact inspection and measured optimization experiments.
It separates library execution from host costs and treats standalone IL2CPP
extraction as an optional feasibility investigation. These comparisons and their
resulting improvements remain planned, not measured capabilities.

Performance priorities follow the primary use: embedded engines in Expo apps on
iOS and Android. In order, optimise:

1. Cold start, from application launch to a runtime ready for its first call.
2. Per-call latency across the JavaScript, native and managed boundary,
   including serialization.
3. Binary size of the compiled library and runtime.
4. Peak memory and collection pauses, which cause visible stutter.
5. Steady-state throughput, which matters for heavy work such as AI search.

Optimisation effort goes first to bridge design, startup work and code size,
then to code-generator throughput. The throughput goal on native targets is a
measured factor of NativeAOT, not parity. On the web, where NativeAOT has no
official target, Cilica aims to compare favourably with the .NET options that
do run in browsers. These are aims, not claims, until baselines exist.

## 16. Milestones and exit gates

| Milestone | Deliverable | Evidence |
| --- | --- | --- |
| 1 | Assembly explorer | Stable metadata/dependency/IL output; malformed-input diagnostics |
| 2 | Primitive executable compiler | Differential control-flow and arithmetic tests |
| 3 | Objects, arrays, strings, dispatch | Correct identity, casts and mutation |
| 4 | Precise collection and handles | Cycles reclaimed; live values survive stress |
| 5 | Structs, boxing, generics, delegates, exceptions, type-test filters | Semantic edge-case regressions |
| 6 | Required library subset and real turn kernel | Equivalent deterministic traces |
| 7 | C ABI and phone libraries | Repeated-call/lifecycle tests on iOS and Android devices |
| 8 | Expo bindings and offline Alchemy slice | Start, play, terminate, restore without network |
| 9 | Browser WASM parity | Equivalent native/browser command traces |
| 10 | Single-threaded async profile | Differential async ordering and exception tests; exported tasks settle as host promises on native and web |
| 11 | Documented public release | A second developer reproduces a clean build; general exception filters pass differential tests; stage 3 readiness review passes |
| 12 | Multithreaded profile (native first) | Concurrency stress and ThreadSanitizer runs; differential results for race-free programs |

Milestone plans and slice status live in [docs/milestones](milestones/), starting
with [milestone 1](milestones/01-assembly-explorer.md). The bounded Int32 checkpoint
is complete in the [milestone 2 record](milestones/02-primitive-execution.md), with
a [reproducible native example](../examples/Primitive/README.md). Milestone 3 is
complete for its explicitly bounded retained-heap profile, with a
[composed object example](../examples/Objects/README.md) covering arrays, strings,
inheritance and dispatch through the compiler API. Its .NET oracle and metadata-only
compiler run in separate processes. The [milestone 3 record](milestones/03-object-execution.md)
consolidates the supported boundary; [milestone 4](milestones/04-collection-and-handles.md)
is complete for bounded non-moving collection, phase A stress and private retained
handles on the locally tested macOS ARM64 target. [Milestone 5](milestones/05-managed-semantics.md)
is complete for bounded compiler-library values, boxing, closed generics, single-target
delegates and managed exceptions/type-test filters (ADR 0087), with 1,855 passing local
checks. [Milestone 6](milestones/06-library-and-kernel.md) is complete for its bounded
owned-library and real-kernel closure on macOS ARM64: 10,752 exact native/.NET
transcript observations pass at O0/O2, with normal and forced collection under
sanitizers. The exact compiled/source-body audit resolves the filter gate for that
root; general filters remain unsupported. The broader runtime, public embedding
API and untested platforms remain planned.

The [parallel Alchemy compatibility plan](milestones/06-library-and-kernel.md#parallel-alchemy-compatibility-implementation-plan)
tracks the audited language/library gaps as AC0–AC10 delivery slices, separately
from milestone numbering. It preserves milestone 6's bounded completion and lets
milestone 7 embedding/device work continue against its supported kernel. The full
offline Alchemy and browser scenarios join the native replay gate and then require
their own target evidence. AC0 is complete with an integrated baseline, assigned gap ledger and public
regression probes; the later compatibility slices remain planned. Target claims
still require their own native/device evidence.

These are capability gates, not calendar estimates. Each leaves a demonstrable
result even if later stages take longer. Public release is a future milestone,
not authorization to publish unfinished private material automatically.

## 17. Portfolio presentation

Provide a reproducible path showing C# -> IL -> typed IR -> C++ -> native/WASM,
with .NET comparison and heap/performance reports. A proposed `explain` command
traces a method through compilation. `why-included` explains binary reachability.

Technical write-ups should show precise C++ roots, reference-bearing structs,
exception search/unwind ordering, generic specialisation and defined arithmetic.
Correctness evidence and inspectable outputs distinguish compiler engineering
from a wrapper demonstration.

## 18. Repository, resources and review

Separate compiler, runtime, compatibility library, embedding SDK, Expo package,
tests and examples as implementation arrives; no empty scaffolding is required.
Maintain this specification, [architecture decisions](decisions/README.md),
a compatibility manifest, ABI documentation, conformance cases, benchmarks and
contribution guidance.

Review each slice for semantics, rooting, ownership and diagnostics before a
coherent commit. Prefer targeted local validation. Hosted CI remains disabled
unless explicitly authorised; quote aggregate runner minutes for all matrix
jobs/retries before enabling hosted work. No extra agents without user direction.

Use independently written code and appropriately licensed dependencies.
[IL2C](https://github.com/kekyo/IL2C) is relevant experimental prior art, not an
assumed compatibility foundation. The project is independent of Unity.

## 19. Design review and unresolved decisions

The main risks are library coverage, precise roots, exception semantics and
generic reachability. The milestone order tackles them before the Expo demo.
Before implementation, pin SDK/reference assemblies, C++ toolchains, exact ABI
layouts, collection root representation and the first supported library members
in architecture decisions backed by small experiments. The execution model and
root representation are proposed in ADRs 0001 and 0002, pending experiments.

The intended initial release claim is deliberately specific:

> Cilica independently compiles a documented subset of .NET IL into C++, and
> runs an existing C# turn engine consistently inside native mobile and browser
> applications.

This claim is aspirational until the corresponding exit gates have passed.

The authored `scalar-managed-exceptions-ir-v2` profile now executes catch, pure type
filters, throw/rethrow and leave/finally/fault through explicit native continuations
([ADR 0084](decisions/0084-native-exception-dispatch.md)). Messages preserve supplied UTF-16
and bounded invariant defaults; O0/O2 sanitizer checks cover rooted identity and
cleanup. Bounded CIL exception translation is now supplied by ADR 0085; a public
structured-error ABI remains separate.

Final exception identity review (ADR 0086) distinguishes fresh formatted defaults,
shared fixed resource strings and supplied/contextual cast messages. A separate
bounded instance resource-root list preserves collection and identity semantics.


Owned Guid support (ADR 0138) now covers numeric constructors, Empty, typed
identity/hash/unsigned comparison and standard D/N/B/P/X formatting through
managed IL. Four Int32 words are an internal representation, not a public layout
or serialization ABI. Boxed value type tests, parsing, random generation and
byte/span APIs remain outside the admitted profile. Exception IR v2 and native
v22 add exact System/Arithmetic/Format resources, including distinct explicit-null
behavior and resource identity. Native Guid evidence is pinned to the local
little-endian .NET oracle; this does not complete the real-kernel milestone.


Composed integer-object heap fields may now contain closed inline values
([ADR 0139](decisions/0139-inline-values-in-heap-fields.md)). The compiler discovers
the combined class/value graph, then expands heap values into bounded scalar and
reference fields with derived component identities. This reuses existing native
field ownership and GC tracing without extra allocations. Empty fields retain a
checked receiver operation. Compound stores directly from struct-return calls
remain diagnostic until destination/exception ordering is explicitly lowered.


The direct-core dictionary trial (ADR 0141) adapts pinned bucket-chain lookup,
insertion, overwrite and resize into Cilica.Library.TrialDictionary. Five probes
cover closed integer and IEquatable struct keys, including mutable callbacks and
exceptions. Exact private helper grants select managed key bodies. System.Dictionary
reference mapping and collection/array interfaces remain pending; the trial's own
name prevents a partial collection contract from being advertised as compatible.


Selected-runtime vector interfaces now support casts, Count and indexed reads
through exact granted managed helpers (ADR 0142). Int32/reference vectors preserve
reference covariance, Int32/UInt32 bit identity, bounds messages and null checks.
Object layout v7, interface plan v2 and native v24 carry explicit array assertions.
Enum elements and unimplemented interface operations remain diagnostic. This does
not yet provide public collection implementations or native kernel equivalence.


The bounded public Dictionary mapping now preserves its exact nominal interfaces
and admitted method flags (ADR 0143). Default construction, Count, TryAdd,
TryGetValue, ContainsKey, setter overwrite and collection flags/SyncRoot are tested
through real reference calls. Missing bodies fail at build time. Nongeneric array
collection flags and SyncRoot use exact granted managed helpers, with layout v8
and native v25. The real kernel now reaches StringBuilder; native equivalence
remains pending.


Immediately proven sealed receivers now narrow public zero-argument virtual calls
before body discovery (ADR 0144). Canonical slots, typed receivers and null checks
remain intact. Branch/handler entry, nonpublic declarations and unproven Object
parameters do not establish a proof. This supports managed overrides without
claiming a general Object.ToString implementation.


The owned StringBuilder now adapts pinned managed chunk append and immutable
snapshot algorithms (ADR 0145). A private granted construction operation copies
low UTF-16 units from rooted Int32 vectors; it preserves canonical empty strings
and fresh nonempty snapshots. Narrow vectors and broader builder APIs remain
unsupported. Typed reference IR v6/native v26 include this operation. Exception
resource cache bounds derive from the emitted resource set, including System and
Arithmetic messages. The real kernel's next gate is Array.Empty<T>.


Selected Array.Empty<T> calls use an exactly granted typed operation and a native
per-instance cache keyed by the exact vector descriptor (ADR 0146). Int32 and
reference vectors retain identity across collection without conflating covariant
element types. Unsigned, narrow, wide and inline-value vector caches remain
diagnostic. Typed reference IR v7/native v27 encode the operation and traced roots.
The real kernel next requires List<T>.


The managed library provides bounded List<T> storage/growth and live
ReadOnlyCollection<T> views (ADR 0147). Exact array interface mutation helpers
preserve fixed-size Add faults and bounds-before-covariant-store checks. Interface
plan v3/native v28 carry validated mutable array views. Int32/reference element
storage is admitted; broad collection APIs remain diagnostic. The concrete kernel
next requires compiler-generated initialized static delegate caches.


Exact single-target delegates admit typed Int32/reference parameters in the
integer-object profile (ADR 0148). Object layout v9/native v29 retain parameter
identities, structural catalog checks and managed reference argument packets.
Targets match exactly; wide/value/byref parameters and binding variance remain
diagnostic. AC2.1 below adds exact reference results. Static callback
initialization was still pending at this checkpoint.


Ordinary Int32/reference static fields without type/module initialization now use
typed load/store IR and instance-owned, quota-charged native slots (ADR 0149).
Reference slots are collector roots, and exact closed owner identities separate
generic state. Readonly writes, addresses, initialized types and narrow/wide/inline
static values remain diagnostic. Object layout v10, typed reference IR v8 and
native v30 encode this foundation; initializer execution is still pending.


TypeInitializationException(String, Exception), TypeName and Exception.InnerException
are now exact granted integer-object operations (ADR 0150). Construction eagerly
formats the pinned invariant message and retains a precisely traced name/inner
payload; null names read as canonical empty strings. Managed/scalar exception IR
v3, typed reference IR v9 and native v31 encode this addition. Initializer execution,
failure caching, serialization and general Exception constructor overloads remain
outside this slice. Local O0/O2 sanitizer comparisons cover payloads and failures.


Authored integer-object IR can now request bounded per-instance initialization
(ADR 0151). Exact private initializer bindings, dedicated call/fault safepoints,
reentrant checks and retained managed/fatal outcomes use typed reference IR v10,
object operations IR v11 and native v32. Wrapper preparation precedes user IL;
preparation failure can retry, while started initializers never repeat side effects.
Native O0/O2 sanitizer tests cover roots, failure identity and quota/corruption.
CIL trigger insertion and initialized metadata admission remain pending.


BeforeFieldInit class initialization is now connected to CIL field reads/writes
(ADR 0152). Exact closed .cctor bodies execute through the ordinary compiler, with
readonly owner checks and a granted failure-wrapper constructor. The permitted
timing choice is first static field access. Generic failure names use the definition
name while state uses the closed owner identity. Four probes provide 1,176 local
O0/O2 differential results. Strict type/module initialization, initialized values,
static addresses and broader static storage remain diagnostic. The concrete kernel
still requires private nested cache access and has no native transcript evidence.


Nongeneric private nested cache owners now use explicit bounded declaring-type
chains (ADR 0153). Same-image containing/nested callers can access their admitted
public fields and public/assembly methods. Detached validation rejects contradictory
chains and visibility claims; missing facts grant no lexical privilege. Object
layout v11 and object operations IR v12 retain this evidence. Three probes provide
882 local O0/O2 differential results. Protected/generic lexical access and broader
private member access remain diagnostic. The unchanged kernel now reaches its
ValueTuple`2 dependency; native equivalence remains pending.


Bounded ValueTuple<T1,T2> fields/construction, typed equality and hashing now execute
as managed code (ADR 0154). Exact private key grants select managed ordinal string
operations as well as existing integer/inline keys. Equality copies component
receivers; hashing addresses the original fields. Fixed private hashes preserve
key consistency without promising cross-runtime numeric hashes. Four probes
provide 1,176 repeated local O0/O2 results. Object equality, tuple formatting,
comparison and structural APIs remain diagnostic. The concrete kernel next reaches
String.Format; no native kernel equivalence is claimed.


Boxed interface-bearing values now preserve authenticated interface ancestry and
variance in native descriptor predicates (ADR 0155). Fresh-box implicit conversions
are typed checked casts; payload storage, identity, unbox copies and precise roots
retain existing behavior. Three probes provide 882 repeated local O0/O2 results.
Boxed method dispatch remains diagnostic until its payload receiver is lowered.
Boxed value IR v2, scalar boxing IR v3 and native v33 retain the added scope.


String.Format(String,Object) now has an exactly granted path for a directly boxed
named non-IFormattable value (ADR 0156). Same-block operand proof selects an ordinary
managed generic parser and a private exact payload borrow/ToString call. Boxing
allocation, repeated callback mutation and fault ordering are retained. Four probes
provide 1,176 local O0/O2 comparisons. Aliases, other argument kinds, providers,
IFormattable and general boxed dispatch remain diagnostic. The kernel next requires
StringComparison; native transcript equivalence remains unproven.


The exact String.Equals(String,String,StringComparison) overload now accepts a
directly proven Ordinal constant through an approved grant (ADR 0157). It reuses
existing UTF-16 equality; nulls and operand evaluation retain existing semantics.
The pinned enum declaration is mapped, but other modes, aliases and general enum
storage remain diagnostic. One probe supplies 294 local O0/O2 comparisons. The
kernel now reaches the bounded metadata work limit; native transcripts remain pending.


String.IsNullOrWhiteSpace now executes as ordinary managed UTF-16 code (ADR 0159).
Selected String static IL bodies use normal discovery and validation; instance
methods and initialization retain explicit restrictions. Object operations IR v13
records this admission. Every UTF-16 code unit and mixed/null/empty strings match
.NET at O0/O2 with ASan/UBSan and forced collection. No native intrinsic was added.
The concrete kernel again reaches the unchanged metadata work bound; native
transcript equivalence and implementation/filter closure remain pending.


The exact readonly String.Empty field now requires a declaration-specific field
grant and maps to the runtime's canonical per-instance empty literal (ADR 0161).
O0/O2 sanitizer tests preserve identity with empty literals, Concat and StringBuilder
output, plus bounds faults and collection safety. Writes, missing approval and
altered field shapes remain diagnostic. The concrete kernel reaches an aggregate
compiler limit; native transcript equivalence remains unproven.


The shared method closure cap is now 512 after the concrete kernel exhausted the
previous declaration count while remaining within its byte, instruction and metadata
budgets (ADR 0162). Exact/excess discovery, detached validation and full-capacity
native O0/O2 checks pass. All other aggregate and runtime limits are unchanged;
interface declarations, facts and bodies continue to share the method cap.


Exact externally visible Int32-backed enums now use existing scalar storage for
signatures, fields, copies and borrows in the integer-object profile (ADR 0163).
Metadata shape and selected Enum ancestry are validated; closed generic signatures
retain nominal identity. Two native probes provide 588 repeated O0/O2 comparisons.
Other widths, enum boxing/arrays and general Enum APIs remain diagnostic. The
concrete kernel next needs Boolean field storage; native transcripts remain pending.


Narrow Boolean/Char/byte/short fields now have explicit read/write casts in the
integer-object profile (ADR 0164). Private Int32 slots preserve managed truncation,
sign extension and copy/zero semantics without a public layout claim. Thirteen
probes supply 1,662 repeated O0/O2 native comparisons, including stores without IL
conversions. Narrow addresses and arrays remain diagnostic. The kernel has reached
another metadata work bound; native transcript equivalence remains pending.


Closed value-vector storage now has invariant nominal element identity and an
explicit bounded scalar shape (ADR 0166; object layout v12/native v34). Typed
Int32/Int64/reference buffers preserve zeroing, exact heap quotas, per-instance
empty identity and collection of every element reference. O0/O2 sanitizer probes
cover storage and reclamation. Value element operations and CIL/library discovery
remain pending; this does not yet compile the kernel's `PlayerId[]` path.


Scalar value-vector component borrows now retain owner roots and validate exact
descriptors, indices, leaf kinds and reference predicates (ADR 0167; scalar location
IR v2/native v35). Twelve focused/composed checks include 288 repeated O0/O2
.NET/native outcomes with sanitizers and forced collection. Logical value element
operations and CIL/library admission are still pending.


Logical closed value-array loads/stores and synchronous element borrows now lower
to checked scalar components (ADR 0168; value array IR v2/value location lowering
v4). Shapes derive from the validated struct plan; copies snapshot fields, roots
retain references/owners, and empty structs retain null/bounds checks. Thirty-four
focused/composed checks include 576 repeated O0/O2 .NET/native outcomes. CIL and
collection/interface admission remain pending.


Closed value-array CIL now admits newarr, typed ldelem/stelem, value ldelema and
class/struct array fields; Array.Empty retains exact value identity (ADR 0169).
Seventy-nine focused/regression checks include 1,470 new repeated O0/O2 native
outcomes against .NET with sanitizers and forced collection. Primitive-width,
enum/nested arrays and readonly-prefixed element borrows remain diagnostic.
The concrete kernel passed PlayerId allocation and reached its bounded metadata
ledger in PriorityWindowRequest construction; native transcripts remain pending.


Boxes confined to comparisons, unboxing and proven ordinary helper parameters can
now coexist with unrelated interface dispatch (ADR 0172; boxed value IR v3/scalar
boxed IR v4). Detached logical and scalar validators independently check all uses
within bounded work. Escaping aliases and actual boxed interface calls remain
diagnostic. Two probes add 588 repeated O0/O2 .NET/native outcomes. The concrete
kernel next requires value-array interface assignment; native transcripts remain
unproven.


Value-array interfaces now preserve exact closed value arguments and use the
selected managed ArrayInterface<T> bodies (ADR 0173; object layout v13). Scalar
helper signatures derive from ordered element leaves, including explicit empty
values. Four probes add 1,176 repeated O0/O2 .NET/native outcomes with sanitizers,
forced collection, nested reference-bearing structs, fixed-size faults and live
List/read-only views. Unrelated value/reference interface casts remain false.
The concrete kernel now reaches switch in ResolutionKernel.Step; native transcript
equivalence remains pending.


The integer managed-object CIL v2 profile lowers bounded switch tables to existing
comparison branches (ADR 0174). Selector consumption, remaining stack values,
duplicate/default targets and exception-region ordering are preserved; synthetic
comparison offsets occupy only the existing table operand span. Five probes add
1,488 repeated O0/O2 native outcomes with sanitizers, forced GC and unwind checks.
Malformed targets, non-Int32 selectors and expanded block overflows are diagnostic.
The kernel now reaches readonly constructor receiver validation; full native
transcript equivalence remains pending.


Class constructors may now branch after one immediate-base call in their entry
prefix (ADR 0175; object operations IR v14). Body paths cannot re-enter that prefix,
repeat initialization or catch its failures. Stable direct receiver provenance
survives joins only when its argument slot is never addressed. A reduced readonly
null-coalescing constructor matches .NET in 294 repeated O0/O2 native outcomes;
mutable receiver slots and detached reinitialization/re-entry mutations are rejected.
The real kernel next reaches cast exception type-name resource validation.


Cast exception type names now admit compiler-generated angle-bracket names while
retaining existing length/text budgets (ADR 0176). A closed anonymous-type probe
compares the complete UTF-16 message against .NET at O0/O2 (294 repeated outcomes).
The real kernel now passes translation and detached IR validation, and native C++
emission succeeds. Native execution/transcript and final closure gates remain.


Milestone 6e now matches the unmodified private kernel's ten-step .NET transcript
against native O0/O2 builds, normally and with collection before every allocation.
All 10,752 scalar/canonical UTF-16 observations pass under ASan/UBSan, with stable
retained roots across repeats. Evidence is pinned to the source build and selected
6d.23 library; final implementation/filter closure and composed review remain.


Milestone 6 completion (ADR 0177) includes the required implementation closure,
real native kernel transcripts and composed collection/exception stress. The final
2,632-test scope is covered by a full run plus targeted reruns correcting three
stale budget expectations; the 163-check follow-up and final composition checks
pass. The full build has no warnings/errors. General-filter stage B is not needed
by this concrete closure, whose 287 source definitions have no exception regions.
The conservative metadata inventory remains incomplete and is not the evidence
used to assert compiled closure. Milestone 7 is the separate public ABI/device gate.

The follow-on private saved-game baseline restores three full-application games
on .NET with 3,227 accepted envelopes, exact archived final state hashes and
repeatable per-command state/event-text traces. This does not establish native
full-game support: the initial real engine constructor/step probe stopped at an unmapped
`System.Type` dependency, and its conservative inventory exhausts the default
dispatch-pair budget. See the milestone 6 follow-on record for scope, evidence
and the bounded local reproduction recipe. JSON/gzip decoding and reflection in
the authored oracle host are outside the advertised native compiler profile.


The follow-on identity slice (ADR AC0001) implements bounded closed reference-type
and vector metadata literals, instance `GetType`, copied traced RuntimeTypeHandle
values, Type equality/hash and Type dictionary keys. Native runtime instances own
lazy, heap-budgeted canonical Type caches; caches are precise permanent roots and
never shared across instances. Type hashes are instance-local implementation
values, not serialized identifiers or .NET hash-number equivalents. The selected
fieldless Type/MemberInfo hierarchy and private concrete implementation need exact
shape checks and layout grants. Object-slot virtual calls require a statically
known ordinary class receiver; special runtime receivers and Object aliases stay
diagnostic until their dispatch is implemented. Abstract ancestry is admitted in
the closed integer-object profile, but abstract allocation/method execution is not.

Four public fixtures match .NET at native O0/O2 with forced collection and
sanitizers. The unchanged private Alchemy constructor/step metadata probe now
passes the Type mapping boundary and stops at unmapped `System.Nullable<T>`.
Full saved-game native replay, general reflection and additional target platforms
remain unverified. No application-specific logic enters the core or compiler.


The follow-on saved-game gap audit reuses the incomplete broad graph and adds five
bounded method-root inventories plus 52 focused probes. Nine probes pass native
O0/O2 differential execution (2,646 repeated comparisons); 43 remain diagnostic.
The 260 observed framework declarations include 135 unmatched/unmapped declarations,
which are not a count of independent missing features or a complete closure.
Nullable values are the first recommended package, followed by reference-returning
delegates, required collection operations/enumeration, numeric/enum/data APIs,
iterators/LINQ and string/encoding/hash support. SHA-256 also appears inside the
engine's semantic fingerprint path, so moving save decoding to a host does not
remove that dependency. See the milestone 6 prioritized audit record for evidence,
remaining analysis bounds and the private reproducible probe recipe. This audit
changes no implemented compiler/library or platform-compatibility claim.


### Nullable value follow-on (AC1a)

[ADR AC0002](decisions/AC0002-nullable-value-storage.md) implements the owned
Nullable<T> constructor, HasValue, Value and both GetValueOrDefault forms through
ordinary closed value storage. Existing copies, return packets and tracing cover
reference-bearing payloads; no special native representation or new intrinsic is
needed. Exact selected enum ancestry now satisfies generic struct constraints,
while executable enum visibility/width restrictions remain separate.

AC1a native evidence includes empty/present/default values, scalar and value
payloads, field/array copies, rooted references and the Value exception. Direct
nullable boxing/unboxing and nullable key operations explicitly reject pending
their distinct null/underlying-value semantics. Equality/hash/text and direct
operator bodies remain unsupported. Full Alchemy replay and untested platforms
remain open; current private roots now expose static inline fields, internal-enum
storage and SHA-256 as subsequent blockers. See the milestone 6 AC1a outcome and
the assigned gap ledger for exact scope.


### Nullable boxing and conversion follow-on (AC1b.1)

[ADR AC0003](decisions/AC0003-nullable-boxing-and-conversions.md) advances AC1b
for already boxable named structs. Exact selected nullable box/unbox.any operands
call verified managed helpers: empty becomes null, present boxes T, null unboxing
returns default, and a present copy requires T's exact descriptor. Ordinary
allocator/root/return-packet paths preserve reference-bearing payloads. Direct
conversion operators now have managed bodies; representative lifted and checked
integer expressions have differential evidence.

A diagnostic-only nullable target flag preserves InvalidCastException's target
name while retaining T's runtime identity check. It flows through boxed IR v4,
scalar boxed IR v5 and native v38; the private exception payload carries the flag.
Native resource selection uses a small helper to stay within the unchanged
2,048-byte O0 sanitizer frame limit. No public ABI or intrinsic grant is added.

Primitive/enum payload descriptors, raw nullable unbox locations and remaining
nullable type-test forms are not implemented by this slice. Nullable keys and
text/equality/hash remain assigned work. Twenty-seven native probes provide
7,938 O0/O2 comparisons plus heap-budget/failure-recovery checks; the affected
local suite passes 120 tests. Full AC1b and full Alchemy replay remain open.


### Nominal scalar boxing follow-on (AC1b.2)

[ADR AC0004](decisions/AC0004-nominal-scalar-boxes.md) adds box/unbox.any for
selected Boolean/Char and integer widths plus existing public nongeneric Int32
enums. Each exact nominal type has a runtime-owned scalar slot, separate from its
evaluation-stack carrier. These box-only layouts cannot become managed inline
fields/arrays or acquire fabricated FieldDefs. Narrow copies normalize through
existing integer lowering; boxes reuse allocation, packets, owners and tracing.

Ordinary unbox supports the .NET Int32/Int32-enum compatibility group; nullable
unbox requires exact underlying identity. GetType still reports the original box
type. Checked copy locations use the actual descriptor's accessor, avoiding native
aliasing between distinct box structs. Core binding plans add six exact primitive
type mappings without new intrinsic grants.

Scalar/nullable type tests, raw unbox locations and scalar interface views remain
explicit errors. Closures combining scalar boxes with interface casts/tests or
interface dispatch conservatively reject until primitive contracts or narrower
proofs are complete. Nonpublic/non-Int32 enums and floating/native-sized values
remain outside scope. Full AC1b and Alchemy replay remain open.

Layout v3, boxed IR v5, scalar boxed IR v6, boxed runtime v3 and native v39 carry
this scope. The 18 scalar and 31 nullable probes provide 14,406 repeated native/
.NET comparisons at O0/O2 on macOS ARM64 with sanitizers and forced GC; 146
focused regression checks also pass. No private replay evidence was regenerated.


### Exact boxed type-test follow-on (AC1b.3)

[ADR AC0005](decisions/AC0005-boxed-type-tests.md) admits isinst/castclass for
existing scalar/enum and named-value boxes, including Nullable<T> targets. Those
targets resolve to T after closed constraint/access/budget validation. Explicit
boxed-target IR retains an Object result view, counts test-only payload roots and
is checked at both value/scalar stages. Existing reference-type rules are retained.
Native ownership/null checks precede exact descriptor comparison; success returns
the original box without allocation or payload reinterpretation.

Type tests require exact identity even where ordinary Int32/enum unboxing admits
compatible copies. Direct .NET IL probes also distinguish failed castclass
Nullable<T>, which names T, from nullable unbox.any, which names Nullable<T>.
The existing nullable-unbox flag is not used for type tests. No core/manifest grant
or public ABI changes are needed. Boxed IR v6, scalar boxed IR v7 and native v40
record the extension.

Twenty-nine native probes provide 8,526 repeated O0/O2 .NET comparisons with
sanitizers/forced GC on macOS ARM64, alongside malformed-operand/IR checks and
existing cast/box/exception regressions. Raw scalar/nullable unbox, primitive
interface contracts/use proofs, nullable keys and general filters remain deferred.
Full AC1b and private saved-game replay are still open; private roots were not rerun.


### Confined scalar/interface composition (AC1b.4)

[ADR AC0006](decisions/AC0006-confined-scalar-box-uses.md) replaces the blanket
scalar-box/interface exclusion with the existing bounded use proof. In a program
with interface operations, every scalar box allocation must remain confined to
supported inspection, exact boxed type tests, unboxing and proven ordinary helper
parameters. Exact type-test results are tracked as aliases. Interface operations
on those aliases, unknown escapes, storage, returns, parameter addresses and
recursive proofs still fail closed. Descriptor-only targets need no allocation
proof. Validation runs before and after lowering with unchanged work/depth bounds.

This permits independent interface calls/casts/tests without treating incomplete
primitive declarations as complete interface semantics. Boxed IR v7 and scalar
boxed IR v8 change; native v40, layouts, emitted operations, core contracts/grants
and public ABIs do not. Thirteen native probes provide 3,822 repeated O0/O2 .NET
comparisons with sanitizers/forced GC; nine rejection fixtures, alias mutations at
both stages and 129 existing regressions pass. General nullable boxing helpers
that return a box still require broader flow proof in these compositions.

The archived body inventory contains no raw unbox in its 1,775 readable bodies,
but its incomplete application/library/callback coverage cannot prove absence
from the final replay closure. Raw scalar/nullable unbox and escaping/interface
box semantics remain open. Full AC1b and saved-game replay are not complete.


### Reference-returning application delegates (AC2.1)

[ADR AC0007](decisions/AC0007-reference-delegate-results.md) extends exact
single-target delegates to admitted managed reference results in the integer-object
composition. The signature owns one typed return; catalogs retain and compare its
identity, including vectors used only in delegate signatures. Captures and calls
must match the result exactly. Native dispatch uses an Object* function pointer
and publishes into normal caller roots before exception routing, preserving the
existing non-allocating reference-return handoff.

Object layout v15, delegate value/scalar IR v2 and native v41 encode this slice.
Thirteen new probes plus the public RefDelegate probe provide 4,116 repeated
O0/O2 native/.NET comparisons with forced GC and ASan/UBSan, including a reduced
Type-to-handler dictionary. The ledger now has 36 expected gaps and 11 native
controls. Framework delegate declarations/signatures remain AC2.2; covariance,
multicast, wide/value/byref results and recursive delegate signatures remain
outside the bound. Neither the public embedding ABI nor private replay evidence
changes, and full AC2 and saved-game replay remain incomplete.


### Required framework delegates (AC2.2)

[ADR AC0008](decisions/AC0008-framework-delegate-contracts.md) adds owned
Action<T>, Func<T,TResult>, Func<T1,T2,TResult>, Predicate<T> and Comparison<T>
alongside Action<T1,T2>, preserving pinned generic variance and exact method
mappings. The integer-object composition also admits Boolean delegate results
through the existing normalized Int32 carrier. Discovery checks declared scalar
target identities before lowering, preventing bool/int and signed/unsigned
mismatches from disappearing into that shared carrier. Parameters remain exact
Int32 or admitted references; wider/inline/byref signatures and variance conversions
remain unsupported.

The composed CIL profile is v3; object layout v15, delegate IR v2 and native v41
are unchanged. Fifteen new probes plus public FuncDelegate provide 4,704 repeated
O0/O2 .NET/native comparisons with forced GC and sanitizers. Twelve focused tests
and 112 distinct existing regressions pass. The public ledger now has 35 expected
gaps and 12 native controls; Where passes its Func mapping and next reports the
missing Enumerable contract. AC2.1/AC2.2 have bounded evidence, while actual private
closed callbacks, full command closure and saved-game replay remain unverified.


### Concrete list mutation and snapshots (AC3.1)

[ADR AC0009](decisions/AC0009-list-mutation-and-snapshots.md) adds managed
List<T>.Insert/RemoveAt/Clear/ToArray over existing element storage. Overlap-safe
shifts preserve ordering; removed reference slots are cleared, including references
inside inline values. Nonempty snapshots are independent arrays with ordinary
value copies. Empty snapshots share per closed T, distinct from Array.Empty<T>,
using an existing traced per-runtime static slot and allocation-before-publication.

Eleven authored probes plus two public probes provide 3,822 repeated native/.NET
comparisons at O0/O2 with forced GC and sanitizers. Four live-list GC checks cover
reference and reference-bearing value elements; 61 existing regressions pass.
The ledger has 33 gaps and 14 native controls. No compiler/runtime profiles,
intrinsic grants or public ABIs change. Interface mutation compositions remain
blocked by missing array helper contracts, as explicit rejection tests confirm.
CopyTo, equality-based operations, bulk APIs, other collections and enumeration
remain planned. Full AC3 and saved-game replay are not complete.


### Array-interface mutations (AC3.2)

[ADR AC0010](decisions/AC0010-array-interface-mutations.md) resolves AC3.1's array
helper gate. Five exact grants select ordinary managed Insert/InsertObject,
RemoveAt, ClearReadOnly and Clear bodies. Fixed-size faults precede index/type
validation. Generic Clear throws read-only; nongeneric Clear resets typed elements
without changing identity/length and releases nested reference slots. Interface
plan v4 checks logical and flattened value packets; native v41 and embedding
ABIs are unchanged.

Thirteen array probes and two advanced list-interface probes supply 4,410 repeated
O0/O2 native/.NET comparisons with forced GC and sanitizers. Four live-array GC
checks, exact grant denial, detached signature checks and 125 existing regressions
pass. The supplemental interface blocker advances; the public ledger remains
33 gaps and 14 controls. Copying, search, bulk operations and enumeration remain
planned; full AC3 and private saved-game replay are not complete.


### Dictionary removal and clearing (AC3.3)

Owned Dictionary Remove (with/without out value) and Clear now unlink bucket
chains, release payload roots and reuse freed entry slots before growing. Mutable
key hashes preserve the pinned receiver semantics; throwing callbacks leave out
storage and dictionary contents unchanged. Ten probes plus public DictionaryRemove
supply 3,234 repeated O0/O2 .NET/native comparisons, four live-dictionary GC checks
and 38 existing library/baseline checks. The ledger has 32 gaps and 15 controls.
No compiler/runtime profile, grant or ABI changes. Copying requires rich range
faults and precise array downcast semantics; Add/getter/constructors, other
collections and enumeration remain planned. Full replay remains incomplete.
See [ADR AC0011](decisions/AC0011-dictionary-removal-and-clear.md).


### Integer math and typed comparisons (AC5a.1)

Owned managed Math implements signed Int32/Int64 Abs and signed/unsigned 32/64-bit
Min/Max. Typed CompareTo methods preserve exact IComparable<T> declaration flags
and use existing scalar receiver lowering. Explicit minimum-value guards preserve
Abs overflow and its message; comparisons avoid overflowing subtraction.
Eleven authored plus three public probes supply 4,116 repeated O0/O2 .NET/native
comparisons; exact overload checks and 35 existing regressions pass. The ledger
has 29 gaps and 18 controls. No profile, grant or ABI changes. Broader math,
object comparison, comparer execution, Guid helpers and full replay remain open.
See [ADR AC0012](decisions/AC0012-integer-math-and-comparison.md).


### List and array search (AC3.4)

List Contains/IndexOf/Remove and live read-only search now use exactly selected
managed loops. Value equality borrows the stored array element, preserving its
observable mutations and roots across allocating/throwing callbacks; integer and
admitted reference search uses copied receivers. Array IndexOf/Contains/Remove
have exact grants and interface-plan-v5 packet checks. ClosedArraySearch is a
separate private grant; scalar/reference element addresses remain unsupported.
Twelve authored plus one public probe supply 3,822 repeated O0/O2 .NET/native
comparisons, two live-list GC checks and 48 existing regression checks. The ledger
has 28 gaps and 19 controls. Native v41 and public ABIs remain unchanged. Nullable/
unknown-reference equality, range/copy/bulk APIs and full replay remain unfinished.
See [ADR AC0013](decisions/AC0013-list-and-array-search.md).


### Ordinal text comparison (AC7b.1)

String.CompareOrdinal and IsNullOrEmpty now have owned managed bodies. Comparison
preserves the pinned .NET 10.0.11 numeric result, including short-string zero
padding and odd-prefix terminators, using safe UTF-16 indexing. Four authored
probes plus the public comparison provide 7,794 repeated O0/O2 native/.NET results
with forced GC and ASan/UBSan. All 71 relevant baseline/string regressions pass.
The ledger has 27 gaps and 20 controls. No profile, grant or ABI changes; cultural,
ignore-case and other text APIs remain separate work. Private replay is unverified.
See [ADR AC0014](decisions/AC0014-ordinal-text-comparison.md).


### Fixed-invariant numeric append (AC7b.2)

StringBuilder.Append accepts signed/unsigned 32/64-bit integers through existing
managed integer formatting and string append. This bounded core fixes numeric
text to invariant culture; it does not implement ambient CurrentCulture behavior.
The .NET oracle is explicitly pinned and restored by the tests. Six probes plus
public StringBuilderInt supply 2,058 repeated O0/O2 native/.NET comparisons, and
37 builder/baseline regressions pass. Forced GC, sanitizers, frame and cleanup
checks remain enabled. The ledger has 26 gaps and 21 controls. No new grants or
profiles; full replay remains unverified. See [ADR AC0015](decisions/AC0015-invariant-numeric-append.md).


### Array read-only views (AC3.5)

Array.AsReadOnly now returns a live nonempty view or a shared per-closed-type empty
view. Private caches use existing instance-owned static roots. Null validation,
covariance, original-array SyncRoot and readonly mutation faults match the pinned
runtime. Six probes plus the public baseline provide 2,058 repeated O0/O2 native/
.NET results; two cache lifetime/isolation checks and 54 existing regressions pass.
Sanitizers and forced GC remain enabled. The ledger has 25 gaps and 22 controls.
No new grants or profiles; enumeration, copying and full replay remain unfinished.
See [ADR AC0016](decisions/AC0016-array-readonly-views.md).


### Concrete list enumeration (AC4.1)

List<T>.GetEnumerator now supplies the exact nested value-type contract with
Current, MoveNext and Dispose. Copies advance independently; successful list
mutations invalidate later MoveNext calls, while capacity changes and failed
removals do not. Default/ended current values, reference/value copies and fault
messages match the pinned runtime. Twelve authored probes plus public ListForeach
supply 3,822 repeated O0/O2 native/.NET comparisons with forced GC and sanitizers.
Three reduced metadata tests fix nested generic parameter double counting; 82
existing regressions and the boxed-interface rejection pass. No new grants or
runtime profiles. The ledger has 24 gaps and 23 controls. Interface enumeration,
boxed receiver dispatch and full replay remain unfinished.
See [ADR AC0017](decisions/AC0017-concrete-list-enumeration.md).


### Dictionary key and value views (AC4.2)

Dictionary Keys/Values now expose cached live views through concrete, generic,
read-only and nongeneric contracts. They preserve identity, Count, SyncRoot and
read-only faults. Key lookup forwards ContainsKey; ContainsValue and value-view
lookup scan live entries with copied equality receivers. Nulls and removed slots
are handled explicitly. Ten probes plus public DictionaryKeys supply 3,234 repeated
O0/O2 native/.NET comparisons; two view-cycle GC checks, exact nested metadata,
unknown-equality rejection and 41 existing regressions pass. The ledger has 23
gaps and 24 controls. No new grants or native profiles. Copying, enumeration and
full replay remain incomplete. See [ADR AC0018](decisions/AC0018-dictionary-views.md).


### Concrete dictionary enumeration (AC4.3)

Dictionary.GetEnumerator now exposes its exact nested value-type contract. It
walks active entry slots, copies the current KeyValuePair and resets it at the end.
Only insertion of a new key increments the version: removal, clearing, overwrite
and failed TryAdd remain valid during iteration. Eleven probes plus the public
foreach baseline supply 3,528 repeated O0/O2 native/.NET comparisons, including
current-value survival across forced GC after clearing the dictionary. Forty-three
existing regressions pass with sanitizers enabled. The ledger has 22 gaps and 25
controls. No new grants/profiles; boxed interface enumeration, view enumeration
and full replay remain unfinished. See [ADR AC0019](decisions/AC0019-concrete-dictionary-enumeration.md).


### Concrete dictionary view enumeration (AC4.4)

KeyCollection and ValueCollection now expose their exact nested enumerator types.
They retain only the selected current key or value, preserve dictionary mutation
rules, copy independently and reset current at completion. Eight probes supply
2,352 repeated O0/O2 native/.NET comparisons; four precise GC checks prove the
key/value retention difference after clear and release at iteration end. All 35
view/baseline regressions pass with sanitizers and forced GC. No new grants or
profiles. The ledger remains 22 gaps and 25 controls; interface/array enumeration
and full replay remain unfinished. See [ADR AC0020](decisions/AC0020-concrete-view-enumeration.md).


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
and 29 controls. See [ADR AC0026](decisions/AC0026-internal-int32-enums.md).


### Metadata constant layouts (AC5b.2)

Validated primitive constant declarations no longer occupy class or struct storage.
Five probes pass 1,470 repeated O0/O2 native/.NET comparisons with sanitizers and
forced GC; 12 metadata boundaries, 97 layout/enum regressions and 21 baseline checks
pass. Direct field operations and malformed constants remain rejected. The frozen
state probe now reaches a private nested enum; replay remains incomplete. See
[ADR AC0027](decisions/AC0027-metadata-constant-layouts.md).


### Direct string calls (AC7b.3)

Exact String.Length/Chars now accept Roslyn's direct call form as well as callvirt.
Existing declaration and intrinsic permissions remain enforced. New fault/UTF-16
probes and direct-call layout regressions pass 2,646 repeated O0/O2 comparisons;
150 string/admission checks pass with sanitizers and forced GC. No profile/grant
changes. See [ADR AC0028](decisions/AC0028-direct-string-instance-calls.md).


### Nested enum storage (AC5b.3)

Exact nested Int32 enum payloads now use admitted layouts with separate lexical
operand access. Three probes pass 882 repeated O0/O2 native/.NET comparisons;
eight boundary checks and 30 previous enum/lexical checks pass. The frozen state
probe now reaches the unchanged 256-type catalog limit. Static value storage and
full replay remain unfinished. See [ADR AC0029](decisions/AC0029-nested-enum-storage.md).


### Wide static storage (AC5b.4)

Int64/UInt64 static fields preserve all bits in per-instance storage. Five probes
pass 1,470 repeated native/.NET comparisons; two persistence/isolation runs, 18
static/default checks, 25 initialization/packet checks and 21 public baseline checks
pass. Native profile v42 and layout v16 account for the wider private slot. Inline
static structs and full replay remain unfinished. See
[ADR AC0030](decisions/AC0030-wide-static-storage.md).


### Static inline-value copies (AC5b.5)

Whole static struct loads/stores now expand through validated value layouts into
per-instance scalar and traced reference slots. Eight probes pass 2,352 repeated
O0/O2 native/.NET comparisons; four GC scenarios, eight packet mutations, the
address boundary, 90 existing regressions and 21 baseline checks pass. Layout v17,
native v42. Frozen EngineStep advances to a nonpublic generic argument; state
lookup still reaches the type limit. Static addresses and full replay remain
unfinished. See [ADR AC0031](decisions/AC0031-static-inline-value-copies.md).


### Generic source-type provenance (AC8.1)

Closed method snapshots now retain declaration signatures/locals, and a bounded
resolver API retains original operand syntax before generic substitution. This
separates explicit private names from opaque type parameters for the next access
slice. All 54 selected metadata/generic checks pass. No new execution capability
is claimed. See [ADR AC0032](decisions/AC0032-generic-type-use-provenance.md).


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


### Abstract class calls (AC8.13)

The integer-object compiler now retains abstract class signatures separately from
executable methods. Public nongeneric abstract calls support scalar/reference
parameters and scalar/reference/void results, including closed generic owners.
Complete maps select concrete overrides; abstract classes are not runtime receivers.
Interface mappings may retain abstract slot facts while selecting concrete bodies.
Null-only closures have empty maps and still throw before dispatch. Direct abstract
calls, function captures, abstract allocation and inline/borrow signatures remain
unsupported. No abstract stub is emitted and existing resource bounds are unchanged.

Nine probes pass 3,402 repeated O0/O2 native/.NET comparisons with ASan/UBSan,
forced collection and exact cleanup. Logical/scalar tamper tests, metadata ownership
and budget checks, plus 42 adjacent dispatch/boxing checks pass. Integer-object CIL
advances to v14, object IR to v23, delegate/value IR to v5 and interface plans to
v10. Native v49/layout v20 are unchanged. Full replay remains unverified; ordinal
comparers and ordering are subsequent slices. See ADR AC0059 and
artifacts/alchemy-abstract-slots/ac8m-*.


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

### Closed generic interface method metadata (AC8.14)

Closed interface target v4 authenticates exact method arguments and selects final
newslot generic implementations using open method signatures within closed owners.
Implicit/explicit, inherited and reimplemented mappings preserve separate owner and
method arguments, constraints and bounded metadata work. Generic override/class
calls remain unsupported; executable interface dispatch is the next slice.
135 selected metadata checks pass; no native compatibility claim or quota changes.
See [ADR AC0062](decisions/AC0062-generic-interface-method-metadata.md).

### Closed generic interface calls (AC8.15)

Exact closed generic interface methods now execute through final newslot class
implementations. Existing detached generic evidence keeps owner and method arguments
separate and rejects swapped specializations even when their native storage matches.
Scalar/reference/inline results, void calls, null checks, exceptions and generic
factory chains have native evidence: ten probes, 2,940 repeated O0/O2 comparisons,
forced GC and ASan/UBSan. 125 selected checks pass. Direct/class generic instance
calls, overridable generic methods and method pointers remain unsupported.
Integer-object CIL v15, object IR v24, delegate-value IR v6 and interface plan v11;
native v49/layout v20 and resource limits remain unchanged. Frozen engine ordering
and the state catalog bound remain unresolved. See [ADR AC0063](decisions/AC0063-generic-interface-method-calls.md).

### Default integer comparers (AC5a.2)

Comparer<T>.Default supports exact Int32, UInt32, Int64 and UInt64 with one cached
object per closed type per runtime instance. Two exact private helper grants select
managed factories and existing typed CompareTo bodies. Eight probes pass 2,352
repeated native/.NET comparisons, plus exact two-instance root checks and grant /
unsupported-scope tests; all 59 selected local checks pass. Culture/string, nullable,
enum and other defaults remain diagnostic. Compiler/native/layout profiles and
resource limits are unchanged. See [ADR AC0064](decisions/AC0064-integer-default-comparers.md).

### Retained comparer-fault wrappers (AC8.16)

The exact granted InvalidOperationException(String, Exception) constructor now
retains inner identity and exact UTF-16 messages through the existing traced
payload. Null-message construction also retains its required String catalog entry.
Seven probes pass 3,024 repeated O0/O2 native/.NET comparisons; all 18 slice checks
and 76 adjacent exception/embedding/core/baseline checks pass with forced GC and
ASan/UBSan. Detached payloads and ownership/allocation failures are checked. Other
inner-constructor families remain diagnostic. Integer CIL v16, exception IR v6,
native v50; layout v20 and limits unchanged. Frozen engine ordering and state
catalog remain blocked; full replay is unverified. See [ADR AC0065](decisions/AC0065-retained-inner-faults.md).

### Stable Int32-key ordering (AC6.11)

OrderBy/OrderByDescending and ThenBy/ThenByDescending now use owned deferred
iterators, exact generic ordering factories and stable index-map introsort. Callback
order, wrapped comparer faults, source/selector failures, cheap-count/first-element
shortcuts and disposal follow the pinned runtime. Seventeen probes pass 14,944
O0/O2 native/.NET comparisons, including verified heap fallbacks and forced-GC
callback allocations; exact live-query ownership and three unsupported-key checks
also pass. All 88 selected checks pass with ASan/UBSan and unchanged frame limits.
String/culture defaults and wide/unsigned/narrow delegate keys remain diagnostic.
No compiler/native profiles or grants change. Frozen engine reaches its metadata
snapshot limit; state remains at its catalog limit. Full replay is unverified.
See [ADR AC0066](decisions/AC0066-stable-integer-ordering.md).

### Declaration-only runtime classification (AC8.17)

Delegate and exception classification now reads bounded direct-base/name facts
before requesting structural metadata. Actual delegate/exception admission keeps
its full validation and exact runtime identities. Unused static-owner fields no
longer consume layout snapshots. A 257-owner regression passes 294 repeated O0/O2
native comparisons; all 105 selected metadata, delegate, exception and baseline
checks pass. Structural/runtime-type and work limits are unchanged. Frozen engine
advances to missing Enumerable.Contains<String>; state remains at its catalog
limit. Full replay is unverified. See [ADR AC0067](decisions/AC0067-base-only-classification.md).

### Default-equality Contains (AC6.12)

The two-argument terminal preserves collection dispatch, pinned iterator shortcuts,
callback order, cursor state and disposal. Seventeen probes pass 12,240 repeated
O0/O2 native/.NET comparisons; reduced String/Object generic type-test operands add
588. All 66 selected slice/adjacent/baseline checks pass with forced GC, ASan/UBSan
and unchanged frame limits. Comparer/nullable and reference OfType.Select variance
compositions remain diagnostic. No new grants or profiles. Frozen engine passes
the missing declaration and reaches its metadata snapshot limit; state retains its
catalog limit. Full replay is unverified. See [ADR AC0068](decisions/AC0068-default-contains.md).

### Generic classification without unused layouts (AC8.18)

Closed receiver classification now uses exact direct-base facts; generic argument
visibility uses declaration facts. Executable closed layouts keep full validation.
Two reduced probes (130 closed owners and 257 unused value arguments) pass 588
repeated native comparisons, and all 101 distinct selected checks pass. Malformed
actual storage, hidden arguments and excessive executable types still fail. No
resource bounds or profiles change. Frozen engine advances to physical-damage
selection before its snapshot limit; state remains at its catalog limit. Full
replay is unverified. See [ADR AC0069](decisions/AC0069-generic-classification-facts.md).

### Non-enum classification without fields (AC8.19)

Enum detection first checks exact direct-base facts; ordinary internal classes no
longer decode unused fields during lexical access checks. Actual enum validation
and all bounds remain intact. The added internal-owner regression passes 294
repeated O0/O2 native comparisons; all 76 selected checks pass with forced GC and
ASan/UBSan. Frozen engine clears its snapshot blocker and reaches the four-string
Concat overload. State/replay remain incomplete. See [ADR AC0070](decisions/AC0070-enum-classification-facts.md).

### Fixed-string concatenation (AC7b.7)

Three/four-string Concat preserves null/empty reduction, original identity and exact
UTF-16 through ordinary owned IL and existing construction grants. The array and
fixed overloads share the checked length guard. Six probes pass 11,664 repeated
O0/O2 comparisons; all 71 selected checks pass with forced GC, sanitizers and exact
cleanup. The ordinal Equals recognizer now filters its method name before applying
its unchanged exact contract checks. No profile or bound changes. Frozen engine
advances to a private containing-type helper call; state and full replay remain
incomplete. See [ADR AC0071](decisions/AC0071-fixed-string-concat.md).

### Private containing-type calls (AC8.20)

Nongeneric nested types can call private members on a proven containing type in
the same image. Discovery and both IR stages require the complete matching lexical
suffix; sibling, enclosing-to-child, foreign, generic-owner and older-profile paths
remain rejected. Nine probes pass 2,646 repeated O0/O2 comparisons; all 80 selected
checks pass with forced GC and sanitizers. Integer-object CIL v17, object IR v25 and
native v51; layout/runtime algorithms and bounds are unchanged. Frozen engine now
reaches a nonpublic handler delegate; state/replay remain incomplete.
See [ADR AC0072](decisions/AC0072-containing-type-private-calls.md).

### Lexically visible nongeneric delegates (AC2.5)

Private/internal nongeneric delegates retain lexical visibility through both IR
stages, including casts, arrays and expanded value signatures. Eleven probes pass
3,234 repeated native/.NET comparisons; all 181 selected checks pass with forced
GC, sanitizers and exact cleanup. Hidden generic/foreign access remains rejected.
Integer-object CIL v18, delegate IR v7, scalar delegate IR v5, object IR v26,
layout schema v21 and native v52; runtime payloads and bounds are unchanged.
Frozen engine passes handler construction and reaches the metadata snapshot limit
in forfeit preparation; state/full replay remain incomplete.
See [ADR AC0073](decisions/AC0073-hidden-delegate-visibility.md).

### Scalar-enum classification facts (AC8.21)

Unboxed Int32 enum validation retains bounded positive facts instead of structural
layout snapshots. Catalog classification no longer counts those scalar aliases as
executable layouts; boxed enums retain full validation. Two 257-enum reductions
pass 588 repeated native comparisons, and all 99 selected checks pass, including
malformed declarations, field/work limits, actual layout limits and the public
baseline. Bounds and profiles are unchanged. Frozen engine advances to missing
Enum.IsDefined in PlayerState construction; state/full replay remain incomplete.
See [ADR AC0074](decisions/AC0074-scalar-enum-classification-facts.md).

### Nominal scalar runtime type tokens (AC5b.6)

Admitted scalar/Int32-enum type literals share the canonical per-runtime identity
of boxed GetType while preserving distinct nominal types. Seven probes pass 2,058
repeated native/.NET comparisons, and all 52 selected checks pass, including both
IR stages, cache ownership/quotas and the public baseline. General value, Nullable,
floating and open tokens remain diagnostic. Integer-object CIL v19, object IR v27,
scalar boxed IR v10 and native v53; no new grants, payloads or bounds. Enum.IsDefined
and full engine/replay remain unfinished. See [ADR AC0075](decisions/AC0075-scalar-runtime-type-tokens.md).

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
See [ADR AC0076](decisions/AC0076-enum-defined.md).

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
See [ADR AC0077](decisions/AC0077-string-substring.md).

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
See [ADR AC0078](decisions/AC0078-five-component-tuples.md).

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
full saved-game replay is unfinished. See [ADR AC0079](decisions/AC0079-byte-char-vectors.md).

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
still apply. See [ADR AC0080](decisions/AC0080-character-string-construction.md).

### Nested-type cast diagnostics (AC8.22)

Cast errors now use a separate bounded declared-type display name: enclosing
names are omitted at the outer level, while generic arguments keep full runtime
names. Runtime type identity and Enum.IsDefined text retain their prior names.
Six nested class/generic/array/box reductions add 1,764 native/.NET comparisons;
all 77 selected metadata, cast and enum checks pass. Native v57 records the
diagnostic correction. No layout, admission or resource limits change. UTF-8
work exposed this defect; its library slice and full replay remain unfinished.
See [ADR AC0081](decisions/AC0081-nested-cast-names.md).

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
full replay is unfinished. See [ADR AC0082](decisions/AC0082-default-utf8.md).

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
See [ADR AC0083](decisions/AC0083-ordinal-string-prefix.md).

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
See [ADR AC0084](decisions/AC0084-guid-text-construction.md).

### Isolated engine capacity probe (AC8.23)

A disposable compiler copy with a 1,024-type diagnostic bound advances frozen
EngineStep to missing List.Sort(Comparison<T>) in GameState construction.
StateLookup translates to eight methods with 36 value layouts, 92 reference
layouts, 166 interfaces and two reference arrays. This is metadata/IR evidence
only: no C++ emission or native execution. Production limits remain unchanged;
full engine size and saved-game replay remain unverified.
See [ADR AC0085](decisions/AC0085-capacity-only-engine-probe.md).

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
See [ADR AC0086](decisions/AC0086-comparison-list-sort.md).

### Diagnostic metadata-work audit (AC8.24)

An isolated 1,024-type/262,144-work copy reaches missing Enumerable.ToList in
AdvanceSlots after 67,822 charged metadata units; the next call is List.FindIndex.
StateLookup translates eight methods with 9,917 units. These partial measurements
leave full engine capacity unknown. Production limits remain unchanged, and this
audit neither emits nor executes native code. Full replay remains unfinished.
See [ADR AC0087](decisions/AC0087-metadata-work-audit.md).

### Boxed named-value interface receivers (AC8.25)

Named non-scalar structs now dispatch through exact interface implementations,
including explicit methods and closed generic owners. Calls borrow the original
boxed payload, preserving mutable state, independent boxed copies and references.
The caller adapts arguments in its existing budgeted frame; empty payloads and
inline results retain correct packet ordering without an extra allocation.
Ten probes pass 2,940 O0/O2 native/.NET comparisons with forced GC and sanitizers;
113 distinct checks pass across the final focused runs, including four native
packet guard checks and the unchanged 39-control/8-gap baseline. Integer CIL v26,
interface plan/scalar boxed IR v12, object IR v31 and native v58 record this slice.
Scalar/enum and inherited class dispatch, generic methods on boxes and variance
adapters remain unsupported. Collection interface factories are the next library
slice. Production quotas and the frozen engine's type-limit stop are unchanged;
full replay remains unfinished. See [ADR AC0088](decisions/AC0088-boxed-interface-receivers.md).

### List interface enumeration (AC4.8)

Nonempty List interface factories now return independent boxed value cursors;
empty generic/nongeneric factories share the existing generic array cursor cache.
Legacy Current preserves exact state faults and element boxing identity. Nine
cursor probes, the advanced legacy probe and two scalar-origin regressions pass
3,528 native/.NET comparisons; six live-graph checks prove cursor retention and
cleanup. All 68 selected checks pass across focused runs, including the unchanged
39-control/8-gap public baseline. A validated named-box/nominal-reference origin
now avoids imposing scalar confinement on unrelated scalar boxes; unknown Object
casts remain checked at both IR stages. Integer CIL v27, boxed IR v9 and scalar
boxed IR v13 record this refinement; native v58 and quotas are unchanged.
Constructor fallback and ToList remain next; full replay remains unfinished.
See [ADR AC0089](decisions/AC0089-list-interface-enumeration.md).

### General enumerable List construction (AC3.11)

List(IEnumerable<T>) now admits exact enumerable aliases and ordinary/yield/boxed
cursor sources. Its granted managed factory preserves the dynamic ICollection<T>
Count/CopyTo fast branch and uses MoveNext/Current/Add with finally-disposal for
other sources. New and advanced probes pass 2,874 repeated native/.NET comparisons,
including every fault/disposal stage and dynamic count/copy failure mode. All 36
selected checks pass; a further 96 comparisons cover every list-cursor mutation
mode. Integer CIL v28 records admission; native v58 and quotas are unchanged.
The public baseline remains 39 controls/8 gaps and frozen roots still hit production
type limits. Full replay remains unfinished.
See [ADR AC0090](decisions/AC0090-enumerable-list-construction.md).

### List AddRange (AC3.12)

AddRange now preserves the pinned collection Count/CopyTo path, checked growth,
post-copy live size/version updates, self-copy and per-item enumerable fallback.
Nine probes pass 2,382 repeated native/.NET comparisons, including reentrant
callbacks, captured partial stores and every enumerator failure/disposal stage.
All 44 selected checks pass across final runs. The public baseline advances to
40 native controls and seven gaps; compiler/native profiles and quotas are
unchanged. Frozen engine/state roots still hit production type bounds and full
replay remains unfinished. See [ADR AC0091](decisions/AC0091-list-add-range.md).

### Captured list query materialization (AC6.13)

Where/Select ToArray now captures a List backing array and logical count once,
including composed Where/Select chains. The private managed prefix cursor keeps
old storage rooted across callback allocation, growth, clearing and replacement;
ordinary enumeration retains version checks. The pinned AsSpan corruption guard
rejects a count outside its captured storage. No Span API, compiler/native profile,
grant or production-budget change is introduced. Five probes pass 798 repeated
O0/O2 native/.NET comparisons with forced GC and sanitizers. Other list terminal
specializations and ToList remain separate work; full saved-game replay remains
unfinished. See [ADR AC0092](decisions/AC0092-captured-list-materialization.md).

### Query list materialization (AC6.14)

Enumerable.ToList validates source and selects the owned query materializer or
existing collection/enumerable list factory. Where, Select, Distinct, OfType and
ordered queries copy private materialized arrays into exact-capacity lists;
Concat requests its cheap count and appends each source in order. These managed
temporaries use existing traced storage and quotas. Select now distinguishes
negative IList counts for ToArray versus ToList and checks fixed destinations
before invoking selectors, including composed projections. No compiler/native
profile, helper grant or budget changes are required. Full engine/saved-game replay
remains unfinished. See [ADR AC0093](decisions/AC0093-query-list-materialization.md).

### Predicate list search (AC3.13)

All three FindIndex overloads now use the pinned start/count/match validation order
and resource messages. Search captures its ending index and reads current backing
storage on each iteration, preserving predicate mutations without enumerator
version checks. Five probes pass 2,148 repeated native/.NET comparisons at O0/O2
with forced collection, sanitizers and exact quota cleanup. No compiler/native
profile, grant or budget changes are introduced. Full engine/replay remains open.
See [ADR AC0094](decisions/AC0094-list-find-index.md).

### Same-assembly internal static fields (AC8.26)

Discovery and logical/scalar validation now share a static-field access rule.
Assembly and FamORAssem fields require the same content image and validated,
nonempty declaring-type facts for caller and target. The target must be visible
within that assembly or its proven lexical scope. Closed generic owners preserve
exact storage identities. Private/protected-only cross-owner access, foreign
assembly access, static addresses and readonly writes outside the declaring
initializer remain rejected. CIL v29, object IR v32 and native v59 record the
admission change; layout v22, runtime algorithms and production bounds are unchanged.
Eight probes pass 2,352 native/.NET comparisons and all 111 selected checks pass.
Full engine/replay remains unfinished. See [ADR AC0095](decisions/AC0095-assembly-static-access.md).

### Engine method-capacity diagnostic (AC8.27)

After AC8.26, the existing 1,024-type/262,144-metadata diagnostic copy reaches its
unchanged 512-method limit in ResolutionKernel.StepReady. A separate copy raises
only its method bound to 1,024 and reaches boxed Int32-enum String.Format in
VictoryEvaluators.Resolve IL_001a after 91,112 metadata-work units. StateLookup
still translates eight methods with 9,917 charged units. Neither copy emits or
executes native code. Production bounds stay at 256 types, 512 methods and 65,536
metadata units; full closure capacity and saved-game replay remain unverified.
See [ADR AC0096](decisions/AC0096-engine-method-capacity.md).

### Composite-format suffix transport (AC7b.12)

The shared parser passes the original format string and its suffix range to the
exact private renderer. Ordinary non-IFormattable values ignore that range before
the existing payload borrow/ToString call. No substring allocation or new native
operation is added. Eight focused checks pass, including 2,940 O0/O2 native/.NET
comparisons with forced collection and sanitizers. Enum rendering is still
diagnostic; this is its parser prerequisite. See [ADR AC0097](decisions/AC0097-format-range-transport.md).

### Bounded Int32 enum composite formatting (AC7b.13)

Directly boxed Int32 enums with unique values and no custom attributes now use the
shared one/two-argument String.Format parser. Ordinary managed bodies implement
default/G/D/X/F suffixes, exact format errors, numeric fallback and unsigned F
decomposition. Four exact private grants provide scalar unboxing, constant count
and immutable sorted value/name table reads. The new EnumEntry operation checks
metadata, index type, result type, roots and exceptional edges at both IR stages;
native bounds/descriptor checks and name allocation preserve managed faults.

Six probes provide 7,956 O0/O2 native/.NET comparisons with forced collection and
sanitizers. All 74 selected distinct checks pass after correcting the stale scalar
diagnostic expectation. Public baseline remains 40 native controls and seven gaps
on native v60/object IR v33/integer CIL v30; runtime layout and production limits
are unchanged. Flags attributes, aliases, other carriers, providers and general
IFormattable dispatch remain diagnostic. Decimal fallback retains the existing
invariant culture contract. See [ADR AC0098](decisions/AC0098-int32-enum-formatting.md).

### One-field and long tuples (AC5b.9)

The frozen engine uses ValueTuple<T1,...,T7,ValueTuple<T8>> in EndParticipation.
The committed AC7b.13 diagnostic refresh reached that dependency after 103,353
metadata units. Add the one-field and eight-parameter declarations, constructors,
storage and typed equality. Long tuples retain a struct Rest; an authenticated
private closed-type predicate checks its exact admitted tuple identity before field
assignment. Public ITuple implementations do not qualify, while a default nested
long tuple qualifies regardless of its own Rest contents. Generic value lowering
supplies copies, return, heap/vector, boxed and nullable storage.

Twelve probes provide 5,040 O0/O2 native/.NET comparisons with forced GC and
sanitizers. All 31 selected distinct checks pass, including nominal reference
contracts, ordinary tuple regressions and the 40-control/seven-gap public baseline.
Integer CIL advances to v31; no native IR, layout or resource-bound changes.
Other tuple arities and non-pair hashing/comparison/text/object equality remain
diagnostic. See [ADR AC0099](decisions/AC0099-one-and-long-tuples.md).

### Dictionary checked insertion and indexed reads (AC3.14)

Add and indexer reads use existing insertion/lookup storage and preserve exact
duplicate/missing-key error text. A private granted text bridge selects managed
invariant integer, string or supported ordinary named-value rendering. The private
insertion key is passed byref so duplicate errors observe hash callback mutations;
lookup errors retain the original key. Formatting remains outside shared insertion,
preserving TryAdd/setter scope for keys without text support. General IFormattable
and remaining reference-key formatting still fail compilation.

Eleven probes provide 5,544 O0/O2 native/.NET comparisons with forced collection
and sanitizers, including reentrant ToString mutation of the same dictionary.
All 52 selected distinct checks pass, including dictionary state/ownership
regressions; the public baseline advances to 42 native controls and five gaps.
Integer CIL v32; native IR/layout/resource bounds unchanged. See
[ADR AC0100](decisions/AC0100-dictionary-add-read.md).

### First-element terminals and captured lists (AC6.15)

Non-predicate First and FirstOrDefault now reuse owned query dispatch, one IList
Count/index read or a single enumerator step with disposal. First preserves the
empty-versus-default distinction. Review also reproduced and fixed predicate and
filtered-list First mutation behavior: capture original backing storage/count and
return the element loaded before the callback. A private first-element traversal
mode carries that behavior through projections without consuming existing cursors.

Sixteen probes supply 9,792 O0/O2 native/.NET comparisons with forced GC and
sanitizers. All 45 selected checks pass, including predicate/distinct regressions
and the unchanged 42-control/five-gap baseline. Grants, compiler/native profiles,
runtime layout and production bounds are unchanged. Full frozen-engine execution
and saved-game replay remain unfinished. See [ADR AC0101](decisions/AC0101-direct-first.md).

### HashSet cursors and enumerable construction (AC4.9 / AC3.15)

HashSet now supplies mapped value cursors and boxed interface cursors, with exact
version/current/reset behavior and the shared empty cursor. Enumerable construction
validates collection, reads ICollection Count once and enumerates with disposal.
Existing-set sources preserve the dense clone versus sparse reinsertion threshold;
duplicate-heavy construction trims stored hashes without callbacks.

Seventeen probes provide 7,956 O0/O2 native/.NET comparisons with forced GC and
sanitizers. All 38 final selected checks pass, including set lifetime/grant tests
and the unchanged 42-control/five-gap baseline. Int64 enumerable construction still
reaches the unsupported wide vector needed by its empty cursor; an explicit negative
probe pins that dependency. Wide fields in admitted value keys pass. Custom
comparers, capacity constructors, public copying, set algebra and serialization
remain unimplemented. Compiler/native profiles, runtime layout and resource limits
are unchanged; full engine/replay remains unfinished. See [ADR AC0102](decisions/AC0102-set-enumeration.md)
and [ADR AC0103](decisions/AC0103-enumerable-set-construction.md).

### Int64 vectors and array helpers (AC7a.2)

Int64 vectors now have a distinct type identity and use the existing UInt64 array
buffer, with one 64-bit slot per element. Discovery, signatures, generic CIL,
logical/scalar IR and native loads/stores preserve all bits and checked fault order.
Exact array helpers support indexed access, search/mutation faults, copying and
enumeration; runtime type/cast text and empty caches distinguish System.Int64[].
List<long> and enumerable HashSet<long> now have native differential evidence.

Thirteen probes supply 4,326 O0/O2 native/.NET comparisons. All 105 selected checks
pass, including malformed IR at both stages, descriptor/quota/recovery tests,
existing array suites and the 42-control/five-gap baseline. Scalar unsigned views
preserve bits; explicit UInt64 vectors and unsigned enumeration/copy recipes remain
diagnostic, as do primitive element borrows. Integer CIL v33, object layout v23,
object IR v34, interface plan v13 and native v61 record this extension. Resource
limits and owned library bodies are unchanged. Full engine/replay remains
unfinished. See [ADR AC0104](decisions/AC0104-int64-vectors.md).

### Constant array data (AC7a.3)

The integer CIL v34 profile accepts the uninterrupted constant/newarr/dup/field-token/
InitializeArray pattern for Byte, Char, Int32 and Int64 vectors: 1–256 elements,
at most 2,048 file-backed data bytes. The exact selected helper requires a manifest
grant; the metadata reader checks readonly RVA storage, size, visibility and layout.
Little-endian data becomes existing checked array stores. General runtime field
handles, other patterns and other element types remain diagnostic.

Eight probes supply 2,352 repeated O0/O2 native/.NET comparisons with forced GC and
ASan/UBSan. Metadata/control-flow/grant negatives and adjacent core/type-identity
checks pass. The public baseline advances to 43 native controls/four gaps. Native
v61, object layout v23 and production resource limits are unchanged. Full engine
execution and saved-game replay remain unfinished. See [ADR AC0106](decisions/AC0106-constant-array-data.md).

### Dictionary capacity construction (AC3.16)

Dictionary(int capacity) now validates negative input, leaves zero capacity lazy,
and allocates positive capacities using the existing prime table. Constructor and
first insertion share initialization that publishes fields after both arrays are
allocated. Default equality/hash and other dictionary behavior are unchanged.

Six probes supply 3,276 repeated O0/O2 native/.NET comparisons. Ten native quota
failure/recovery cycles check released roots and reusable runtime state. All 39
selected checks pass, including dictionary/view/error-message regressions and the
43-control/four-gap public baseline. Compiler/native profiles and production limits
are unchanged. Comparer and other constructors remain diagnostic; full engine and
saved-game replay remain unfinished. See [ADR AC0107](decisions/AC0107-dictionary-capacity.md).

### List predicate existence (AC3.17)

List.Exists now uses FindIndex(match) != -1, matching the pinned source. Four
probes provide 2,160 repeated O0/O2 native/.NET comparisons covering null/empty
behavior, short-circuiting, faults and predicate mutation of scalar, reference and
reference-bearing value lists. All 25 selected checks pass, including FindIndex
regressions and the unchanged 43-control/four-gap public baseline. Forced GC and
sanitizers remain enabled. Profiles and resource limits are unchanged; full engine
and saved-game replay remain unfinished. See [ADR AC0108](decisions/AC0108-list-exists.md).

### Scoped scalar/reference ref locals (AC5b.10)

The integer CIL profile now admits Int32, Int64 and reference borrows stored in
method locals with one assignment site that dominates every read. The bounded
proof runs at both IR stages. Each local publishes its heap-owner root and retains
existing frame-token lifetime checks. Ref returns, heap-stored refs, stack joins,
inline-value ref locals and ref locals in methods with exception regions remain
unsupported. Reference-array slot borrows are the next prerequisite.

Seven probes supply 2,058 repeated O0/O2 native/.NET comparisons. All 98 selected
checks pass, including five malformed-IR cases at both stages, source boundaries,
root plans, native carrier/lifetime regressions and the 43-control/four-gap baseline.
Integer CIL v35, managed-location IR v2, scalar-location IR v3, object IR v35 and
native v62 record this extension. Layout, library and production limits are
unchanged. Full engine/replay remains unfinished. See [ADR AC0109](decisions/AC0109-scoped-ref-locals.md).

### Reference-array element borrows (AC7a.4)

Exact reference-array element addresses now compose with scoped ref locals.
Carriers retain and root the original array owner across allocations and source
variable replacement. Writable addresses check null, bounds and exact runtime
array type in .NET order; covariance does not permit a differently typed writable
slot. Existing carrier checks protect ownership, readonly state and payloads.
Six probes provide 1,764 native/.NET comparisons, with 54 focused positive,
safety and regression checks passing at O0/O2 under ASan/UBSan and forced GC.
Integer CIL v36, value-array IR v3, scalar-location IR v4, object IR v36 and native
v63 record the change. Layout, library and production bounds are unchanged.
Primitive array borrows and full engine/replay remain unfinished.
See [ADR AC0110](decisions/AC0110-reference-array-borrows.md).

### Int64 delegates (AC2.6)

The integer-object profile admits exact Int64 delegate parameters and results.
Signed scalar target identity is authenticated before lowering, and native dispatch
uses a dedicated typed 64-bit function pointer with exact bit preservation. Existing
receiver roots, packets and fault cleanup compose unchanged. Eight probes provide
2,352 new O0/O2 native/.NET comparisons; all 89 selected checks pass under forced
GC and ASan/UBSan. Integer CIL v37, delegate IR v8, scalar delegate IR v6, object IR
v37, layout v24 and native v64 record the extension. Unsigned/narrow/enum delegate
signatures and general byrefs remain diagnostic. No library or capacity change;
full engine/replay remains unfinished. See [ADR AC0111](decisions/AC0111-int64-delegates.md).

### Proven ordinal string list sorting (AC3.18)

List<string>.Sort(IComparer<string>) accepts a directly proven selected
StringComparer.Ordinal getter in the same block. An exact ListOrdinalSort grant
selects the managed bridge; original getter evaluation, null-receiver order,
version changes, captured storage, UTF-16 order and identity are preserved.
Null/custom/aliased comparers, lookalike getters, joins and nonstring lists remain
diagnostic. Four probes provide 1,170 native/.NET comparisons; 37 distinct focused
and regression checks pass. Integer CIL v38 records the extension; native v64,
layout v24 and production bounds remain unchanged. Full engine/replay is unfinished.
See [ADR AC0112](decisions/AC0112-proven-ordinal-list-sort.md).

### Proven constrained enum comparison (AC5b.11)

Enum.CompareTo(Object) accepts a constrained Int32 enum receiver with a directly
boxed matching enum or literal null. The exact EnumCompareTo grant and declaration
flags are authenticated. Receiver reading follows argument evaluation; matching
boxes use checked unboxing and signed comparison, while null returns one.
Dynamic targets, different types, aliases, boxed receivers, wide enums and Enum
constraints remain diagnostic. Six probes provide 1,764 native/.NET comparisons;
74 selected checks pass under forced GC and ASan/UBSan. Integer CIL v39 records
the extension; native v64/layout v24 and production limits remain unchanged.
Full engine/replay remains unfinished. See [ADR AC0113](decisions/AC0113-proven-enum-comparison.md).

### Proven ordinal String.Compare (AC7b.14)

String.Compare(String,String,StringComparison) accepts a directly proven Ordinal
constant under its exact StringOrdinalCompare grant. Shared text-bridge validation
selects the existing managed CompareOrdinal helper and preserves argument order,
null behavior and exact integer results. Culture/ignore-case/unknown/aliased modes
remain diagnostic. Four probes provide 4,338 new native/.NET comparisons; all 47
selected checks pass. Integer CIL v40 records the extension; native v64/layout v24
and production limits remain unchanged. Full engine/replay remains unfinished.
See [ADR AC0114](decisions/AC0114-proven-ordinal-string-compare.md).

### Lexically visible nonpublic enum operations (AC5b.12)

Direct typeof, boxing/unboxing, boxed type tests and constrained comparison now
accept admitted internal/private nested Int32 enums under the existing assembly
and lexical-scope checks. Hidden generic arguments, protected/closed generic
nesting and foreign hidden declarations remain diagnostic. Eight new probes give
2,352 O0/O2 native/.NET comparisons with forced GC and ASan/UBSan. Integer CIL v41
records admission; native v64/layout v24 and production limits are unchanged.
Full engine/replay remains unfinished. See
[ADR AC0115](decisions/AC0115-nonpublic-enum-operands.md).

### Int32 enum default key hashing (AC3.19)

The core-private ClosedKeyHash operation selects the existing Int32.GetHashCode
body for validated public nongeneric Int32 enums. One direct hash witness supplies
294 native/.NET comparisons, with exact grant denial and unsupported boundaries.
Enum dictionary/set roots remain diagnostic at their separate enum-array interface
dependency; no collection-execution claim follows from hashing alone. Integer CIL
v42 changes admission; native v64/layout v24 and production budgets stay unchanged.
See [ADR AC0116](decisions/AC0116-int32-enum-key-hashing.md).

### Internal exception access (AC8.29)

Top-level internal fieldless nongeneric exceptions and their accessible public or
internal forwarding constructors are admitted in the integer profile. Explicit
type/constructor uses check the actual caller even when metadata is cached.
Five probes provide 1,470 native/.NET comparisons; foreign access and extra
constructor behavior remain diagnostic. Integer CIL v43 records admission;
native v64/layout v24 and production bounds stay unchanged. The real engine's
message-building custom constructor and full replay remain unfinished.
See [ADR AC0117](decisions/AC0117-internal-exception-access.md).

### Bounded exception message construction (AC8.30)

Verified one-string custom constructors can concatenate a constant prefix, their
argument and a constant suffix before calling the direct selected Exception or
InvalidOperationException string constructor. The exception is allocated/rooted
first; a checked one-time message initialization preserves its identity afterward.
Forwarding children inherit the proof. Other bodies/bases remain diagnostic.
Six native probes give 1,764 comparisons; all 86 selected checks pass, including
both IR stages and native ownership/quota/GC rejection. Integer CIL v44,
managed/scalar exception IR v7 and native v65 record the change; layout v24 and
production bounds remain unchanged. Full engine/replay remains unfinished.
See [ADR AC0118](decisions/AC0118-bounded-exception-message-construction.md).

### Authorized hidden Int32 enum generic arguments (AC5b.13)

Internal/private Int32 enums can be generic arguments when the original caller has
lexical access. Generic bodies may carry their already-bound enum opaquely through
copies, boxes and Nullable helpers, but cannot name hidden declarations in foreign
IL. Six probes provide 1,764 native comparisons; all 87 selected checks pass.
Hidden structs, other enum widths and enum-array interfaces retain their separate
boundaries. Integer CIL v45 changes admission; native v65/layout v24 and production
limits stay unchanged. Full engine/replay remains unfinished.
See [ADR AC0119](decisions/AC0119-hidden-enum-generic-arguments.md).

### AC3.20: List predicate lookup (2026-10-07)

List.Find now scans live size/storage and rereads the matched slot after the
callback, matching pinned .NET mutation behavior. Four probes supply 2,160
repeated O0/O2 comparisons for scalar, reference and reference-bearing values.
All 33 selected checks pass, plus 15 checks after strengthening null fault order.
Forced GC, sanitizers and existing resource limits remain. No compiler/native
profile or grant changes. Full engine execution/replay remains unfinished.
See ADR AC0120 and artifacts/alchemy-list-find.

### AC3.21: Explicit ordinal string-set construction (2026-10-07)

HashSet<string>(StringComparer.Ordinal) now accepts the exact selected getter
in the same block under a dedicated factory grant. Managed set storage preserves
explicit comparer identity so default-comparer copies rebuild in the right order.
A differential copy-after-removal failure supplied the regression. Four probes
provide 1,176 repeated native/.NET comparisons; all 48 selected checks pass with
forced GC/sanitizers. Integer CIL v46; native v65/layout v24 and production limits
unchanged. General comparers, full engine execution and saved-game replay remain
unverified. See ADR AC0121 and artifacts/alchemy-ordinal-set.

### AC7b.15: Parameterless integer text (2026-10-07)

Int32/UInt32/Int64/UInt64.ToString() now use existing managed decimal formatting
and the shared small-integer cache under exact overload grants. Default text
uses the core's existing fixed-invariant contract; ambient culture changes are
unsupported. Seven probes provide 9,630 repeated native/.NET comparisons with
forced GC/sanitizers. All 30 selected checks and eight strengthened focused
checks pass. Integer CIL v47; native v65/layout v24 and resource limits unchanged.
Full engine execution/replay remains unfinished. See ADR AC0122 and
artifacts/alchemy-default-integer-text.

### AC8.32: Protected static base helpers (2026-10-07)

Direct protected static calls now require a proven closed base-class chain and
matching class visibility; private-protected additionally requires the same
assembly. IR independently retains and checks ancestry metadata even for static
callers with no allocated object. Six fixtures and one foreign probe provide
2,058 repeated native/.NET comparisons. All 158 selected checks pass, including
detached proof corruption and catalog/access regressions. Integer CIL v48/object
IR v38; native v65/layout v24, core/grants and production limits remain unchanged.
Full engine execution/replay is unfinished. See ADR AC0124 and
artifacts/alchemy-protected-static.

### AC5b.14: Hidden inline generic arguments (2026-10-07)

Lexically accessible hidden structs can now be generic arguments, including
readonly reference-bearing payloads in mixed Func signatures. Generic helpers
may copy/box their already-bound T; explicit foreign hidden-type names and
private members remain inaccessible. Eight native probes provide 2,352 repeated
comparisons; all 82 selected checks and 23 strengthened focused/baseline checks
pass with forced GC/sanitizers. Hidden-value generic arrays retain their separate
diagnostic. Integer CIL v49; object IR v38/native v65/layout v24 and production
limits unchanged. Full engine execution/replay remains unfinished. See ADR AC0126
and artifacts/alchemy-hidden-value-generics.

### AC3.22: Explicit ordinal dictionary construction (2026-10-07)

Dictionary<string,TValue>(StringComparer.Ordinal) now uses the shared exact
ordinal collection-constructor proof with a separate factory grant. Existing
string-key algorithms and value ownership are reused. Four new probes provide
1,176 repeated native/.NET comparisons; all 58 selected checks pass with forced
GC/sanitizers. Comparer exposure/copy constructors remain diagnostic and require
separate identity semantics. Integer CIL v50; object IR v38/native v65/layout v24
and production limits unchanged. Full engine execution/replay remains unfinished.
See ADR AC0127 and artifacts/alchemy-ordinal-dictionary.

### AC6.16: Indexed Where (2026-10-07)

The indexed Where overload now uses deferred source enumeration and checked
input indices, preserving .NET 10 fault cleanup, explicit-disposal reuse and
retained Current. Nine probes provide 2,970 repeated native/.NET comparisons;
two exact GC checks verify source-cursor release and retained payload lifetime.
All 62 selected checks pass with forced GC and sanitizers. Nonindexed query
optimizations, grants, profiles and production limits are unchanged. Full engine
execution/replay remains unfinished. See ADR AC0128 and
artifacts/alchemy-indexed-where.

### AC7c.1: Portable SHA-256 algorithm (2026-10-07)

Cilica.Library now supplies an owned managed SHA-256 digest algorithm following
FIPS 180-4. It uses fixed private working arrays, unsigned round arithmetic and
wide padding lengths. Eight focused checks include 4,746 native/.NET comparisons;
managed tests additionally cover a million-byte known answer and 128 random inputs.
The framework factory, lifecycle/disposed exception, and unchanged engine
fingerprint remain unimplemented. No compiler/core grants, profiles or limits
change. Full engine execution/replay remains unfinished. See ADR AC0129 and
artifacts/alchemy-sha256-algorithm.

### AC8.34: Disposed-object exception payloads (2026-10-07)

Selected ObjectDisposedException string constructors and ObjectName/Message now
preserve exact UTF-16, default/null behavior, identity and typed catch/rethrow.
Explicit ObjectName IR reuses the existing traced native name slot only for the
validated disposed exception. Both IR stages reject malformed payloads. All 89
selected checks pass, including 36,864 new native/.NET comparisons, exact grants,
GC and allocation-failure recovery. Integer CIL v51, exception IR v8/native v66;
layout v24 and discovery limits unchanged. SHA256 lifecycle binding and full engine
execution/replay remain unfinished. See ADR AC0130 and
artifacts/alchemy-disposed-exception.

### AC8.35: Final class calls (2026-10-07)

Final virtual class methods now use the existing exact-body call with its receiver
null check. Arguments still evaluate first; inherited final bodies remain fixed
when a derived class hides the method. Interface calls retain interface dispatch.
This also permits null-only closures whose abstract owner has no concrete receiver.
Both IR validators require final-method evidence for this direct checked call.
Integer CIL v52 and object IR v39 record the admission; native v66/layout v24 and
all resource limits are unchanged. Five probes supply 1,470 repeated native/.NET
comparisons at O0/O2 with forced GC and ASan/UBSan. All 85 focused and surrounding
dispatch checks pass, including removal of final evidence at both IR stages.
Review covered argument/null order, fixed target identity, interface separation,
rooting and unchanged native emission. Full engine/replay remains unfinished.
See ADR AC0131.

### AC7c.2: Whole-array SHA-256 provider (2026-10-07)

SHA256.Create, HashAlgorithm.ComputeHash(byte[]) and disposal now bind to the
owned portable managed implementation. Independent providers/results, repeated
hashing, exact disposed-before-null ordering and UTF-8 composition pass 4,536
repeated O0/O2 native/.NET comparisons with forced GC and sanitizers. Five
streaming/state/custom-provider probes remain diagnostic. All 21 selected provider,
algorithm and public-baseline checks pass. The baseline advances to 44 controls
and three gaps. No new intrinsic grants, native algorithms or production limits.
Full frozen fingerprint execution and engine/replay remain open. See ADR AC0132
and artifacts/alchemy-sha256-provider (core/manifest/fixture pins included).

### AC7a.5: Nominal Int32 enum vectors (2026-10-07)

Int32 enum arrays now retain nominal runtime/empty-cache identity while sharing
the existing four-byte scalar storage. Compatible Int32/enum array views share
mutations, and constant data preserves exact bits. Eleven probes plus the advanced
public control supply 3,528 repeated O0/O2 native/.NET comparisons with forced GC
and sanitizers. Exact static roots, cast fault text and six malformed catalog
mutations pass; an additional forged interface plan is rejected. All 76 selected
checks pass, including surrounding wide-array, constant-data, empty-array,
interface and public-baseline regressions. The baseline is 45 controls/two gaps.

Generic enum contracts, actual enum-array interface dispatch, element borrows,
non-Int32 enums and UInt32 vectors remain diagnostic. Integer CIL v53, object IR
v40, layout v25 and native v67 record the new nominal descriptor/facts. Production
limits and grants remain unchanged. See ADR AC0133 and
artifacts/alchemy-enum-array-audit. Full engine execution/replay is still open.

### AC5a.4: Invariant Int32 parsing (2026-10-07)

The exact four-argument Int32.TryParse form now supports proven Integer style and
InvariantCulture, including overflow, ASCII whitespace/signs, trailing NULs and
zeroed failed outputs. Both IR stages retain exact parser identity/signature and
operand proof. Flags metadata remains present; other styles/providers/overloads
and flags formatting stay diagnostic. All 43 selected checks pass, including
2,892 new native/.NET comparisons and 131,648 managed-only parser cases. Public
baseline remains 45 controls/two gaps. Integer CIL v54/object IR v41; layout v25,
native v67 and production limits unchanged. See ADR AC0134 and
artifacts/alchemy-invariant-int32-parse. Full engine/replay remains open.

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

### AC3.25: Ordinary reference-key equality and hashing (2026-10-07)

Closed ordinary class keys now select IEquatable<T> from the static key type's
exact interface/base closure, or use virtual Object.Equals when that contract is
absent. Hashing uses the existing virtual GetHashCode path. Ordinary managed
helpers preserve null guards and reference identity; no native equality callback
or identity-hash algorithm is added. Default object hash values remain opaque.

The boxed-dispatch safety check uses actual, validated call-site receiver types:
an unrelated boxed enumerator cannot enter an ordinary-class reference. Object
aliases, vector keys and special runtime receivers retain existing diagnostics;
nullable value search and reference-key error text are separate remaining work.
Integer CIL v59 records admission; object IR v41, native v67, layout v25 and all
production limits are unchanged. See [ADR AC0141](decisions/AC0141-reference-key-operations.md).
Full-engine/fingerprint execution and saved-game replay remain unverified.

### AC3.26: Sealed reference-key error text (2026-10-07)

Dictionary duplicate/missing messages now render sealed ordinary class keys.
The compiler resolves the exact inherited ToString target: ordinary overrides
remain normal managed calls; the default Object implementation uses a private,
independently granted closed-type-name literal. Nested and closed generic names
use metadata's bounded runtime spelling. Existing dictionary null checks run
before rendering. No public reflection or general Object.ToString API is added.

Non-sealed keys and inherited IFormattable contracts remain diagnostic. Integer
CIL v60 records admission and ReferenceKeyTypeName grants the private helper;
object IR v41, native v67, layout v25 and all production limits are unchanged.
See [ADR AC0142](decisions/AC0142-sealed-reference-key-text.md).
Full-engine/fingerprint execution and saved-game replay remain unverified.

### AC8.38: Measured diagnostic type capacity (2026-10-07)

The frozen engine reaches 1,025 roots at the diagnostic 1,024-type cap. A
counter-only copy records 74 vectors, 92 values, 859 references, 2,926 method
declarations and 69,045 instructions. Raising only that copy to 2,048 types
reaches Object.ToString on a duplicated StringBuilder receiver in unchanged
SemanticFingerprint.Create IL_000c after 368,197 metadata units. StateLookup
remains eight methods/9,927 units. Production limits and all other diagnostic
limits are unchanged; no engine emission/execution. See
[ADR AC0143](decisions/AC0143-engine-type-capacity.md).

### AC8.39: Retained-stack sealed receiver proof (2026-10-07)

Zero-argument public virtual calls can now prove a sealed reference receiver
retained below intervening call arguments or duplicated on the same block's
stack. A backwards scan tracks one stack slot for at most 256 instructions;
branch and exception boundaries, unknown operations and unproven types stop it.
Normal translation independently validates the actual typed receiver, selected
slot, null check and exception ordering. No call is reordered or executed while
compiling. Integer CIL v61; other IR/native profiles and budgets are unchanged.
See [ADR AC0144](decisions/AC0144-retained-stack-receiver-proof.md).
This admits the frozen fingerprint writer's stack pattern, not yet its execution.

### AC7b.18: Pair tuple formatting (2026-10-07)

ValueTuple<T1,T2>.ToString now renders its fields by reference in left-to-right
order, preserving mutable value callbacks and null strings. Exact private
TupleElementText grants select invariant signed/unsigned 32/64-bit integer
formatting, string identity or an implemented ordinary value ToString override.
Nested pairs reuse that path; dictionary error messages and directly boxed pair
formatting compose with it. IFormattable components use their ordinary ToString,
as tuple formatting requires. Non-string reference, enum, nullable and narrow
components, and other tuple arities, remain diagnostic. No allocation-count or
interning equivalence is claimed. Integer CIL v62; existing IR/native profiles
and budgets remain unchanged. See [ADR AC0145](decisions/AC0145-pair-tuple-text.md).
Full engine/fingerprint execution and saved-game replay remain open.


### AC8.40: BeforeFieldInit struct storage (2026-10-07)

Closed structs with BeforeFieldInit initializers now share the existing lazy
static-storage path. Static fields are excluded from inline layouts, including
otherwise empty structs and static copies of the declaring value. Generic state,
reference roots, reentrant zero reads and cached failures retain the existing
runtime behavior. Strict/module initialization and static addresses remain
unsupported. Integer CIL v63; IR/native profiles and budgets are unchanged.
Six probes provide 1,764 O0/O2 native/.NET comparisons, with forced collection,
ASan/UBSan, plus runtime isolation/root-release checks. All 88 selected
struct/static/layout checks pass. See [ADR AC0146](decisions/AC0146-beforefieldinit-structs.md).
The frozen engine previously reached EffectTarget layout after 372,047 metadata
units; complete engine/fingerprint execution and saved-game replay remain open.


### AC7a.6: Int32 enum array interfaces (2026-10-07)

Validated enum identities now survive closed array-interface metadata. Generic
helpers retain the requested element type; nongeneric enumeration retains the
actual array element type when boxing. Compatible integer/enum views share
four-byte storage, including CopyTo, while runtime type identity remains distinct.
Unused vector pruning also removes corresponding dispatch facts. Strict catalog,
grants, rooting and scalar-box confinement checks remain in force.
Twelve new probes and three formerly negative controls provide 4,410 repeated
O0/O2 native/.NET comparisons with ASan/UBSan and forced collection. The 100
selected array/copy/enumeration/confinement checks pass. Integer CIL v64, object
IR v42, layout v26, interface plan v14 and native v68 record the extension;
core and budgets are unchanged. See [ADR AC0147](decisions/AC0147-int32-enum-array-interfaces.md).
The frozen engine previously reached IEnumerable<PriorityWindowTiming> after
386,000 metadata units; full engine/fingerprint execution and replay remain open.


### AC8.41: BeforeFieldInit interface targets (2026-10-07)

Closed interface planning now admits BeforeFieldInit owners, matching ordinary
method and layout admission. Existing static-field access performs initialization;
interface dispatch adds no new trigger. Strict/module initialization and the
older non-closed profile remain diagnostic. Six probes supply 1,764 repeated
O0/O2 native/.NET comparisons with forced GC and ASan/UBSan; 90 focused checks pass.
Integer CIL v65; IR/native profiles, core and budgets are unchanged. See
[ADR AC0148](decisions/AC0148-initialized-interface-targets.md).
The frozen engine previously reached SpellResolutionHandler.get_Id after 440,238
metadata units. Full engine/fingerprint execution and saved-game replay remain open.


### AC8.42: Single-candidate interface variance dispatch (2026-10-07)

Reference receivers can now dispatch through one compatible declared generic
interface when no exact contract is present. Exact matches take precedence;
multiple candidates, generic variant methods and value/byref adaptation remain
diagnostic. Selected declaration identity and compatible argument/result types
are retained and validated before and after lowering. Seven new probes and one
promoted control provide 2,352 O0/O2 native/.NET comparisons with forced GC and
ASan/UBSan. The 152 distinct focused checks pass. Integer CIL v66, object IR v43,
interface plan v15 and native v69 record this extension; core and budgets are
unchanged. See [ADR AC0149](decisions/AC0149-variant-interface-dispatch.md).
The frozen engine previously reached an OfTypeIterator<RoundStartedEvent> through
IEnumerable<DomainEvent> after 466,593 metadata units. Full engine/fingerprint
execution and saved-game replay remain open.


### AC8.43: Reuse type relationships within interface planning (2026-10-07)

Frozen closure discovery cleared variance and exhausted 524,288 metadata units.
Two copied 786,432-unit probes hit their unchanged 60-second cap. A 600,000-unit
phase audit isolated repeated assignment-graph construction inside interface
planning: the first 2,997 method reads took 1.3 seconds, followed by about 40
seconds in the planner before the budget diagnostic.

Reuse assignment rules within one immutable planning/validation pass, with fresh
rules for each new catalog. All 152 focused interface checks pass. The same frozen
600,000-unit probe drops from 41,913 ms to 2,607 ms with identical diagnostics and
all metadata-work buckets. This is one measured compiler probe, not runtime or
whole-application throughput. Production limits, profiles and managed semantics
are unchanged. See [ADR AC0150](decisions/AC0150-interface-planning-reuse.md).
The 786,432-unit copied diagnostic budget remains subject to the 60-second cap;
full engine/fingerprint execution and saved-game replay remain unverified.


### AC8.44: Interface and declaration signature context (2026-10-07)

Bound interface packets no longer borrow the entry method's lexical access
context. Method parameter/return/local storage uses the declaring method's own
closed context. Original source declarations and operand tokens still enforce
lexical visibility, and selected helpers still require exact intrinsic grants.
Four two-assembly probes provide 1,176 repeated O0/O2 native/.NET comparisons
with forced GC and ASan/UBSan; all 201 focused access/value/interface checks pass.
Integer CIL v67 records admission; IR/native profiles and budgets are unchanged.
See [ADR AC0151](decisions/AC0151-interface-signature-context.md).
The frozen engine previously reached hidden WaveKind inside a generic array
helper signature after 776,942 metadata units. Full engine/fingerprint execution
and saved-game replay remain open.


### AC8.45: Retain delegate metadata for vector helpers (2026-10-07)

Validated delegate declarations now retain their exact constructed metadata
identity for selected array-interface helper binding. Delegate shape validation,
visibility, source access and helper grants are unchanged. Four probes cover
nongeneric Count, indexed private-delegate reads, closed Func copies and
nongeneric Clear, with captured target lifetime checks. They provide 1,176
repeated O0/O2 native/.NET comparisons with forced GC and ASan/UBSan; all 64
focused checks pass. Integer CIL v68 records admission; IR/native profiles and
budgets are unchanged. See [ADR AC0152](decisions/AC0152-delegate-vector-metadata.md).
The frozen engine previously reached a CommandHandler[] array helper after
780,053 metadata units. Full engine/fingerprint execution and replay remain open.


### AC7a.7: Byte/character array interface metadata (2026-10-07)

Byte and character arrays now support interface Count, collection flags and
SyncRoot using their exact existing array identity. Selected Count/False/True/
Identity helpers never read or write elements. Indexed operations, copies,
enumeration, clearing and signed generic views remain diagnostic. Detached plans
check the same exact widths and operation boundary. Five probes provide 1,470
repeated O0/O2 native/.NET comparisons with forced GC and ASan/UBSan; all 113
focused checks pass. Integer CIL v69, object IR v44, interface plan v16 and native
v70 record the extension; core and budgets are unchanged. See
[ADR AC0153](decisions/AC0153-narrow-array-interface-metadata.md).
The frozen engine previously reached narrow array Count after 782,813 metadata
units. Full engine/fingerprint execution and saved-game replay remain open.


### AC8.46: Diagnostic metadata and dispatch-method capacity (2026-10-07)

After AC7a.7, the copied 786,432-unit metadata budget exhausts with 786,428 units
charged. Raising only copied metadata to 1,048,576 reaches the method limit after
817,418 units. A counter-only audit measures 4,246/4,096 combined method identities:
2,997 bodies, four abstract methods, 273 interface declarations, 1,017 mapping facts
and 38 exception declarations (categories overlap). Other counters remain within
their diagnostic limits: 1,207 type roots, 72,751 instructions, 5,870 parameters and
3,239 locals.

A copied 6,144-method allowance then reaches HashSet<string>(IEnumerable<string>,
StringComparer.Ordinal) in HandLimitHandler.Resolve after 820,241 metadata units
in 3,477 ms. StateLookup remains 15 methods/11,511 units. Production bounds and
60-second probe caps are unchanged; no native engine emission/execution occurred.
See [ADR AC0154](decisions/AC0154-dispatch-capacity-audit.md). Full engine/fingerprint
execution and saved-game replay remain open.


### AC3.27: Source collection plus proven ordinal HashSet construction (2026-10-07)

HashSet<string>(IEnumerable<string>, comparer) now admits a directly proven
StringComparer.Ordinal getter through its exact selected constructor/factory and
separate two-argument grant. Construction retains source-expression/getter order,
duplicate and null-key behavior, enumeration faults and comparer-sensitive copying.
Same-comparer source sets may preserve slots; different comparer identities rebuild
by enumeration. Seven probes supply 2,058 repeated O0/O2 native/.NET comparisons
with forced GC and ASan/UBSan; 68 distinct focused checks pass. Integer CIL v70
records admission; native v70/layout v26 and production budgets are unchanged.
See [ADR AC0155](decisions/AC0155-ordinal-set-source-construction.md).
The frozen engine previously reached this constructor in HandLimitHandler.Resolve
after 820,241 metadata units. Full engine/fingerprint execution and replay remain open.

### GUID static parsing (AC5a.5)

Guid.Parse(String) shares the owned managed D/N/B/P/X parser with Guid(String).
It preserves the static API's input parameter name and FormatException for
UInt32 overflow; the constructor retains g and OverflowException. Parsed words
are copied into the existing Guid representation only after success. No compiler
profile, intrinsic grant, native operation or resource bound changes.
Six static probes and the promoted Parse control provide 7,266 repeated O0/O2
native/.NET comparisons with ASan/UBSan, forced collection and exact cleanup.
Constructor and static tests share malformed-input, whitespace and mutation corpora.
All 24 focused GUID checks pass. See ADR AC0156. TryParse/ParseExact and span/byte
inputs remain unmapped. Full engine and saved-game replay remain unverified.

### Three-component tuples (AC5b.15)

The owned ValueTuple<T1,T2,T3> supplies three fields, construction and typed
equality through the established value/key model. Comparisons copy each component,
read in order and stop on the first mismatch. Numeric and reference-bearing tuple
copies, returns, heap/vector storage, boxing and nullable values use existing paths.
Ten probes add 2,940 repeated O0/O2 native/.NET comparisons with ASan/UBSan and
forced collection. All 44 tuple-family checks pass. Hashing, text, object/structural
equality, comparison and ITuple indexing remain explicit diagnostics. No compiler
profile, intrinsic grant or resource-bound change. See ADR AC0157. Full engine and
saved-game replay remain unverified.

### Two-argument HashCode.Combine (AC5a.6)

HashCode.Combine<T1,T2> uses existing closed key hashing and the fixed-seed PairHash
combiner. Arguments are by-value copies, hashed in order with zero for null. Six
probes provide 1,764 repeated native/.NET behavioral comparisons; 294 additional
comparisons validate numeric output against an independent pinned-algorithm model
with Cilica's seed. Cross-runtime numeric hash equality is not claimed.
All 29 distinct selected hash/key/tuple checks pass after repairing an older array-key
diagnostic: unsupported array hashing/equality now fails before layout validation.
Nullable/array components, other arities and the mutable builder remain unsupported.
Existing grants and compiler/native profiles are unchanged. See ADR AC0158.
Full engine, fingerprint execution and saved-game replay remain unverified.

### Further diagnostic metadata capacity (AC8.47)

After committed HashCode.Combine support, the frozen engine exhausts the copied
1,048,576-unit metadata budget. Raising only that diagnostic copy to 1,572,864
reaches a static-field access rejection at AlchemyResolutionAdapter.Prepare IL_02ed
(1,100,871 charged audit units; 3,695 ms). StateLookup remains 15 methods/11,517 units.
Production limits are unchanged, as are all other diagnostic bounds and the
60-second command cap. This is discovery evidence, not native engine execution.
See ADR AC0159. Full saved-game replay remains unfinished.

### Nested private static fields (AC8.48)

The integer-object profile admits private static reads/writes from nongeneric
nested declarations when the existing validated lexical chain proves directional
containment in the declaring type. Discovery and both IR stages share the same
rule already used for private method access. Sibling, enclosing, unrelated, foreign,
generic-owner and legacy-profile access remain diagnostic; readonly writes retain
the owning-initializer restriction. Static addresses remain unsupported.
Six probes add 1,764 repeated O0/O2 native/.NET comparisons with forced collection,
ASan/UBSan, retained-root accounting and initializer-fault checks. All 107 distinct
focused/adjacent checks pass across the recorded final runs. CIL v71, object IR v45
and native v71 record admission; layout v26, interface plan v16 and limits remain
unchanged. See ADR AC0160. Full engine and saved-game replay remain unverified.


### Sealed application-class shallow cloning (AC8.49)

Object.MemberwiseClone now has an exact owned-core intrinsic grant for calls from
sealed application classes in the integer-object profile. The compiler validates
the complete ordinary application base chain and emits typed field copies into a
fresh allocation. Inherited fields, embedded value leaves and references are copied;
constructors and runtime ownership, identity and collector headers are not copied.
The source remains rooted across allocation, and allocation failure uses the existing
managed fault path. Nonsealed classes, special runtime hierarchies, invalid caller
receivers and pre-base constructor use remain diagnostic.
Eight probes provide 2,352 repeated O0/O2 native/.NET comparisons with ASan/UBSan,
forced collection and exact cleanup. Quota/header checks cover object and byte limits,
managed allocation faults and recovery. All 42 focused clone, constructor, identity,
collection and boxed-value checks pass. CIL v72, object IR v46 and native v72 record
the operation; layout v26, interface plan v16 and resource limits are unchanged.
See ADR AC0161. Full engine, unchanged fingerprint execution and saved-game replay
remain unverified.


### Read-only collection enumeration (AC4.10)

ReadOnlyCollection<T> now forwards enumeration to its backing list using existing
managed cursor support. The generic factory reads Count first and returns the shared
per-type empty array cursor at zero; otherwise it forwards GetEnumerator. The
nongeneric factory directly forwards without reading Count. This preserves live
array reads, list version checks, Current/Reset/Dispose behavior, empty cursor identity
and custom-list callback/fault order from the pinned .NET implementation.
Nine probes provide 2,646 repeated O0/O2 native/.NET comparisons with ASan/UBSan,
forced collection and exact static-root cleanup. All seven focused checks pass.
No compiler profile, intrinsic grant, native layout or resource-bound changes.
CopyTo and the remaining nongeneric search/index surface remain diagnostic. See
ADR AC0162. Full engine, unchanged fingerprint execution and saved-game replay
remain unverified.


### Deferred LINQ casts (AC6.17)

Enumerable.Cast<TResult>(IEnumerable) returns an already compatible generic source
unchanged, checks null immediately and otherwise defers element casts. Separate
managed cursors preserve the general yield iterator's automatic fault cleanup and
retained Current versus the ICollection iterator's caller-driven cleanup and cleared
Current. Collection Count/Any, materialization, First and Contains use the pinned
shortcuts; existing query interfaces preserve Select callbacks and cursor identity.
Reference, scalar, nullable and reference-bearing value targets use existing casts,
boxes, roots and managed exceptions. No compiler profile, intrinsic grant, layout or
resource bound changes. Unsupported source/element operations remain diagnostic.
See ADR AC0163 for differential evidence, source provenance and exact boundaries.
Full engine, unchanged fingerprint execution and saved-game replay remain unverified.

AC6.17 validation: 40 distinct checks pass, including 5,382 repeated native/.NET
Cast comparisons and the public baseline; O0/O2, forced GC and ASan/UBSan remain active.


### Dictionary and view interface enumeration (AC4.11)

Dictionary<TKey,TValue>, KeyCollection and ValueCollection now return boxed existing
cursors for nonempty generic/nongeneric enumeration. Their nongeneric Current checks
state before returning fresh value boxes or the original reference. Existing slot,
version, copy, Reset and no-op Dispose behavior is retained. Empty key/value views
share the mutable array cursor; empty dictionary pairs use a distinct stateless
per-type singleton whose Current always reports enumeration not started.
Reference, nullable and reference-bearing value payloads use existing tracing and
boxing. Legacy IDictionary.GetEnumerator and IDictionaryEnumerator.Entry/Key/Value
remain explicit unsupported bodies. No compiler profile, intrinsic grant, native
layout or resource-bound changes. See ADR AC0164 for tests and review.
Full engine, unchanged fingerprint execution and saved-game replay remain unverified.

AC4.11 validation: 33 distinct checks pass, including 4,212 repeated native/.NET
comparisons and the public baseline, with forced collection and ASan/UBSan.


### Measured diagnostic local capacity (AC8.50)

The frozen engine exhausts the diagnostic copy's aggregate local bound at 4,115/4,096
slots, with 7,549/8,192 parameters. A fresh disposable copy raises only locals to
6,144. Other copied limits remain types 2,048, methods 6,144, metadata 1,572,864,
instructions 131,072 and parameters 8,192; command timeout stays 60 seconds.
It reaches Boolean.TryParse(String, Boolean&) in ContributeShieldCardPrimitive's
bond-policy parser after 1,127,785 charged metadata units in 3,843 ms. StateLookup
remains 15 methods/11,527 units. Production limits/profiles are unchanged; no engine
C++ is emitted or executed. See ADR AC0165 and artifacts/alchemy-locals-6144.
Full engine, unchanged fingerprint execution and saved-game replay remain unverified.

### Width-preserving frame borrows (AC8.51)

Byte, short, Char and Boolean locals/parameters now support synchronous borrows
with explicit storage widths, preserved through both IR stages and checked native
carriers. Boolean retains its low byte. Mismatched widths and narrow field/array
addresses remain diagnostic. Fifteen probes supply 2,250 repeated O0/O2 native/.NET
comparisons with forced GC and sanitizers; see ADR AC0166. Integer CIL v73, object
IR v47 and native v73 record the extension. Limits are unchanged. The measured
Boolean.TryParse API and full saved-game replay remain unfinished.

### Boolean string parsing (AC5a.7)

The owned Boolean.TryParse(String, out Boolean) now preserves case-insensitive
true/false, surrounding Unicode whitespace/NUL, and failed-output reset through
local/parameter borrows. Four probes supply 3,660 repeated native comparisons;
852,096 additional managed character cases and all 29 selected checks pass.
Profiles, grants and limits are unchanged. The frozen engine now reaches generic
ReadOnlyCollection<T>.CopyTo after 1,124,897 charged diagnostic units; StateLookup
remains 15 methods/11,527 units. Full engine/fingerprint/replay execution remains
unfinished. See ADR AC0167 and artifacts/alchemy-boolean-tryparse-engine.

### Generic read-only collection copying (AC3.28)

ReadOnlyCollection<T>.CopyTo now forwards to the backing list without prevalidation
or extra callbacks. Eight probes provide 2,352 repeated native/.NET comparisons,
including live views, values/references, faults, partial copies and List snapshots.
Profiles and limits stay unchanged; nongeneric copying remains unsupported. Frozen
engine discovery next reaches Dictionary.KeyCollection.CopyTo at 1,147,132 charged
units. StateLookup remains 15 methods/11,527 units. Full replay remains unfinished.
See ADR AC0168 and artifacts/alchemy-readonly-copy-engine.

### Generic dictionary copies (AC3.29)

Dictionary keys, values and generic key/value pairs now copy with pinned validation,
active-slot order, reference identity and partial covariant-store behavior. Thirteen
probes plus capacity cases supply 4,146 repeated native/.NET comparisons. Owned
helpers retain the compiler's generic access boundaries; profiles and limits stay
unchanged. Frozen discovery next reaches HashSet.CopyTo through List.AddRange in
EffectTags.Combine (1,149,539 charged units); StateLookup remains 15 methods at
11,529 units. Full replay is unfinished. See ADR AC0169.

### Generic HashSet copies (AC3.30)

All three HashSet.CopyTo overloads now preserve validation order, negative actual
values, active-slot order, requested-count behavior and partial covariant writes.
Fifteen C#/raw-IL probes plus capacity cases provide 4,842 repeated native comparisons;
all 35 selected checks pass. No profiles, grants or production/native limits change.
Frozen engine discovery next exhausts the diagnostic parameter budget at 8,193/8,192
(4,504/6,144 locals; 1,159,312 charged units). StateLookup remains 15 methods/11,529
units. Full replay remains unfinished. See ADR AC0170.

### Measured diagnostic parameter capacity (AC8.52)

After observing 8,193/8,192 parameters, a fresh diagnostic copy raises only that
budget to 12,288. Other copied capacities, production/native limits and 60-second
commands stay unchanged. Frozen discovery reaches OrdinalCaseSensitiveComparer
hashing through TypeKeyOperations.Hash (1,159,620 charged units). Its exact IL calls
Object.GetHashCode on a loaded Type&; investigate over-broad virtual reachability
before treating this as a required hash API. StateLookup remains 15 methods/11,529
units; full replay is unfinished. See ADR AC0171.

### Known class receiver discovery (AC8.53)

Public zero-argument virtual discovery now uses a proven ordinary static class,
including abstract/closed generic bases and a reference loaded from a validated
managed location. Canonical slot selection, subclass overrides, null checks,
visibility and bounded same-block proof remain intact. This removes unrelated
comparer reachability from Type-key hashing. Seven new native probes provide
2,058 repeated O0/O2 comparisons with forced GC and sanitizers; all 47 selected
checks pass. Integer CIL v74 records the change; IR v47/native v73 and limits stay
unchanged. See ADR AC0172. Frozen discovery next reaches the actual ordinal string
hash in UnitId.GetHashCode at 1,182,823 metadata units; full replay is unfinished.

### Ordinal string hashing (AC7b.19)

StringComparer.Ordinal.GetHashCode(string) now checks null with parameter obj and
uses the existing managed UTF-16 key hash. Direct/generic comparer calls preserve
equal-string consistency and fault ordering. Numeric hashes are implementation-
specific and intentionally differ from randomized .NET values. Eight probes supply
5,454 repeated native comparisons; all 30 selected checks pass. Profiles, grants
and limits are unchanged. See ADR AC0173. Frozen discovery next reaches excessive
parameterless-comparer hash reachability from ReferenceKeyOperations<T>.Hash;
full engine/fingerprint/replay execution remains unfinished.

### Receiver-bounded virtual dispatch (AC8.54)

Class dispatch maps retain proven receiver classes from bounded stack discovery
or validated constrained reference calls. Bounds union across call sites; unproven
calls retain owner-wide discovery. Typed call validation and complete subclass
coverage prevent omitted receivers, including inherited Object implementations.
Six probes add 1,764 repeated native comparisons, with eight detached-IR corruption
cases and 88 passing selected checks. CIL v75/object IR v48/native v74 record the
contract; layout/interface/grants/limits are unchanged. See ADR AC0174. Frozen
discovery next reaches SelectMany(source, selector) in ConstructionInvitations at
955,482 metadata units; full engine/fingerprint/replay execution remains unfinished.

### Single-selector SelectMany (AC6.18)

The nonindexed two-generic SelectMany overload now provides deferred nested
iteration plus the pinned independent terminal paths. Managed segments preserve
inner collection Count/CopyTo choices, and cursor faults retain exact disposal
ordering. Fifteen probes provide 6,078 repeated native comparisons; all 45 selected
checks pass. Other overloads/nested-vector source types remain diagnostic. See ADR
AC0175. Profiles, grants and limits are unchanged. Frozen discovery next reaches
Single(source) in construction-contribution handling at 958,875 metadata units;
full engine/fingerprint/replay execution remains unfinished.

### Source-only Single terminals (AC6.19)

Single(source) and SingleOrDefault(source) now preserve the pinned IList Count/index
fast path and two-MoveNext enumerable path, including disposal and exact fault text.
Nine probes supply 2,424 repeated native comparisons; all 37 selected checks pass.
Explicit-default overloads remain unmapped. See ADR AC0176. Profiles, grants and
limits are unchanged. Frozen discovery next reaches indexed Select in
ContinueShieldContribution at 972,267 metadata units; full engine/fingerprint/replay
execution remains unfinished.

### Indexed Select (AC6.20)

The indexed Select overload now follows the pinned yield lifecycle with checked
indices, deferred source access, fault cleanup and retained Current. It uses no
optimized terminal shortcuts. Eleven probes supply 3,630 repeated native
comparisons plus two live-root checks; all 63 selected checks pass. See ADR AC0177.
Profiles, grants and limits are unchanged. Frozen discovery next reaches List<T>.
Reverse() in ContinueShieldWave at 964,532 metadata units. Full engine/fingerprint/
replay execution remains unfinished.

### List reversal (AC3.31)

List<T>.Reverse() and Reverse(index,count) now reverse the validated range in place,
including exact argument order/text and enumerator invalidation for empty/singleton
ranges. Nine probes supply 2,952 repeated native comparisons; all 34 selected checks
pass. See ADR AC0178. Profiles, grants and limits are unchanged. Frozen discovery
next reaches String.Split(char,StringSplitOptions) in DamageReplacementCheckpoint.
RecordAnswers at 989,604 metadata units. Full engine/fingerprint/replay execution
remains unfinished.

### Character string splitting (AC7b.20)

String.Split(Char,StringSplitOptions) now supports UTF-16 separators, empty-entry
removal and Unicode trimming through managed scans and existing substring copies.
Invalid flags and empty/input identities match the pinned runtime. Six probes
supply 3,696 repeated native comparisons; all 29 selected checks pass. See ADR AC0179.
Other split overloads remain diagnostic. Profiles, grants and limits are unchanged.
Frozen discovery next reaches invariant Int64.TryParse with NumberStyles.None in
RecordAnswers at 990,636 metadata units. Full engine/fingerprint/replay execution
remains unfinished.

### Invariant Int64 parsing (AC5a.8)

The exact Int64.TryParse(String,NumberStyles,IFormatProvider,out Int64) declaration
now accepts directly proven None and InvariantCulture. A managed parser checks
unsigned accumulation before overflow and preserves all output bits. Four probes
supply 3,660 repeated native comparisons; 131,648 additional grammar cases are
managed-only. All 72 distinct selected checks pass. See ADR AC0180. CIL v76/object
IR v49 record the wider contract; native v74 and all limits remain unchanged.
Frozen discovery next stops at nonpublic GameOccurrence.Permits dispatch in
ShieldReactionDiscovery.Discover (998,224 metadata units). Full engine/fingerprint/
replay execution remains unfinished.

### Same-assembly internal class dispatch (AC8.55)

Closed class dispatch now admits internal virtual/abstract slots and C#'s
CheckAccessOnOverride flag under exact same-assembly receiver/override and caller
access checks. Other access modes and legacy/interface profiles remain restricted.
Eight probes add 3,024 repeated native comparisons; all 123 selected checks pass.
See ADR AC0181. Closed class target v4, CIL v77 and object IR v50 record the change;
native v74, grants and limits are unchanged. Frozen discovery now reaches the
separate inline-value abstract-signature restriction at GameOccurrence.Permits
(998,255 metadata units). Full engine/fingerprint/replay execution is unfinished.

### Copied value arguments on abstract class calls (AC8.56)

Abstract declarations now accept copied inline-value parameters and expand their
nested scalar/reference leaves with the same layouts as concrete implementations.
Empty values remove no calls or null checks. Value returns and borrowed abstract
parameters remain diagnostic. Six probes supply 1,764 repeated native/.NET
comparisons; all 65 distinct selected checks pass. See ADR AC0182. CIL v78/object
IR v51 record the contract; native v74, grants and limits are unchanged. Frozen
engine discovery advances past Permits(PlayerId) to a custom message/inner-exception
constructor in SetRangeModifier.DistanceFromFeet (1,019,328 metadata units).
Full engine, unchanged fingerprint and saved-game replay execution remain unfinished.

### Custom message/inner exception forwarding (AC8.57)

Exception(String,Exception) and fieldless custom forwarding chains now preserve
exact message/inner identity through existing traced payloads. Discovery checks
every argument and direct-base constructor; both IR stages and native allocation
share allowed wrapper identities. Side effects and unsupported framework families
remain diagnostic. Eight probes supply 2,352 repeated native/.NET comparisons,
with two additional GC/allocation-failure checks; all 90 selected checks pass.
See ADR AC0183. Integer CIL v79, exception IR v9 and native v75 record the change.
Frozen discovery advances to missing EqualityComparer<T> mapping in target
selection (1,039,295 metadata units). Full engine/fingerprint/replay remains unfinished.

### Default comparers for exact equatable keys (AC3.32)

EqualityComparer<T>.Default retains one comparer per closed T and runtime for four
integer types and exact IEquatable<T> keys. Existing key operations preserve copied
structs, null handling and typed callbacks. Eleven probes supply 3,234 repeated
native/.NET comparisons plus two independent-runtime ownership checks; all 80
selected checks pass. See ADR AC0184. Integer CIL v80 adds a private factory grant;
native v75, IR and limits are unchanged. Specialized string/enum/nullable/object
fallbacks, nongeneric operations and comparer-object methods remain diagnostic.
Frozen discovery advances to the existing field-metadata limit (1,038,837 work
units). Full engine, unchanged fingerprint and saved-game replay remain unfinished.

### Diagnostic field-budget audit (AC8.58)

The unchanged frozen engine exhausts 4,096 cached field declarations at 1,914 type
snapshots; the next type needs eight more fields. Explicit roots then contain 184
values, 1,304 references and 157 vectors. These are partial discovery counts, not
emitted layouts. Raising only the isolated diagnostic copy to 6,144 fields reaches
String.IndexOf(Char) in TargetOptions.ParseCell (1,041,357 metadata units/4,429 ms).
Production limits, profiles and semantics remain unchanged. See ADR AC0185.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.

### Character string searching (AC7b.21)

String.IndexOf(Char) and its start/count overloads now use a managed UTF-16 scan,
with exact ordered range validation and errors. Seven probes supply 5,640 repeated
native/.NET comparisons; all 69 selected checks pass. See ADR AC0186. Profiles,
grants and production limits are unchanged. Frozen discovery with the measured
diagnostic field allowance advances to Int32.Parse(String,NumberStyles,IFormatProvider)
in TargetOptions.ParseCell (1,051,158 metadata units). Full engine, unchanged
fingerprint and saved-game replay execution remain unfinished.

### Character text for compatibility batch 003

Exact Char.ToString instance calls use existing UInt16-tagged frame borrows; the
static overload constructs a fresh one-code-unit string through existing managed
character-array construction. All 19 focused and 52 adjacent checks pass, including
every UTF-16 code unit, identity, roots and malformed receiver facts. Integer CIL
v81 and object IR v52 record admission. No new native operations, grants or limits;
boxed/provider calls and narrow field/array receivers remain diagnostic. See
[character-text decision](decisions/compatibility-003-character-text.md) and the
[batch manifest](compatibility-candidates/batch-003.json). Full DiffPlex execution
remains unverified with Boolean storage and ignore-case comparer gates.

### Throwing invariant Int32 parsing (AC5a.9)

The exact three-argument Int32.Parse now admits directly proven Integer and
InvariantCulture. Shared managed status parsing distinguishes malformed text from
overflow, preserving invalid-suffix precedence and existing TryParse outputs.
Four probes supply 2,202 repeated native/.NET comparisons; all 73 selected checks
pass. Another 262,752 Parse/TryParse oracle cases are managed-only. See ADR AC0187.
Integer CIL v82 and object IR v53 add the exact throwing-mode declaration contract;
native v75 and production limits remain unchanged. Frozen engine discovery now
reaches the diagnostic instruction limit (1,049,093 metadata units). Full engine,
unchanged fingerprint and saved-game replay execution remain unfinished.

### Boolean vectors for compatibility batch 004

Boolean arrays now have a distinct nominal identity and reuse existing narrow
UInt32-carried storage, preserving raw low bytes rather than canonicalizing them.
Allocation, generic element copies, Empty, heap fields, constant initialization and
exact metadata helpers are covered by 21 focused and 62 adjacent checks. Integer
CIL v83, object IR v54, layout v27 and native v76 record admission; quotas remain
unchanged. Element borrows/interface element operations remain diagnostic. See the
[Boolean-vector decision](decisions/compatibility-004-boolean-vectors.md) and
[manifest](compatibility-candidates/batch-004.json). Full DiffPlex execution remains
unverified pending ignore-case comparer admission and semantic integration testing.

### Diagnostic instruction-budget audit (AC8.59)

The frozen engine consumes 131,071 of the diagnostic 131,072 instructions; the next
method needs more than the one remaining. Partial discovery has 5,147 declarations,
5,096 bodies, 324,841 IL bytes, 10,035 parameters and 5,994 locals. Raising only the
archived diagnostic compiler to 196,608 instructions reaches String.ToLowerInvariant
in ContinueConstructionContribution (1,052,133 metadata units/4,239 ms). Production
limits remain unchanged. This audit uses AC5a.9's archived CIL v82/IR v53 sources,
separate from concurrent Boolean-vector work. See ADR AC0188. Full engine,
unchanged fingerprint and saved-game replay execution remain unfinished.

### Pinned invariant string lowercasing (AC7b.22)

String.ToLowerInvariant now uses pinned Unicode 16.0.0 simple lowercase mappings,
including the invariant U+0130 exception, supplementary pairs, lone surrogates and
pinned .NET string identity. A generated managed lookup uses no static arrays or
host globalization API. All 1,114,112 code-point inputs match the local .NET oracle;
18,156 repeated native comparisons cover exact changed scalars/neighbors, complete
Unicode string-block hashes, identity, nulls and mixed text. All 98 selected
checks pass. See ADR AC0189. No compiler profile, grant or production limit changes.
Frozen discovery advances to 6,145/6,144 diagnostic locals in ContinueShieldContribution;
full engine, unchanged fingerprint and saved-game replay remain unfinished.

### Diagnostic local-budget audit (AC8.60)

After AC7b.22, the frozen engine needs 6,145 local slots against the diagnostic
6,144 allowance (10,345/12,288 parameters). Raising only the archived diagnostic
copy to 8,192 locals reaches Enumerable.Skip in ContinueAcquiredShieldChoice,
with 1,075,533 metadata units/4,336 ms. Production limits remain unchanged; no
engine code is emitted or executed. See ADR AC0190. Full engine, unchanged
fingerprint and saved-game replay remain unfinished.


### Managed dictionary comparers (compatibility batch 005)

Both comparer constructors now retain and invoke supplied admitted comparers through
ordinary managed dispatch. Aliases, runtime selection, null defaults and explicit
value-default normalization preserve hashing, equality and mutable-key copies.
The default comparer cache has private identity metadata; no compiler/native
profile or limit changes. The two obsolete dictionary ordinal-factory grants are
removed. See [decision](decisions/compatibility-005-dictionary-comparers.md) and
[results](compatibility-candidates/batch-005-results.md). OrdinalIgnoreCase/culture
factories remain explicit diagnostics; full DiffPlex execution is unverified.


### Unicode ordinal-ignore-case comparer (compatibility batch 006)

StringComparer.OrdinalIgnoreCase now uses pinned Unicode16 simple uppercase with
U+0131/U+017F excluded, valid surrogate-pair ordering and lone-surrogate preservation.
Typed comparison/equality and consistent deterministic hashing use ordinary managed
code; no new compiler grant, runtime operation or limit is added. See
[decision](decisions/compatibility-006-ordinal-ignore-case.md) and
[results](compatibility-candidates/batch-006-results.md). DiffPlex's selected source
requirements are mapped; real integration remains unexecuted pending approved
isolation. Culture factories and nongeneric comparer operations remain diagnostic.

### Skip over ordinary enumerable and list sources (AC6.21)

Enumerable.Skip now supports ordinary IEnumerable and IList sources, including
arrays, deferred acquisition/disposal, repeated/overflowed skips, mutable lists,
count/first/materialization and the exposed read-only list view. Copied equality,
ToList capacity and source/destination copy validation match the pinned .NET paths.
Seventeen probes supply 7,344 repeated native comparisons; all 124 selected checks
pass. Optimized LINQ-source specializations remain explicit build diagnostics,
including Where, Select and ordering. See ADR AC0191. No compiler/native profile,
intrinsic grant or production-limit change is needed. Frozen discovery remains
partial; full engine, unchanged fingerprint and saved-game replay are unfinished.

### UInt32 vectors for compatibility batch 007

UInt32 arrays now retain their own descriptors while using the existing 32-bit
carrier and compatible Int32/enum array views. Exact requested generic helper
identity preserves boxing/enumeration; nongeneric enumeration retains the actual
array element. Typed/unsigned loads, static initialization, copy, search, faults,
heap roots and malformed packets have independent differential regressions.
CIL85, IR55, layout28, interface17 and native77; limits and grants are unchanged.
See [decision](decisions/compatibility-007-uint32-vectors.md) and
[results](compatibility-candidates/batch-007-results.md). Scalar element addresses
and checked native-integer indices remain diagnosed. The latter is Crc32.NET's
next mapped gate; full candidate execution has not occurred.

### Diagnostic type-snapshot audit (AC8.61)

AC6.21 fills the 2,048 cached type snapshots in the diagnostic copy. Raising only
that archived copy to 3,072 reaches Guid.TryParse(String, out Guid) in
TargetOptions.ParsePlayer at IL_0003. Discovery charges 1,117,323 metadata units
in 4,288 ms; StateLookup remains 15 methods/11,613 units. Production's 256-type
limit stays unchanged. See ADR AC0192. No engine emission or execution occurs;
full engine, unchanged fingerprint and saved-game replay remain unfinished.

### Nonthrowing string GUID parsing (AC5a.10)

Guid.TryParse(String, out Guid) now shares D/N/B/P/X validation with the constructor
and Parse, returns false and clears the output for null/invalid/overflowing input,
and creates no parsing exception on those paths. The throwing APIs retain their
exact fault policies. Six new probes supply 6,972 repeated native/.NET comparisons;
all 32 selected GUID checks pass with O0/O2, forced GC and sanitizers. See ADR AC0193.
The frozen engine advances to invariant Int64.Parse(String,IFormatProvider) in
ShieldAcquisitionReceipt.Read. Full engine, fingerprint and replay remain unfinished.

### Checked native array indices for compatibility batch 008

The integer object profile admits conv.ovf.i/conv.ovf.i.un only for block-local
array element indices. Overflow uses emitted target pointer width; invalid wide
indices are encoded without truncation or early bounds faults. Opaque frontend
tracking rejects other native-integer uses and block transfers. CIL 86/IR 56/native 78;
array layout, grants and quotas are unchanged. Independent 64-bit differential and
labelled 32-bit width-emulation tests cover evaluation/fault order and GC. Actual
32-bit ABI and full CRC project execution remain unverified. See
[decision](decisions/compatibility-008-checked-native-indices.md) and
[results](compatibility-candidates/batch-008-results.md).

### Throwing invariant Int64 parsing (AC5a.11)

Int64.Parse(String,IFormatProvider) now supports a directly proven InvariantCulture
provider and implicit Integer style. Its full signed range, ASCII whitespace,
trailing NULs and exact format/overflow precedence match .NET. Four probes supply
2,202 repeated native comparisons; all 40 selected integer-parser checks pass,
including 262,754 managed-only oracle cases. Integration with the separately
committed checked-index slice passes 69 distinct selected checks. See ADR AC0194. The frozen engine next
reaches constrained ElementGender.ToString; full engine, fingerprint and saved-game
replay remain unfinished.

### Constrained default enum text (AC7b.23)

Exact constrained Object.ToString calls on admitted Int32 enums now read the scalar
receiver and use managed formatting. Names are cached per closed enum type;
unnamed values reuse the existing invariant integer cache. Private metadata helpers
stay private. Nine probes supply 3,030 repeated native/.NET comparisons, plus 129
allocation-limit recovery cases at each optimization level. All 77 selected checks
pass. See ADR AC0195. Flags, aliases, wide/boxed receivers and direct enum-array
addresses remain diagnostic. Frozen discovery reaches 12,290/12,288 diagnostic
parameters; full engine, unchanged fingerprint and saved-game replay are unfinished.

### Diagnostic parameter and method allowances (AC8.62)

After AC7b.23 reaches 12,290/12,288 parameters, increasing only the copied parameter
allowance to 16,384 reaches exactly 6,144 method declarations. Increasing only that
copied method allowance to 8,192 then reaches HashSet<T>.UnionWith through
ResolveChain. Production remains 4,096 parameters/512 methods. See ADR AC0196.
No engine code is emitted or executed; full engine, fingerprint and replay remain
unfinished.

### Enumerable set union (AC3.33)

HashSet<T>.UnionWith now performs the pinned null check and one-pass insertion.
It preserves source callbacks/disposal, partial mutation, self-union and stored
identity without Count/CopyTo shortcuts. All 67 selected set checks pass; twelve
new probes provide 5,184 repeated native/.NET comparisons. See ADR AC0197. Frozen
discovery next reaches protected abstract GameOccurrence.WriteContext dispatch.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.

### Protected class slots (AC8.63)

Closed class dispatch admits exact protected slots with same-assembly receivers
and overrides. Caller access remains independently checked: calls from the owning
class work; this does not grant inherited or foreign protected access. All 176
selected checks pass, including 3,024 new repeated native/.NET comparisons. See
ADR AC0198. Frozen discovery passes WriteContext and reaches the default string
comparer branch behind an explicit ordinal ThenBy. Full engine, unchanged
fingerprint and saved-game replay remain unfinished.

### Explicit ordinal ordering (AC6.22)

All four OrderBy/ThenBy comparer overloads admit directly proven StringComparer.Ordinal
string keys through checked managed bridges. Sorting, selector order, stable ties and
terminal behavior reuse the owned implementation; cloning reuses its resolved comparer.
Ordinal ThenBy requires exact owned source element types in the closed receiver universe;
custom/covariant sources and unproven comparers remain diagnostic. Ninety-one distinct
selected checks pass across the integrated run and targeted fixture repairs, including
4,410 new repeated native/.NET comparisons. See ADR AC0199. Frozen discovery advances
to missing IGrouping<TKey,TElement> in forced movement. Full engine, unchanged fingerprint
and saved-game replay remain unfinished.

### Bounded Int32 array element locations (compatibility batch 006)

Int32 vector receivers and synchronous reborrows now retain their array owner and
index through argument evaluation, preserving later mutation, value copies and GC
roots. Exact Int32 views also preserve existing UInt32/Int32-enum array aliases;
other declared scalar views and escaping/native borrows remain diagnostic. Both
IR stages and native carriers validate the new path. All 31 new checks pass,
including 2,352 repeated O0/O2 native/.NET comparisons. CIL92/IR59/native79; see
[the decision](decisions/compatibility-011-int32-array-locations.md).
The unchanged selected DiffPlex character/line roots now pass 64 scenarios and
11,232 repeated native/.NET transcript comparisons at O0/O2; see the
[trial report](compatibility-candidates/diffplex-int32-20261008.md). Broader candidate
surfaces remain untested.

### Default grouping (AC6.23)

The two-argument GroupBy and IGrouping now preserve first-key group order, element
order, deferred buffering, callback failures, read-only group lists and cursor state.
Managed lookup storage uses the existing admitted default key operations. Sixty-two
selected checks pass, including 6,342 new repeated native/.NET comparisons and exact
retained-graph collection checks. See ADR AC0200. Frozen engine discovery advances
to missing Stack<T> in forced movement. Comparer/selector overloads, ToLookup and
optimized query Skip remain unimplemented. Full engine execution, unchanged
fingerprinting and saved-game replay remain unfinished.

### Bounded stack storage (AC3.34)

Stack<T> now supports parameterless construction, Count, Push and Pop with pinned
growth and empty-stack exceptions. Popped reference-bearing values release their
stored references; failed growth leaves the stack unchanged and recoverable.
Twenty-three selected checks pass, including 2,352 new native/.NET comparisons.
See ADR AC0201. Frozen discovery advances to CollectionExtensions.GetValueOrDefault
in forced movement. Stack enumeration, other constructors and remaining members stay
diagnostic. Full engine, unchanged fingerprint and saved-game replay remain unfinished.

### Dictionary fallback lookup (AC3.35)

Both IReadOnlyDictionary GetValueOrDefault overloads now use the owned null check
and one ordinary TryGetValue call, preserving custom dictionary callbacks and fallback
values. Eighteen checks pass with 2,352 native/.NET comparisons. See ADR AC0202.
Frozen engine discovery advances to Nullable<T>.ToString in composite formatting;
other CollectionExtensions members remain diagnostic. Full engine, unchanged
fingerprint and saved-game replay remain unfinished.

### Named nullable text (AC1a.2)

Nullable<T>.ToString now returns empty text when absent and invokes an admitted named
payload override when present. Payload mutation, null returns, exceptions and receiver
storage follow pinned .NET behavior. Twenty-nine selected nullable checks pass, with
2,058 new native/.NET comparisons. See ADR AC0203. Primitive constrained formatting,
narrow field addresses and nullable composite boxes retain explicit diagnostics.
Frozen discovery advances to Enumerable.Range; the independently observed nullable-box
formatting gap also remains. Full engine, unchanged fingerprint and replay are unfinished.

### Nullable boxed formatting (AC7b.24)

String.Format now recognizes directly proven nullable boxes of admitted named values
and Int32 enums. Discovery selects the actual payload type; block-local proof comes
only from the exact nullable box operation. The shared formatter treats null arguments
as empty text before payload dispatch. Sixty-four checks pass with 2,646 new repeated
native/.NET comparisons. See ADR AC0204. CIL93; IR59/native79 unchanged. Aliases, joins,
primitive nullable formats and IFormattable payloads stay diagnostic. Frozen discovery
still stops at Enumerable.Range. Full engine, unchanged fingerprint and replay are unfinished.

### Int32 ranges (AC6.24)

Enumerable.Range now validates bounds and supplies a reusable Int32 iterator with
read-only list views and the admitted query operations. End-marker wraparound,
retained cursor values, copy exceptions and independent Select traversal match the
pinned runtime. All 24 focused Range checks pass with 3,528 repeated native/.NET
comparisons; 23 adjacent controls also passed. See ADR AC0205. Frozen engine discovery
advances to the isolated scan's 8,192-local allowance (8,194 observed). Production
limits and CIL93/IR59/native79 are unchanged. Full engine execution, unchanged
fingerprinting and saved-game replay remain unfinished.

### Diagnostic local-storage allowance (AC8.64)

After Range exposed 8,194 aggregate locals, the isolated engine-discovery probe now
allows 12,288 locals. The production 4,096-local limit and CIL93/IR59/native79 are
unchanged. Discovery advances to Enum.GetValues(Type) in SpellModeChoices.BondsAfter;
StateLookup still translates its 15-method closure. See ADR AC0206. This is diagnostic
coverage, not full engine compilation, native execution or replay evidence.

### Named enum value arrays (AC5b.16)

Enum.GetValues(typeof(Int32-enum)) now constructs fresh nominal arrays in unsigned
constant order, retaining aliases and empty-array identity. One uninterrupted typeof
sequence and exact grants for both calls are required; dynamic Type values, other
carriers and more than 256 entries remain diagnostic. Sixty-eight distinct selected
checks pass, including 3,234 new native/.NET comparisons. See ADR AC0207. CIL94;
IR59/native79 unchanged. Frozen discovery advances to parameterless List<Int64>.Sort
in GameStateHasher.AppendSpell. Full engine, unchanged fingerprint and replay are unfinished.

### Default signed integer list sorting (AC3.36)

Parameterless List<Int32>.Sort and List<Int64>.Sort reuse the managed introsort and
existing granted numeric CompareTo bridge. Successful sorts invalidate cursors even
for zero/one items; no comparer cache is created. Thirty-seven selected checks pass,
including 1,764 new native/.NET comparisons. See ADR AC0208. No new compiler/runtime
operation, profile or grant is introduced (validated snapshot CIL94/IR59/native79).
Unsigned delegate/vector composition and other default comparer types remain diagnostic.
Frozen engine discovery advances to an access check in HashSet<internal WaveGroupId>
hashing. Full engine, unchanged fingerprint and saved-game replay remain unfinished.

### Bounded vector section clearing (compatibility batch010)

Array.Clear(Array,Int32,Int32) now selects a granted ordinary managed helper for
same-block proven vectors. Complete null/range validation precedes typed default
stores; covariance, argument evaluation and reference release are preserved.
Int32/UInt32/Int64/enums, references and supported inline values have O0/O2 .NET and
GC evidence. Narrow scalar generic locations, general Array aliases and joined
argument stacks remain diagnostic. CIL95; IR59/native79, layouts, quotas and
collector unchanged. See [decision](decisions/compatibility-012-array-section-clear.md)
and compatibility batch010. CircularBuffer still needs public Copy/ArraySegment;
its upstream code and full Alchemy replay have not been executed by this slice.

### Hidden value-key callbacks (AC8.65)

Core collections can invoke the exact GetHashCode override and IEquatable<T>.Equals
implementation of admitted internal/private value keys, including explicit interface
implementations. Metadata-selected callback contracts survive scalar lowering and
are rechecked at both IR stages; ordinary private calls stay restricted. Existing
borrow/copy semantics preserve mutating keys, allocating callbacks and exception order.
Sixty-five distinct selected checks pass, with 3,234 new native/.NET comparisons.
See ADR AC0209. CIL97/IR60 adds checked call evidence; native79 and production limits
are unchanged. Private nested generic construction and default value-key text retain
independent diagnostics. Frozen discovery passes WaveGroupId hashing and next exhausts
the existing diagnostic instruction allowance. Full engine execution, unchanged
fingerprints and saved-game replay remain unfinished.

### Bounded public vector copying (compatibility batch011)

Array.Copy now supports the three/five-argument Int32 overloads for same-declared-
element vectors proven in one block. Managed validation preserves null/range/type
order; directional copying preserves overlap and existing checked stores preserve
covariant partial faults and reference lifetimes. Scalar, reference, inline-value,
constructor-field and mutation probes run against .NET at O0/O2 with sanitizers.
General Array aliases, unsupported producer shapes, cross-element widening/boxing
and Int64 overloads remain diagnostic. CIL98; IR60/native79, quotas and runtime
unchanged. See [decision](decisions/compatibility-013-array-section-copy.md).
CircularBuffer itself is unexecuted; ArraySegment and full replay remain open.

### Initial interface-planning audit allowances (AC8.66)

After AC8.65, the isolated frozen-engine scan reaches 196,602 decoded instructions;
the next method consumes its remaining six-instruction quota with 29 IL bytes left.
A diagnostic-only 262,144-instruction allowance lets the initial work queue drain at
7,621 bodies. Interface planning then exhausts 1,572,864 metadata work units. Raising
only that isolated budget to 2,097,152 reveals the next semantic gap: nongeneric
narrow-array enumeration reached through OfTypeIterator.Count. This is partial
closure discovery, without full IR admission, native emission or replay. Production
limits remain 65,536 instructions/metadata units; StateLookup still translates 15
methods. See ADR AC0210 for the measured progression and unchanged input identities.

### Narrow nongeneric array enumeration (AC7a.8)

Boolean, Byte and Char arrays now use the existing nongeneric array cursor and exact
primitive boxes. Shared discovery/detached-plan rules preserve the actual element
identity; generic enumeration, other narrow element operations, signed/UInt16 array
construction and OfType<byte>'s narrow-memory requirement remain diagnostic. All 36
selected checks pass on the combined Array.Copy snapshot, including 2,352 new repeated
native/.NET comparisons at O0/O2 with sanitizers and forced GC. CIL99/interface-plan18;
object IR60/native79 and all production quotas remain unchanged. See ADR AC0211.

The follow-up AC8.67 audit increases only copied diagnostic metadata work to 4,194,304
after measured 2,097,152 exhaustion. Frozen discovery then identifies boxed List<T>
enumerator covariance at IEnumerator<T>.Current as the next gap. This is partial
interface planning, not full engine execution, fingerprint parity or saved-game replay.

### Boxed single-candidate interface variance (AC8.68)

Named boxed values now share the existing exact-first, single-candidate variance
selection with class receivers. Discovery selects the declared contract; both IR
stages check its original member identity and compatible reference/scalar signature.
Boxed payloads remain borrowed in place, preserving mutation and allocation roots.
All 55 distinct selected checks pass, including 2,940 new native/.NET comparisons.
CIL100/interface-plan19; object IR60/native79, core bodies and production quotas are
unchanged. Ambiguous/generic variance and missing return-interface catalog discovery
remain diagnostic. Frozen discovery finishes its initial interface-planning pass,
then reaches 10,797 method declarations/mappings against the 8,192 diagnostic quota.
See ADR AC0212. Full engine execution, fingerprint parity and replay remain unfinished.


### Bounded ArraySegment views

ArraySegment<T> constructors and Array/Offset/Count now use ordinary rooted managed
value fields. Null/negative/range ordering, default values, shared backing identity
and copied segments preserve pinned behavior. Required interface metadata remains
exact; existing boxed Count is tested, while other interface/indexer/equality/copy/
enumerator bodies remain unsupported. Empty segments retain the complete backing
array. No compiler/runtime profile, grant or quota changes: CIL99/IR60/native79.
Batch012 passes 48 focused checks, 2,940 new repeated native/.NET comparisons and 4 GC
lifetime variants. The actual CircularBuffer trial remains blocked at same-owner
constructor chaining before oracle/native execution; full replay is unverified.

### Interface-body discovery audit allowances (AC8.69)

After AC8.68, raise only the isolated diagnostic method allowance from 8,192 to
12,288 after measuring 10,797 distinct declarations/mappings. Loading the selected
interface bodies then reaches 16,385/16,384 aggregate parameter slots; a diagnostic
32,768-parameter allowance advances to unmapped System.Decimal in frozen spell-route
selection. Frozen IL requires Int32 conversion/construction, decimal multiplication,
division and invariant-provider text. Production limits remain unchanged. This is
partial closure discovery, without full engine emission/execution, fingerprint parity
or saved-game replay. See ADR AC0213.


### Same-owner constructor chains

The integer-object profile admits acyclic calls to another constructor on the exact
same closed class, retaining direct receiver, access, single-prefix initialization
and uninitialized-object safety checks. Both IR representations verify the chain
graph. Typed value-array argument stores now retain those checks after lowering.
CIL101/object IR61; native79, runtime and quotas unchanged. Batch013 passes 111 focused
checks, 2,058 new native/.NET comparisons and allocation-failure recovery at O0/O2.
CircularBuffer requires a new frozen selected-root trial; no external retry ran.


### Bounded Decimal route arithmetic (AC8.70)

The owned Decimal value implements Int32 construction/conversion, multiplication,
division and directly proven invariant-provider text. A reusable managed helper
preserves the 96-bit coefficient, sign, scale, nearest-even rounding, overflow and
division-by-zero behavior. Fixed six-word scratch arrays use existing managed
operations; there is no new native numeric ABI or floating-point approximation.
CIL102/object IR62 record Decimal provider admission; native79 and production limits
are unchanged. Constants, five-part/other constructors, parsing, other operators,
equality/hashing and general formatting remain diagnostic.

The final merged snapshot passes 71 focused/adjacent checks, including 38,766 new
O0/O2 native/.NET comparisons with ASan/UBSan, forced collection, exact root/borrow
cleanup and frame-budget boundaries. The frozen engine advances past Decimal to
missing Enumerable.SequenceEqual in redirect-edit admission. This remains partial
closure discovery, without full engine execution, fingerprint parity or replay.
See ADR AC0214 and artifacts/alchemy-decimal{,-merged,-refresh,-engine}.


### Explicit-character and directional trimming (compatibility016)

Owned String.Trim/TrimStart/TrimEnd now support Char and Char[] arguments; parameterless
TrimStart/TrimEnd share the existing Unicode whitespace classifier. Exact UTF-16 set
membership, null/empty-set whitespace fallback, receiver identity, canonical empty
results and call evaluation order are preserved through managed operations. All19
focused/adjacent checks pass, including15,042 new repeated O0/O2 native/.NET comparisons,
forced GC, sanitizers and frame/root boundaries. No profiles, grants, compiler/runtime
operations or quotas change. Actual Albireo.Base32 execution and String.ToCharArray
remain unverified/unsupported respectively; see compatibility batch014 and ADR
compatibility-016-character-set-trimming. Span overloads and full replay remain open.


### Bounded SequenceEqual traversal (AC8.71)

SequenceEqual now accepts two admitted sequences, with default equality or an
admitted explicit comparer. Preserve first/second null precedence, collection
count shortcuts, indexed reads and paired-enumerator acquisition, callbacks and
reverse disposal. Exact arrays/lists retain rooted backing-storage/count snapshots,
including across callback mutations; other shapes use the collection or cursor path.
Default value equality receives copies, so mutating Equals cannot alter the source.
Existing unsupported key types remain diagnostic, including Nullable.

Ten focused checks pass, including 7,920 O0/O2 native/.NET comparisons with forced
collection, sanitizers and exact cleanup/frame-budget checks. No compiler profile,
intrinsic, runtime, emitter or production quota change. The frozen engine advances
to Stack<T>'s unimplemented nongeneric enumerator via OfType.Count. Full engine
execution and replay remain unfinished. See ADR AC0215.


### Bounded Stack enumeration (AC8.72)

Stack<T> now has the pinned concrete Enumerator plus generic/nongeneric factories.
Version checks follow successful Push/Pop mutations; failed growth preserves active
cursors. Nonempty interface factories box independent mutable cursors; empty factories
share the existing per-T array/list cursor. Preserve reverse order, copied/current
values, reset/dispose behavior and exact System.Collections mutation text. Concrete
empty/default cursors retain their distinct behavior. Direct nested Enumerator use
requires its exact reference-to-core mapping alongside Stack<T>.

All 35 focused/adjacent checks pass, including 3,234 new O0/O2 native/.NET comparisons,
existing lifetime tests and two new quota/cursor recovery checks. Compiler profiles,
runtime, emitter and production quotas are unchanged. The frozen engine advances to
its diagnostic 3,072 cached-type allowance; full admission/execution/replay remain
unfinished. See ADR AC0216.


### Parameterless string character arrays (compatibility017)

String.ToCharArray now returns a fresh exact UTF-16 Char vector for nonempty strings
and the existing per-instance empty Char array for empty strings. Normal null-receiver
faults, copy independence and roots across allocation remain intact through managed
operations. All27 focused/adjacent checks pass, including16,068 new repeated O0/O2
native/.NET comparisons and every UTF-16 code unit, forced GC, sanitizers and frame/root
boundaries. Minimal readonly in-string composition passes; Func<Char,Boolean> remains
a diagnosed narrow delegate-parameter gap. Range ToCharArray and actual candidate
execution remain unverified. No profiles, grants, runtime/compiler operations or
quotas change. See compatibility batch015 and ADR compatibility-017-string-character-arrays.


### Later interface-body audit headroom (AC8.73)

The diagnostic scan passes Stack enumeration, then exhausts cached types, fields,
call arguments and combined interface-method declarations. Increase only isolated
audit allowances to types 4,096, fields 8,192, call arguments 131,072 and methods 16,384.
After measuring 259,735/262,144 instructions and 11,747/12,288 locals, add proactive
headroom to 393,216 instructions, 16,384 locals and 6,291,456 metadata units; these last
three original limits were not exhausted in that run. Parameters remain 32,768.
Production limits and the 60-second probe bound remain unchanged.

The next semantic gap is Int32.Parse(String,IFormatProvider) in the unchanged
ActivationCountDamage operation, using directly supplied InvariantCulture. EngineStep
still fails during discovery; StateLookup validates a 15-method program. These diagnostic
scans suppress C++ emission for both roots; no probe output is compiled/executed natively. Full engine/fingerprint/replay work
remains unfinished. See ADR AC0217.


### Exact Char delegate parameters (compatibility batch 016)

The integer-object profile admits exact Char parameters using its existing Int32
carrier and UInt16 argument normalization. Target scalar identity is checked before
lowering; other narrow parameters and Char results remain diagnostic. All 51 focused
checks pass with 14,808 new O0/O2 native/.NET comparisons, forced GC and sanitizers.
CIL v103 records admission; object IR 62/native 79, roots, quotas and grants are
unchanged. Char-vector Enumerable.Any still reaches a separate interface gate;
actual candidate execution and full replay remain unverified. See
[decision](decisions/compatibility-018-character-delegate-parameters.md) and [results](compatibility-candidates/batch-016-results.md).


### Provider-only Int32 parsing (AC8.74)

Int32.Parse(String,IFormatProvider) now uses the existing owned Integer parser with
an exact overload-specific InvariantIntegerParsing grant and a directly proven
InvariantCulture operand. Validation derives the provider position from the selected
signature and the result width from the primitive identity. CIL104/object IR63;
native79, runtime operations and production quotas are unchanged. Other providers,
provider aliases, default overloads and Int64 explicit-style Parse remain diagnostic.

All 90 combined parsing/Char-delegate checks pass on an isolated snapshot, including
2,202 new repeated O0/O2 native/.NET comparisons for four parsing roots, forced GC,
ASan/UBSan and exact frame/root cleanup. Removing only this overload's grant rejects
it while preserving explicit-style Int32 Parse. The frozen engine passes the new call
and reaches unmapped SortedDictionary<TKey,TValue> in DispelOperation.Run. The measured
probe predates the merged Char-delegate source; full native engine/replay remains open.
See ADR AC0218. AC8.73 emission notes are corrected: diagnostic scans emit neither root.


### Exact generic Char enumeration (compatibility batch 017)

Exact Char arrays now reuse the managed generic enumeration helper with exact
Char receiver and IEnumerator<Char> validation. Any's existing body now composes.
All 90 selected checks pass, including 9,588 new repeated native/.NET comparisons
and four precise GC/cache checks. CIL105/interface-plan20 record the extension;
object IR63/native79, grants and runtime ownership are unchanged. Other narrow
generic operations remain diagnostic. The owned decoder next reaches missing
ToUpperInvariant; actual candidate execution and full replay remain unverified.
See [the decision](decisions/compatibility-019-character-array-enumeration.md).


### Pinned invariant uppercase (compatibility batch 018)

String.ToUpperInvariant reuses pinned Unicode 16.0.0 data with exact invariant
long-s/dotless-i rules, identity and UTF-16 width preservation. All 39 selected
checks pass, including 18,564 new repeated native/.NET comparisons and exact
lookup comparison over all Unicode values. Existing lowercase/ordinal behavior
is preserved; no compiler/runtime profile, grant or quota changes. The owned
decoder API composition passes, but actual candidate execution and full replay
remain unverified. See [the decision](decisions/compatibility-020-invariant-uppercase.md).


### Bounded ordinal sorted dictionary (AC8.75)

SortedDictionary<string,TValue> now admits a directly proven StringComparer.Ordinal
constructor through an exact factory grant, Count/Comparer, Add, indexed read/write,
ContainsKey, TryGetValue and concrete/generic/nongeneric traversal. The owned red-black
tree preserves insertion order of comparisons, duplicate/version behavior, live current
values and shared cursor-copy paths. Empty interface cursors share the mutable array
cursor, distinct from ordinary Dictionary's empty cursor. Remaining members are absent
or diagnostic stubs; public SortedSet construction is not implemented.

All 21 focused/adjacent checks pass, including 2,592 new O0/O2 native/.NET comparisons
and two allocation-failure recovery checks, with sanitizers, forced collection and
exact frame/root cleanup. CIL106; object IR63/interface20/native79 unchanged. The frozen
scan next stops at String.LastIndexOf(Char) in SurfaceOccupants.SettleDrops. A separate
reduced probe retains the duplicate-text diagnostic for non-sealed reference values
such as List<string>; the full Dispel path is not yet admitted. No engine C++ emission,
native engine run or saved-game replay. See ADR AC0219.


### Reverse character search (AC8.76)

String.LastIndexOf now implements the Char, Char/startIndex and Char/startIndex/count
overloads through bounded managed UTF-16 scans. Preserve the pinned empty-string return
before validating bounds, then validate startIndex before count with exact parameter
names/messages. Count zero performs no reads. String needles, comparison modes and
LastIndexOfAny remain diagnostic. No new grants, compiler profiles, runtime operations
or production quotas; CIL106/object IR63/interface20/native79 remain unchanged.

All 30 forward/reverse search checks pass, including 5,640 new repeated O0/O2 native/.NET
comparisons, ASan/UBSan, forced collection and exact frame/root cleanup. The frozen engine
passes SurfaceOccupants.LastIndexOf and reaches the three-argument Enumerable.Contains
call in ForcedMovementOperation.OneOf. Full native engine/replay and the independently
confirmed polymorphic sorted-dictionary duplicate-text gap remain open. See ADR AC0220.

### Explicit-comparer sequence membership (AC8.77)

`Enumerable.Contains<T>(source,value,comparer)` now supports admitted default-key
and comparer shapes. An explicit null comparer uses default equality, including for
collections with their own Contains behavior. Exact arrays and Lists retain their
original storage/count through comparer callbacks; other sources enumerate with
normal short-circuiting and disposal. Nullable/default-key and unsupported collection
construction gaps remain diagnostic. No compiler profile, runtime or quota changes.

The broad run passed 37 of 38 checks; its sole failure was a stale negative for a
reference OfType/Select composition that now translates. Replacing it with native
positive coverage passed both optimization levels: 39 distinct final checks passed
across the broad run and focused follow-up, with 12,534 new native/.NET comparisons.
ASan/UBSan, forced collection, exact roots/borrows and frame budgets remain enabled.
The frozen engine scan now reaches String.Join in ForcedMovementOperation.Encode
(3,306,855 work units); StateLookup validates 15 methods. Neither audit emits C++.
Full engine closure/replay and non-sealed sorted-dictionary duplicate text remain
open. See ADR AC0221 for evidence and scope.

### String-sequence joining (AC8.78)

`String.Join(string,IEnumerable<string>)` now uses List/array storage shortcuts and
ordered cursor traversal for other sequences. Preserve null separator/entries,
empty and singleton identity, UTF-16 code units, length checks and finally disposal.
Other Join overloads remain diagnostic. Existing managed UTF-16 buffers and the
owned chunk builder replace pinned native/span construction; no runtime/profile or
quota changes, and concurrent source mutation is not validated.

Twenty distinct checks pass across the adjacent run and focused follow-up, including
5,376 new O0/O2 native/.NET comparisons with sanitizers, forced collection and exact
cleanup/frame budgets. The first run's two failures were an incorrect cached-delegate
expectation in the new test; fixture IL proves a fresh delegate, and the corrected
zero-root test passes. The frozen engine scan now reaches String.Replace in
SuppressTaggedEffectOperation.Label (3,310,764 units); StateLookup validates 15
methods. Both audit emission fields are null. Full closure/replay and the separate
non-sealed sorted-dictionary duplicate-text gap remain open. See ADR AC0222.

### Character replacement (AC8.79)

`String.Replace(Char,Char)` now preserves the original string for equal characters
or no match, otherwise copies exact UTF-16 units to a fresh string through existing
managed construction. String-pattern/culture overloads remain diagnostic. All 12
focused/adjacent checks pass, including 5,880 new sanitized O0/O2 native/.NET
comparisons with forced collection and exact cleanup/frame budgets.

The frozen engine scan now reaches List.RemoveAll in ObstacleOverflow.Drain
(3,379,372 work units); StateLookup validates 15 methods. Neither audit emits C++.
No profile/grant/runtime/production-budget changes; full closure and saved-game
replay remain incomplete. See ADR AC0223 for validation and the frozen hashes.

### Predicate list removal (AC8.80)

`List<T>.RemoveAll` now follows pinned live compaction, callback/exception order,
reference-bearing tail clearing and completion-only size/version publication.
Reentrant list mutation remains observable; null predicates and failed clear ranges
retain managed faults. All 19 focused/adjacent checks pass, including 4,608 new
O0/O2 native/.NET comparisons and four new live-list GC checks for references and
reference-bearing values. Existing sanitizer, cleanup and frame checks remain on.

The frozen engine now reaches the previously reduced polymorphic duplicate-text gap
in SortedDictionary.Add through DispelOperation. The collection value is a non-sealed
reference type; its ToString dispatch is not yet admitted by DictionaryKeyText.
Engine work is 3,387,734 units; StateLookup validates 15 methods. Both Emission fields
are null. Full closure and saved-game replay remain incomplete. See ADR AC0224.

### Alchemy reference: generic handler registry

A reduced owned regression, based on the separately pinned Alchemy IL2CPP
reference capture, now composes closed generic interface handlers, dictionary
lookup, ordered events and state identity across allocations. Five focused checks
pass, including 2,310 O0/O2 sanitized native/.NET comparisons and two native
lifetime checks for removed handlers and retained events. Compiler/runtime
behavior and production bounds are unchanged. This is supplementary regression
evidence; full Alchemy closure and saved-game replay remain incomplete. See the
[reference and validation record](compatibility/alchemy-handler-registry-reference.md).

### Bounded polymorphic dictionary text (compatibility 021)

Non-sealed ordinary dictionary keys/values retain receiver-bounded ToString
virtual dispatch and actual runtime class names. The private default-text grant
uses existing UTF-16 literal roots and charging; Object aliases, vectors/boxes,
reflected Type names and IFormattable contracts remain diagnostic. Both direct
and virtual calls validate receiver proofs. CIL107/object IR64/native80; interface20
and production ownership/GC/resource limits are unchanged.

120 distinct selected checks pass, including 2,598 new O0/O2 native/.NET comparisons
and native quota/cache/instance-boundary checks. Actual converted Alchemy C++
reference locations and hashes informed dynamic-name and virtual-slot tests;
its DLLs differ from the frozen audit inputs and no Unity runtime was adopted.
Frozen discovery advances to scalar boxed formatting in BoardObjectState.WithHitPoints
(3,390,014 work units); StateLookup validates 15 methods. Both emission fields remain
null. Full engine execution/replay and milestone-7 completion remain unverified.
See [compatibility 021](decisions/compatibility-021-polymorphic-key-text.md) and
`docs/compatibility-candidates/batch-019-results.md` for exact receipts and limits.

### Temporary formatting producers (AC8.81)

A bounded argument scan now handles value-type property temporaries and local/
parameter addresses while retaining independent same-block box/literal proof.
The frozen WithHitPoints IL and converted Alchemy C++ both guide the regression.
All 31 selected checks pass, including 1,764 new sanitized O0/O2 native/.NET
comparisons. CIL108; IR64/interface20/native80, grants and production limits remain
unchanged. The diagnostic engine scan passes that formatting gap and reaches the
type-relation work limit at 3,590 identities; StateLookup validates 15 methods.
Neither audit emits C++. Full closure and saved-game replay remain incomplete.
See ADR AC0225 for reference hashes, exact tests and counter evidence.

### Contiguous list removal (AC8.82)

List<T>.RemoveRange now preserves range-validation order, overlapping compaction,
positive-count version changes and reference-bearing tail clearing. All 19 focused/
adjacent checks pass, including 5,808 new O0/O2 native/.NET comparisons and four
precise lifetime checks. Converted Alchemy C++ and pinned List.cs guided the slice.
No profiles, grants, runtime or production limits change. The enlarged diagnostic
catalog uses a measured relation-work allowance of 4,194,304; production remains
1,048,576. Frozen engine discovery now reaches Math.Sign in BarrierOperation.Span;
StateLookup validates 15 methods. Both emission fields are null; full closure/replay
remain incomplete. See ADR AC0226 for exact counts, provenance and audit evidence.

### Signed integer Sign (AC8.83)

Math.Sign(Int32/Int64) returns -1/0/1 through managed comparisons, including signed
minimum values without negation. Converted barrier-span calls guide the regression.
All 10 focused/adjacent checks pass, including 1,296 new sanitized O0/O2 native/.NET
comparisons and exact overload admission. No profiles, grants or production limits
change. Frozen discovery passes Sign and exhausts 8,192 diagnostic snapshot fields.
A diagnostic-only increase to 16,384 reaches Guid.TryParseExact in shield-option
parsing; StateLookup validates 15 methods. Both emission fields are null. Full
closure/replay remain incomplete; see ADR AC0227.

### Exact Guid string parsing (AC8.84)

Guid.TryParseExact(String,String,out Guid) now selects D/N/B/P/X explicitly,
preserving case handling, Unicode whitespace, legacy grammar and default output
on failure. Converted shield parsing and pinned Guid.cs guided the change. All
16 distinct focused/adjacent checks pass, including 13,230 new sanitized O0/O2
native/.NET comparisons with forced collection and borrowed storage. Profiles,
grants, runtime and production budgets remain unchanged.

Frozen discovery reaches the dynamic type-name string budget after 15,253 bodies;
StateLookup validates 15 methods. Both audit emission fields are null. Full engine
IR admission and saved-game replay remain incomplete. See ADR AC0228 for exact
validation, the fixture correction and diagnostic timeout/follow-up evidence.

### Same-assembly instance fields (AC8.85)

Reference-instance fields now use the shared field-access rule for internal and
family-or-assembly members, with exact image identity and validated lexical
visibility. Proven nested callers can access private containing-type members;
readonly, inaccessible nesting and foreign-image restrictions remain. Frozen
PhysicalDamagePayload.Invocation and the converted engine guided the regression.
CIL109/object IR65/native81; interface20, physical layouts and production limits
remain unchanged. All 81 distinct selected checks pass, including 2,058 new O0/O2
native/.NET comparisons and logical/scalar tampering checks.

Diagnostic string charging measures 352,292 UTF-16 units, above production 262,144;
only the audit copy receives more room. The fixed frozen engine reaches aggregate
value-IR capacity after discovery/access validation. Neither root emits C++; full
engine closure and saved-game replay remain incomplete. See ADR AC0229.


### Proven non-null field returns (AC8.86)

The integer-object translator now admits a direct struct-return call stored into
an instance field when the receiver is proven non-null before RHS evaluation.
A bounded forward analysis intersects local/input facts across incoming edges,
recognizes stable constructor receivers and allocations, and excludes address-taken
locals. A completed field access also proves a stable instance receiver for later
loads and normal successors. Exception entries and finally-capable leaves drop local facts. It never
moves a null check, reads a receiver again, or changes the existing caller-owned
return packet and final field-copy lowering.

All 32 selected checks pass, including 3,948 new O0/O2 native/.NET comparisons
with forced collection and ASan/UBSan. Nullable destinations, nullable loop paths,
borrowed aliases and finally mutation remain diagnostic. CIL110 identifies the
new admission; IR65, native81, runtime and production resource limits are unchanged.
The proof has its own 1,048,576-work cap per method. See ADR AC0230 for the caught
exception-order regression, converted reference, review and frozen body inventory.


### Array-length zero comparisons (AC8.87)

CIL111 admits adjacent ldlen/ldc.i4.0/ceq or cgt.un as a Boolean observation of
bounded vectors. The array null check stays in place; split consumers, nonzero
comparisons and general native-integer flow remain diagnostic. Converted
EffectOperationDescriptor.Supports guided this slice. All 27 selected checks pass,
including 1,764 new sanitized O0/O2 native/.NET comparisons. IR65/native81, owned
core, grants and budgets are unchanged. Full closure/replay remain incomplete;
see ADR AC0231.


### Stable location joins (AC8.88)

CIL112 carries identical local/parameter slot identities across branches by
recreating their addresses in successor blocks. Borrowed arguments retain the
current contents and existing method lifetime. Different origins and derived
heap/array addresses remain diagnostic. All 59 distinct selected checks pass,
including 2,646 new sanitized O0/O2 native/.NET comparisons. IR65/native81, owned
core, grants and budgets are unchanged. The combined diagnostic-only inventory
now has 28 body failures (15,225 individually translated); it is not full IR/native
admission or saved-game replay. See ADR AC0232.


### Invariant Int64 Integer TryParse (AC8.89)

Int64.TryParse now accepts explicit NumberStyles.Integer alongside None with a
proven invariant provider. It reuses the throwing parser's signed grammar and
clears output on failure. CIL113/object IR66/native82 share the exact style gate;
grants, native algorithms and budgets are unchanged. All 31 selected checks pass,
including 3,660 new sanitized O0/O2 native/.NET comparisons. The rebuilt-core
inventory clears both parser blockers and now has 26 body failures; it returns no
program or native emission. Full closure/replay remain incomplete; see ADR AC0233.


### Readonly value receivers (AC8.90)

Bounded metadata reads retain type/member IsReadOnlyAttribute evidence. Ordinary
value and primitive methods receive readonly borrows, preserved through scalar
expansion, boxed dispatch and value-key calls. Both IR stages reject writes;
constructors retain writable receivers and mutable methods still use C# defensive
copies. Owned primitive readers and nullable accessors are explicitly readonly;
Nullable.ToString remains mutable to preserve payload override semantics.
CIL114/object IR67/native83 identify the contract; grants, native algorithms,
physical layouts and production limits are unchanged.

All 119 distinct selected checks pass, including 2,646 new sanitized O0/O2
native/.NET comparisons. The converted EffectTarget getter guided the regression.
The frozen inventory clears all 11 readonly mismatches: 15,238/15,254 bodies now
translate individually, leaving 16 failures, including a newly exposed ldvirtftn.
No full IR program or native engine is produced; replay remains incomplete.
See ADR AC0234 for exact scope, review and evidence.


### Checked static storage borrows (AC8.91)

ldsflda now borrows admitted Int32/Int64/reference/inline-value static storage,
including closed generic owners and empty values. Scalar expansion preserves the
same per-instance slots; static references remain GC roots. Every native carrier
requires an active frame token from the current context, exact slot/type metadata
and readonly capability. Existing initializer order and sticky faults are reused.
Readonly static addresses remain readonly even inside initializers; narrow-field
addresses and the specialized String.Empty address remain diagnostic.
CIL115/object IR68/native84; grants and production resource limits are unchanged.

All 99 distinct selected checks pass, including 3,528 new sanitized O0/O2
native/.NET comparisons and native carrier lifetime/type/ownership checks.
Converted EffectiveSpellCompiler.Failure guided the static readonly regression.
The frozen inventory clears all eight static-address failures: 15,246/15,254 bodies
translate individually, with seven address joins and one ldvirtftn failure left.
No full program/native engine is produced; replay remains incomplete. See ADR AC0235.


### Exact final virtual delegate targets (AC8.92)

Adjacent dup/ldvirtftn/newobj can bind a declaration that itself resolves to a
final virtual class method. The receiver is evaluated once, the exact target is
retained and typed signature checks run before scalar lowering. Both IR stages
validate final-target metadata and adjacency evidence. Like the measured .NET
oracle, final-method lookup folds to direct binding; null receivers fail delegate
construction with ArgumentException before allocation. Overridable/interface
operands, base-slot references to sealed overrides and other pointer patterns
remain diagnostic. CIL116/object IR69/native85; runtime/grants/budgets unchanged.

All 61 distinct selected checks pass, including 2,058 new sanitized O0/O2
native/.NET comparisons. The frozen inventory clears ForcedMovementOperation.Run:
15,247/15,254 bodies translate individually, leaving seven address joins. It emits
no program or native engine; full closure and replay remain incomplete. See ADR AC0236.


### Checked branch references (AC8.93)

Integer-object control flow now carries checked managed locations and their owner
roots across branches, including selections between different variables and
heap/array/static locations. Readonly widening and exact element types are
validated at both IR stages; constructor-this identity remains explicit for
readonly initialization. Self-edge copies use separate source/input slots.
CIL117/object IR70/native86; runtime representation, grants and production budgets
are unchanged. Converted EffectOperationInstruction.Run guided the regression.

All 181 distinct selected checks pass, including 6,174 new/promoted sanitized
O0/O2 native/.NET comparisons. The reviewed slice clears all body-level failures:
15,254/15,254 frozen bodies translate individually (3,839,228 metadata units,
63,374 ms). The completed bodies total 61,756 blocks, 294,454 statements and 1,021
exception regions. This diagnostic run returns no program; production aggregate
capacity, full logical/scalar validation, native engine and saved-game replay
remain incomplete. See ADR AC0237 for lifetime reasoning, review corrections and
exact evidence.


### Hidden generic value arrays (AC8.94)

Instruction-scoped bare-variable array evidence now covers hidden inline values
and Int32 enums as well as references. Both IR stages preserve exact closed type,
array kind and operation identity; scalar allocation retains value visibility
facts. Explicit foreign names and unrelated type tests remain restricted.
CIL118/object IR71; native86, layouts, core, grants and production budgets unchanged.
All 92 selected checks pass, including 2,940 new sanitized native/.NET comparisons.

The bounded full-IR inventory now reaches cross-method/catalog checks. After a
diagnostic-only string allowance (3,512 distinct literals measured), 35 bodies
still fail validation: generic type tests/casts, direct hidden value arrays,
constructor initialization and one delegate capture. All 15,254 bodies translate
before validation; no engine program is admitted or emitted. Production capacity
and full saved-game replay remain incomplete. See ADR AC0238 for limits, exact
failure counts, review correction and converted-source guidance.


### Direct value-array scope checks (AC8.95)

Inline layouts retain their lexical declaring scopes through scalar lowering.
Direct arrays of visible internal/nested value types use the same bounded access
rule as reference types; conflicting or forged scope facts are rejected.
CIL119/object IR72/value layout model5; physical layouts, native86, core, grants
and production budgets unchanged. All 119 selected tests pass, including 1,764
new sanitized native/.NET comparisons.

The full-IR inventory clears the three direct hidden-value-array failures and
leaves 32 first-body validation failures: 29 generic type tests/casts, two constructor
prefix checks and one delegate capture. All 15,254 bodies still translate, but
full logical/scalar closure, production capacity, native engine and saved-game
replay remain unfinished. See ADR AC0239.


### Generic reference casts and type tests (AC8.96)

Original bare-variable and single-vector-variable type-test sites retain their
closed context, parameter identity and exact operation/offset. Both IR stages
validate that evidence before allowing a hidden substituted target. Array
allocation proof remains separate; foreign explicit names and members retain
access checks. CIL120/object IR73; native86, layouts, core, grants and production
budgets unchanged. All 104 distinct selected checks pass, including 4,998
new/promoted sanitized O0/O2 native/.NET comparisons. Converted CastIterator code
guided the independent implementation; see ADR AC0240 for review and receipts.

The full-IR inventory clears 23 cast/test failures and leaves nine failing bodies:
six same-assembly type tests from generic callers, two constructor initialization
paths and one delegate capture. All 15,254 bodies translate. Full validated closure,
production capacity, native engine and saved-game replay remain incomplete.


### Generic callers and assembly-visible types (AC8.97)

Same-image assembly type visibility is checked before private generic nesting
restrictions. This aligns detached type tests with source access rules without
relaxing private containment or foreign image checks. CIL121/object IR74; native86,
layouts, core, grants and production budgets unchanged. All 35 selected checks pass,
including 1,470 new sanitized native/.NET comparisons. See ADR AC0241.

The full-IR audit clears all six remaining type-test failures. Three failing bodies
remain: two constructor initialization paths and one delegate capture. All 15,254
bodies translate; complete validated closure, production capacity, native engine
and saved-game replay remain unfinished.


### Branched constructor initialization (AC8.98)

A bounded block-state check admits argument branches/loops before a single direct
base or same-owner constructor call. It preserves receiver identity, evaluation
order and roots while rejecting receiver escape, ambiguous initialization paths,
local recovery of initialization faults and repeated/cyclic initialization.
CIL122/object IR75; native86, layouts, core, grants and production budgets unchanged.
All 119 selected checks pass, including 2,352 new sanitized native/.NET comparisons
and exact cached-delegate retention. Converted constructor code guided lifetime
checks. See ADR AC0242 for design, review and receipts.

The full-IR inventory now leaves one failing delegate capture among 15,254
translated bodies. Full validated closure, production capacity, native engine and
saved-game replay remain unfinished.
