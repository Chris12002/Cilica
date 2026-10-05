# Embedding ABI: versioned scalar and object profiles

The current module profile is `embedding-object-handles-v3`, described below.
It adds typed retained object handles and void results to shared export modules.
V1/V2 remain supported. Local evidence is macOS ARM64; mobile support is unverified.

## Persistent Int32 profile v1

The implemented `embedding-int32-v1` profile is milestone 7a's bounded foundation.
It is tested locally on macOS ARM64 with Apple Clang; iOS and Android packaging
and device support remain planned. It does not yet provide the full public
object/value SDK described in the design specification.

## Generating a library

`EmbeddingCppEmitter.EmitProgram(ObjectIrProgram)` or
`EmbeddingCppEmitter.EmitExceptions(ScalarManagedExceptionIrProgram)` returns an
`EmbeddingCppArtifact` containing `Header`, `Text` and `SymbolPrefix`. Pass an
already translated/validated program; for composed managed code use
`ValueScalarLowering.LowerExceptions` before emission. Compilation reads input
assemblies as data. The emitter does not run a native compiler or load the input.

Save `Header` as the consumer's `.h` and `Text` as `.cpp`. The C++ source includes
its own identical declarations and existing runtime implementation. Compile as
C++20 with exceptions/RTTI disabled and `-fvisibility=hidden`; the header explicitly
exports just the four embedding functions with C linkage. Consumers compile as
C11 or C++. Native builds/linking remain explicit host actions. The native tests
demonstrate a separate C object linked to a shared library.

Each function name starts with the generated `SymbolPrefix`. It includes the
content-scoped entry identity, so it changes with the input assembly. This is not
yet a stable application export name. One artifact exposes one Int32-returning
export with only Int32 parameters (zero parameters are permitted). Objects,
strings, 64-bit integers, inline values and borrows cannot cross this boundary.
The existing supported semantics may execute inside the closure.

## Functions and ownership

| Suffix | Contract |
| --- | --- |
| `_version()` | Returns `1`; no instance required |
| `_create(version, flags, uint64_t* runtime)` | Requires version `1`; creates an independent instance and writes its nonzero token on success |
| `_invoke(runtime, const int32_t* arguments, uint32_t count, cilica_v1_result* result)` | Exact declared arity; runs synchronously and copies the result/fault report into caller storage |
| `_destroy(runtime)` | Rejects an active invocation; otherwise invalidates the token and releases its entire heap and runtime-owned state |

The caller owns all argument/output storage. Nonnull pointers must be properly
aligned and point to valid memory of the declared extent for the duration of the
call. Outputs must be disjoint from input storage. Null arguments are accepted
only for zero arity; null output pointers are invalid. No input pointer is retained.
Arbitrary nonnull pointers cannot be validated by this C API; it is not a sandbox.

Runtime tokens are opaque library/closure-scoped identifiers, not pointers, object
handles, serializable state or capabilities for an untrusted caller. Never send a
token to another artifact's functions: separate artifact registries can issue the
same numeric token. Within one loaded artifact, released tokens never become valid
again, including after slot reuse. Zero and unknown/stale tokens are rejected
before accessing a heap. Unloading/reloading a dynamic library resets its identity
domain and requires discarding every old token. Safe dynamic unloading is not promised.

Independent instances may execute concurrently. Entry to one instance is serialized
by the host (later adapters use a dedicated worker). The boundary rejects overlapping
or reentrant entry and destruction with `BUSY`; it does not queue calls, cancel an
active call or start a worker. If invoke races destroy, either invocation acquires
the instance and destruction is busy, or destruction wins and invocation sees an
invalid token. Destruction that reports busy must be retried after the call finishes.

## Status and results

All statuses use `uint32_t`, not C enum layout. Macros are in the generated header.

| Value | Macro suffix | Meaning |
| --- | --- | --- |
| 0 | `OK` | Operation completed |
| 1 | `INVALID_ARGUMENT` | Null required pointer, wrong arity or unknown create flags |
| 2 | `UNSUPPORTED_VERSION` | Requested ABI version differs from 1 |
| 3 | `INVALID_RUNTIME` | Zero, unknown or released token in this registry |
| 4 | `BUSY` | Runtime already has an active invocation |
| 5 | `LIMIT` | All 64 slots are occupied or token issuance is exhausted |
| 6 | `EXECUTION_FAILED` | Managed execution reported a fault; inspect the copied report |
| 7 | `ALLOCATION_FAILED` | Native allocation of the instance failed |

Argument checks precede version/instance checks. Outputs are unchanged for host
errors. An admitted invocation replaces its entire result report, even on execution
failure. `cilica_v1_result` has an `int32_t value`, three `uint32_t` fields
(`fault_code`, `method_token`, `il_offset`) and `char image_hash[65]`. This is a C
structure with the platform C layout, not a packed serialization format.

Success has a value and zero fault/origin fields, including when managed code caught
an exception. Failure has a zero value, diagnostic fault code and method/IL/source
image origin where available. The image field contains copied lowercase SHA-256
text terminated with zero, or is empty when no origin is available. It remains
valid after subsequent calls and runtime destruction because the caller owns it.
No managed object or borrowed string crosses the boundary. Native unwinding does
not carry managed exceptions through C.

Fault codes retain the native v35 diagnostic categories: 1 divide by zero, 2 overflow,
4 null, 5 allocation, 6 ownership/type, 7 invalid cast, 8 call depth, 9 execution
storage, 10 bounds, 11 array type mismatch, 12 argument and 13 explicit managed
exception. These are not exception type identity; richer exception type/message
copying is a later gate. Managed side effects before a fault remain visible on the
next call. Each invocation starts with fresh pending-fault state and frames.

## Bounds and version policy

Each emitted registry admits 64 live runtime instances. Each instance retains the
existing defaults: 1 MiB managed allocation accounting, 4,096 live objects, 1 MiB
execution arena per call and 64 active managed frames. Native registry/instance
metadata and private auxiliary allocations are separate; these are not a total
process-memory cap. Token exhaustion is permanent rather than wrapping.

Flags `0` select ordinary collection. Bit 0 (`COLLECT_BEFORE_ALLOCATION`) enables
the existing stress collector; other bits are rejected. No configuration growth,
capability query or per-instance quota adjustment is admitted yet.

Incompatible changes require a new versioned symbol and header contract. Future
multi-export manifests, retained-object APIs and structured value/error transfer
will be specified explicitly; v1 does not imply their availability.

## Shared export module v2

`EmbeddingModuleEmitter.EmitProgram(ObjectIrProgram, EmbeddingModuleContract)` or
`EmitExceptions(ManagedExceptionIrProgram, EmbeddingModuleContract)` returns
`SymbolPrefix`, `Header`, `Text`, `ManifestJson` and `ContractHash`. The composed
entry takes logical managed-exception IR and performs lowering itself: export
signatures are checked before structs can flatten into scalar fields. A struct
with one integer field is not an Int32 parameter for export admission.

Supply one validated compiled closure and a contract with a module name, nonzero
schema version and 1..64 `EmbeddingExport` declarations. Each declaration contains
a nonzero UInt32 ID, name, exact `IrMethodId` and expected parameter count. The
method must already be present, public/static on an externally visible owner,
have logical Int32 parameters/result, and not be an initializer or special-name
method. Missing methods, wrong arity and duplicate IDs/names/methods are errors.
Names are 1..48 lowercase ASCII characters, begin with a letter and contain only
letters, digits and single underscores; trailing/doubled underscores are rejected
to avoid reserved C++ identifiers. Authored detached IR is subject to the existing
validation contract, not authenticated as original metadata by the emitter.

The compiler still discovers one ordinary inclusion root. An application facade
can reference its exports from that root; the module calls selected methods directly
and never executes the inclusion root at create time. Independently discovering
several roots and merging independently compiled programs are not implemented.
All exports share one emitted descriptor catalog, static storage, initialization
state and collector. No method becomes exported just because it is reachable.

The stable symbol prefix is `cilica_<module-name>_abi_v2`. Application authors assign
IDs explicitly; changing method tokens, source hashes or declaration order cannot
renumber them. The generated header defines `<PREFIX>_EXPORT_<NAME>`,
`<PREFIX>_SCHEMA_VERSION` and `<PREFIX>_CONTRACT_HASH` macros (uppercase prefix/name).
Keep one module artifact per generated library and give modules distinct names.
Do not combine multiple copies of the same emitted closure in one link: legacy
internal entry symbols are not independently namespaced by module name.

The manifest has `profile`, `contractHash`, `contract` and `bindings`. `contract`
contains `abiVersion`, `name`, `schemaVersion` and exports sorted by numeric ID;
each export has `id`, `name`, a `parameters` array of `"int32"` and `result: "int32"`.
`bindings` separately records the exact image hash, method token and specialization.
The fingerprint is lowercase SHA-256 of the emitter's compact UTF-8 JSON `contract`
object, with the documented property order and no whitespace/newline. Use the
generated fingerprint rather than hashing the pretty-printed manifest. It verifies
interface agreement, not code integrity or behavioral equivalence. Deliberate
semantic changes that require new clients must bump the application schema version.

### V2 functions

| Suffix | Contract |
| --- | --- |
| `_version()` | Returns 2 |
| `_capabilities()` | Returns UInt64 feature bits, currently 63 |
| `_create(version, contract_hash, config, runtime)` | Requires version 2 and the host's expected 65-byte terminated contract fingerprint; nullable config selects defaults |
| `_invoke(runtime, export_id, arguments, count, result)` | Dispatches the declared ID with exact arity on this runtime's shared heap |
| `_destroy(runtime)` | Same guarded teardown and stale-token behavior as v1 |

Capability bits are Int32 storage (1), persistence (2), multiple exports (4), copied
fault origins (8), collection stress (16) and configurable quotas (32). They do not
advertise retained-object APIs, structured values/messages, general filters,
managed threading, async execution or any mobile platform.

`cilica_v2_config` is five UInt32 fields in order: `struct_size`, `flags`,
`heap_bytes`, `object_limit`, `execution_bytes`. `CILICA_V2_CONFIG_INIT` initializes
the exact 20-byte structure and defaults (flags 0, 1 MiB heap accounting, 4,096
objects, 1 MiB execution arena). Sizes must match exactly, with no unknown flags.
Each quota may be lowered to zero but cannot exceed its default maximum. Zero is
a real zero budget, not a default sentinel. Flags bit 0 selects collection before
allocation. The config is copied at create and never retained; every call uses
that instance's execution limit. Native registry/auxiliary allocations remain
outside these quotas, and the 64-runtime registry/64-managed-call-depth limits
remain fixed. No total-process-memory guarantee follows.

Status macros have prefix `CILICA_V2_`: values 0..7 retain v1 meanings, 8 is
`UNKNOWN_EXPORT`, and 9 is `CONTRACT_MISMATCH`. Create checks required pointers,
version, fingerprint and config before native allocation. Invoke checks the result
pointer, ID and arity/input pointer before runtime validity/busy state. Host errors
leave outputs unchanged. `cilica_v2_result` has the same documented fields and
copy semantics as the separate v1 result type. It zeroes all fault fields on
success and the value on execution failure. The shared guard covers every export;
calling a different export cannot bypass an active invocation.

V1's pointer-validity, disjoint-storage, runtime-token scope, scheduling, ownership
and unloading rules also apply to v2. Pass the expected hash compiled into the host's
header; obtaining the current library's hash blindly would defeat mismatch checks.
The hash pointer must reference 65 readable bytes. No native pointers cross as
runtime tokens or managed values.

Example C call after including the generated `counter` header:

```c
cilica_v2_config config = CILICA_V2_CONFIG_INIT;
uint64_t runtime = 0;
uint32_t status = cilica_counter_abi_v2_create(
    2, CILICA_COUNTER_ABI_V2_CONTRACT_HASH, &config, &runtime);
if (status == CILICA_V2_OK) {
    int32_t input = 10;
    cilica_v2_result result;
    status = cilica_counter_abi_v2_invoke(
        runtime, CILICA_COUNTER_ABI_V2_EXPORT_RESET, &input, 1, &result);
    /* Inspect status; result is written only for OK or EXECUTION_FAILED. */
    uint32_t destroyed = cilica_counter_abi_v2_destroy(runtime);
    /* Handle BUSY by waiting for an active call before retrying destruction. */
    (void)destroyed;
}
```

## Retained object module v3

`EmbeddingObjectModuleEmitter.EmitProgram` accepts ordinary object IR;
`EmitExceptions` accepts composed managed-exception IR. Both return the same
header/source/manifest artifact shape as v2. The prefix is
`cilica_<module-name>_abi_v3`. Contracts select 1..64 public ordinary static methods
already in one validated closure; creating the runtime does not execute its root.

`EmbeddingObjectContract` declares stable reference aliases (`Id`, `Name`, bound
`ObjectTypeId`) and `EmbeddingObjectExport` signatures. Shapes are Int32, Handle
with a reference alias ID, or Void for results only. Every alias must be used;
alias IDs, names and bound types are unique. Parameters/results must exactly match
logical IR signatures before lowering. At invocation, reference arguments accept
null and runtime types assignable to the declared parameter, including derived
classes. Unsupported shapes produce compiler-library argument errors.

The canonical manifest hashes ABI version 3, module/schema names, sorted reference
aliases and sorted typed exports. Physical method/type bindings are listed
separately and do not participate in that hash. As in v2, the application maintains
the semantic meaning of its stable IDs/schema; a hash is not a behavioral proof.

### C layout and ownership

`cilica_v3_value` contains UInt32 `kind`, UInt32 `reserved`, UInt64 `payload`, in
that order (16 bytes, payload offset 8 on the tested target). The kinds are:

| Kind | Payload |
| --- | --- |
| `CILICA_V3_INT32` (1) | Raw Int32 bits in the low 32 bits, zero-extended; upper 32 bits must be zero |
| `CILICA_V3_HANDLE` (2) | Opaque object lease token; zero denotes a null reference |
| `CILICA_V3_VOID` (0) | Result only; payload zero |

`reserved` must be zero. Negative Int32 inputs are encoded by casting to UInt32
before widening; sign extension to UInt64 is rejected. Use the low 32 result bits
as the Int32 representation. No native managed pointer is public.

`cilica_v3_result` contains `value`, then UInt32 `fault_code`, `method_token`,
`il_offset`, then `char image_hash[65]`. Use the emitted header and native layout;
do not pack records or serialize padding. Successful calls clear fault fields.
Execution failures zero the value and copy the fault code/origin, with the same
origin and lifetime rules as v1/v2. Exception types/messages are not transferred.

Object results grant one strong lease. Returning the same object again or calling
`retain_handle(runtime, token, &copy)` grants another, distinct token. Tokens
identify leases, not object identity. Use managed exports for identity comparisons.
`release_handle(runtime, token)` removes that lease; other handles and managed
references can keep the object alive. Reclamation occurs at a later collection.
Neither passing a token as an argument nor an invocation failure consumes it.
Destroy invalidates every lease belonging to the runtime. Release or reuse of an
already released token fails. Retain/release of zero succeed with a valid runtime;
retaining zero writes zero and uses no slot.

Tokens are scoped to the originating emitted module and runtime. Foreign-instance
and stale tokens are rejected. Never transfer tokens between modules, library
loads or processes; separate modules can issue the same numeric token. Handle and
runtime tokens are different domains even when their numeric values happen to
match. Hosts keep pointer arguments readable/writable and stable during the call;
the adapter retains no host buffer pointer.

### Calls, quotas and failure order

The API exports version/capabilities/create/invoke/destroy as in v2, using v3 types,
and adds `retain_handle` and `release_handle`. Capabilities total 255: the six v2
bits plus `CILICA_V3_CAP_HANDLES` (64) and `CILICA_V3_CAP_VOID` (128).
`cilica_v3_config` has six UInt32 fields in order: `struct_size`, `flags`,
`heap_bytes`, `object_limit`, `execution_bytes`, `handle_limit` (24 bytes).
`CILICA_V3_CONFIG_INIT` gives the v2 budgets plus 1,024 handles. All budgets may
only be lowered, including to zero. The private handle table is a bounded native
allocation separate from the managed heap budget. There are at most 64 runtimes
per emitted module, as in v2.

Statuses 0..9 keep the v2 meanings. V3 adds `INVALID_HANDLE` (10) and
`TYPE_MISMATCH` (11). Unknown export/arity/pointer checks precede runtime admission;
then each argument is validated in order for tag/reserved/scalar encoding, handle
ownership and reference assignability. These errors leave result buffers unchanged
and do not execute managed code. Invoke, retain, release and destroy share one
admission guard; overlapping operations return `BUSY`. Hosts must serialize/retry
as appropriate, including a release that returned `BUSY`.

After argument validation, each reference-returning call reserves a strong-handle
slot **before managed execution**. Full quotas return `LIMIT` with outputs and
application state unchanged, even when the method would eventually return null or
throw. Native allocation failure during reservation returns `ALLOCATION_FAILED`.
The adapter immediately publishes a nonnull returned object into its reserved
slot without any managed allocation. Null results use HANDLE/zero and release the
reservation. Faults and execution-arena failures also release it; ordinary managed
side effects before an execution failure are not rolled back. Scalar/void exports
require no output handle slot. Retain is independently quota-limited and leaves
its output unchanged on failure.

The existing unloading, C linkage and platform rules continue to apply. V3 has
local C11/shared-library and native race evidence under O0/O2 sanitizers, including
forced collection and both emitter paths. It does not yet provide string buffers,
64-bit/value transfer, rich exception copying, iOS or Android device evidence.
