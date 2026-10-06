# Embedding ABI: versioned scalar and object profiles

The current module profile is `embedding-object-handles-v3`, described below.
It adds typed retained object handles, exact Integer64 values, void results and optional UTF-16 copying
to shared export modules, with optional retained exception capture and bounded numeric records.
V1/V2 remain supported. Local evidence covers macOS ARM64 and the bounded iOS
simulator checks below; physical-device support remains unverified.

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
`ObjectTypeId`) and `EmbeddingObjectExport` signatures. Shapes are Int32, Integer64, Handle
with a reference alias ID, Record with a record alias ID, or Void for results only.
Record declarations and invocation are described under 7c.5 below. Every alias must be used;
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
| `CILICA_V3_INTEGER64` (3) | All 64 raw integer bits; managed operations determine signedness |
| `CILICA_V3_RECORD` (4) | Record result only; stable record ID, with fields copied to a separate buffer |
| `CILICA_V3_VOID` (0) | Result only; payload zero |

`reserved` must be zero. Negative Int32 inputs are encoded by casting to UInt32
before widening; sign extension to UInt64 is rejected. Use the low 32 result bits
as the Int32 representation. No native managed pointer is public.

`cilica_v3_result` contains `value`, then UInt32 `fault_code`, `method_token`,
`il_offset`, then `char image_hash[65]`. Use the emitted header and native layout;
do not pack records or serialize padding. Successful calls clear fault fields.
Execution failures zero the value and copy the fault code/origin, with the same
origin and lifetime rules as v1/v2. The ordinary invoke result does not transfer exception types/messages; the optional
capture API below provides them separately.

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
and adds `retain_handle` and `release_handle`. Base capabilities total 255: the six v2
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
forced collection and both emitter paths. The optional string extension below adds
host-buffer copying. Reference-bearing record transfer, inner-exception traversal, stack traces, iOS and Android
device evidence remain planned. Optional exception type/message copying is described
below.

### Optional UTF-16 copying (7c.2)

Set `EmbeddingObjectContract.Utf16StringTypeId` to the ID of a declared reference
alias bound to the closure's exact intrinsic String type. That alias must be used
by an export, as with other reference aliases. Its default value zero disables
this capability. Enabled contracts add `utf16StringTypeId` to the canonical schema
and hash, and advertise `CILICA_V3_CAP_UTF16` (256; total capabilities 511). Disabled
contracts retain the original v3 schema/hash and omit these function declarations.
Existing v3 record layouts are unchanged.

The enabled header adds:

```c
uint32_t cilica_example_abi_v3_string_create(
    uint64_t runtime, const uint16_t* units, uint32_t length, uint64_t* handle);
uint32_t cilica_example_abi_v3_string_copy(
    uint64_t runtime, uint64_t handle, uint16_t* units,
    uint32_t capacity, uint32_t* length);
```

Replace `example` with the module name. The UInt16 elements are numeric UTF-16 code
units in native memory, not encoded bytes, C `wchar_t`, Unicode scalar values or a
serialized endianness. Every unit is preserved, including zero and unpaired
surrogates. There is no normalization, validation of surrogate pairing or appended
terminator. Counts always mean code units, not bytes or displayed characters.

`string_create` copies `length` units before returning. For zero length, `units`
may be null and the result is a nonnull empty string handle. No interning or
identity relationship with managed string literals is promised. The host may
release/reuse the source buffer after return. The returned handle follows the
ordinary retain/release rules and may be passed to compatible exported methods.
Passing a null managed string still uses handle zero through `invoke`; import
never creates that null value.

`string_copy` requires a live nonzero string handle. Zero/stale/foreign-instance
handles return `INVALID_HANDLE`; a live non-string handle returns `TYPE_MISMATCH`.
A null `units` pointer is permitted only with zero capacity. On success, it writes
exactly the string's code units and stores their count in `*length`, leaving spare
buffer capacity untouched. With insufficient capacity it returns
`CILICA_V3_BUFFER_TOO_SMALL` (12), writes the required count in `*length`, and writes
no text. This is the only failure that modifies an output. Other failures leave
both outputs untouched.

To query the length, pass null/zero for the buffer/capacity. Empty strings return
`OK` and zero; nonempty strings return `BUFFER_TOO_SMALL` and their length. Allocate
that many UInt16 elements and call again. Strings are immutable, so their required
length remains stable while the lease remains live. The caller owns the copied
buffer even after releasing the handle or destroying the runtime.

All pointer arguments must address appropriately aligned readable/writable storage
of the declared extent for the duration of a call. Keep output fields and buffers
separate and non-overlapping. Source buffers must remain stable during import;
output buffers must not be accessed concurrently while the runtime writes them.
Neither function retains host pointers. Copies require no managed allocation or
execution arena. They do not create new object leases.

Both calls validate pointer shape before runtime admission. Import additionally
rejects lengths above Int32.MaxValue with `LIMIT` before admission or input reads.
It then reserves a handle slot before attempting managed allocation. Handle quota
or token exhaustion returns `LIMIT`. Failure to allocate the string within the
heap/object budget, or native allocation failure, returns `ALLOCATION_FAILED`.
The existing allocator can collect and retry; cancellation releases the reserved
slot on failure. The output handle stays unchanged on every failed import.

Import/copy share the same guard as invoke/retain/release/destroy. `BUSY` leaves
outputs unchanged. Hosts must keep the string lease until copying finishes and
handle/retry concurrent calls explicitly. This extension does not add a separate
buffer-release API or expose an internal managed pointer.

### Exact Integer64 transfer (7c.3)

Use `EmbeddingValueKind.Integer64` in an export shape for a logical Int64 parameter
or result. It must have `ReferenceTypeId = 0`. The manifest spells the kind
`integer64`; it participates in the existing contract hash. A module advertises
`CILICA_V3_CAP_INTEGER64` (512) if any export uses this shape, independently of the
UTF-16 feature. Base plus wide capabilities total 767; adding UTF-16 totals 1023.
Internal wide operations alone do not advertise wide host transfer.

The value tag is `CILICA_V3_INTEGER64` (3), `reserved` must be zero, and all 64 bits
of `payload` are significant. No bit pattern is excluded. The compiler represents
both managed `long` and `ulong` with this storage type: the manifest does not
separately certify declaration signedness. Application contracts decide how hosts
interpret the bits; managed operations preserve their signed/unsigned behavior.
To send a signed C Int64 value, cast it to UInt64 (defined modulo 2^64). On the
tested target, recover signed result bits with `memcpy` into Int64; in C++20 use
`std::bit_cast<int64_t>`. Unsigned values use the payload directly. Preserve all
bits in future JavaScript adapters using an exact representation such as BigInt;
do not route wide values through a floating-point number.

Width tags must match exactly, even when a numeric value would fit both widths.
There is no implicit widening/truncation or handle-to-integer conversion. Invalid
tags/reserved fields return `INVALID_ARGUMENT` before managed effects and leave
outputs unchanged. Successful results use kind 3 with cleared fault fields;
execution failure has the existing zero-value/copied-fault behavior. Checked
arithmetic and division retain managed fault ordering. Wide values need no handle
slots but remain subject to the execution budget. Structured/borrowed values still
cannot be disguised as wide scalars after lowering.

### Retained exception capture (7c.4)

Set `EmbeddingObjectContract.CaptureExceptions = true` when using `EmitExceptions`.
The validated closure must include intrinsic String support; the ordinary object
emitter rejects this option. It adds `captureExceptions: true` to the hashed
contract and `CILICA_V3_CAP_EXCEPTION_CAPTURE` (1024) to capabilities. It is independent
of the optional string-import capability and does not require a public String alias.
Disabled contracts retain their original hashes and declarations.

Enabled headers add two functions, using the module's usual prefix:

```c
uint32_t cilica_example_abi_v3_invoke_capture(
    uint64_t runtime, uint32_t export_id,
    const cilica_v3_value* arguments, uint32_t count,
    cilica_v3_result* result, uint64_t* exception);
uint32_t cilica_example_abi_v3_exception_copy(
    uint64_t runtime, uint64_t exception,
    uint16_t* type_name, uint32_t type_capacity,
    uint16_t* message, uint32_t message_capacity,
    cilica_v3_exception_info* info);
```

The existing `invoke` remains available and does not reserve an exception lease.
Use it when only the copied code/origin is needed. Capturing invocation requires
both output pointers and uses the same export, count, runtime and argument checks.
After argument validation it reserves one handle slot before running managed code.
If no slot is available, it returns `LIMIT`, leaves both outputs unchanged and
executes no managed code. Native handle-table allocation failure similarly returns
`ALLOCATION_FAILED`. Reference-returning exports share this single reservation
between their result and an exception; two slots are not required.

On success, `*exception` is zero and an unused reservation is released. A successful
reference result uses the reserved slot for its normal result handle. Caught
exceptions do not escape as handles. On `EXECUTION_FAILED`, the existing result
copies the fault code/origin and zeroes its value. If a valid escaping managed
exception object is available, `*exception` receives a new strong lease to it.
Fatal faults such as execution-budget exhaustion, or failure to materialize a
managed exception, instead set it to zero. Capture does not allocate a replacement
exception or change the original fault. Other return statuses leave both outputs
unchanged. Output fields and argument buffers must not overlap.

A captured handle obeys ordinary retain/release rules: it belongs to this runtime
and module, survives later calls/collection, and must be released (or destroyed
with the runtime). It can be cloned independently. It does not contain the throw
origin; keep the copied `cilica_v3_result` alongside it. The same managed exception
object can be thrown more than once, so object identity is not an event identifier.

`exception_copy` accepts a live handle to a supported managed exception. Zero,
released or foreign-instance tokens return `INVALID_HANDLE`; a live non-exception
returns `TYPE_MISMATCH`. `info` is required. Each text pointer may be null only when
its corresponding capacity is zero. Buffers and `info` must be aligned, writable,
non-overlapping host storage and remain valid for the call. Capacities and lengths
are counts of numeric UInt16 code units. No normalization or terminator is added;
embedded zero and unpaired surrogates are preserved.

`cilica_v3_exception_info` contains, in order, UInt32 `type_name_length`,
`message_length`, `type_token`, followed by `char type_image_hash[65]`. This describes
the exception's source TypeDef declaration in the compiled closure, not its throw
site or a host CLR reflection object. The hash is copied and null-terminated. Use
the emitted native C layout, not packed records or serialized padding.

Pass null/zero for both text buffers to query sizes. If either buffer is too small,
the call returns `BUFFER_TOO_SMALL`, fills `info`, and leaves **both** text buffers
untouched. Allocate the required capacities and retry the copy with the same live
lease. On success it copies both complete texts, leaves spare capacity untouched
and fills `info`. Neither query nor retry reexecutes the failed export. The copied
buffers and metadata remain valid after releasing the handle or destroying the
runtime.

Message formatting uses the existing bounded native exception formatter, including
supported default, supplied, parameter and contextual messages. It may allocate
and collect within the existing heap/object budgets. The incoming exception lease
roots its payloads throughout formatting; the final copy has no intervening managed
allocation. Allocation failure returns `ALLOCATION_FAILED`; another formatter or
internal consistency failure returns `EXECUTION_FAILED`. Both leave `info` and
text buffers unchanged. These are failures of detail retrieval, not replacements
for the already copied invocation fault. Retain that original report even when no
message can be copied. Querying may allocate or populate runtime message caches;
it is not guaranteed allocation-free.

Both functions use the existing admission guard. Overlap with invoke, copy,
retain/release or destruction on the same runtime returns `BUSY`; hosts serialize
or retry accordingly. This profile copies type and message, not stack traces or
an inner-exception graph. Its evidence is local macOS ARM64, with no phone-device
compatibility claim.

### Bounded numeric records (7c.5)

`EmbeddingObjectContract.Records` optionally declares up to 32
`EmbeddingRecordBinding(Id, Name, Type, Fields)` entries. Each field is an
`EmbeddingRecordField(Id, Name, Path, Kind)`: `Path` is the exact sequence of
`ObjectFieldId` values leading to an Int32 or Integer64 scalar leaf. Include every
leaf exactly once. A record must have 1..64 leaves; empty and reference-bearing
records are rejected. The record type, nested declaring types and all fields
along each path must be externally visible/public. IDs are nonzero; record
IDs/names/types are unique within the module, and field IDs/names/paths are unique
within their record. Every record declaration must be used by an export.

Use `new EmbeddingValueShape(EmbeddingValueKind.Record, RecordTypeId: id)` for a
by-value parameter or result. Its reference ID must be zero; non-record shapes
must have record ID zero. Logical types must match before lowering, and expanded
native signatures must also match afterwards. Only `EmitExceptions` supplies
this validated value-lowering path; capture itself may remain disabled. Public
`ref`/`out` signatures remain unsupported.

The canonical `contract.records` lists declarations by record ID and fields by
field ID, with stable names and numeric shapes. Actual types and metadata paths
appear in separate `recordBindings` outside the fingerprint. Changes to stable
IDs, names or kinds change the hash; metadata layout alone does not. The
application owns the semantic meaning of each ID. Modules with records advertise
`CILICA_V3_CAP_RECORDS` (2048) and emit record ID macros; Integer64 leaves also
enable capability 512. Modules without records keep their earlier contract hashes
and symbols. Existing C layouts do not change.

Record modules additionally export:

```c
uint32_t prefix_invoke_records(
    uint64_t runtime, uint32_t export_id,
    const cilica_v3_value* arguments, uint32_t count,
    cilica_v3_value* fields, uint32_t capacity,
    cilica_v3_result* result, uint64_t* exception);
```

Replace `prefix` with the generated symbol prefix. Build `arguments` in logical
parameter order: each scalar or handle occupies one element; each record expands
to its scalar fields in ascending field-ID order. `count` is the expanded element
count, not the logical parameter count. Record inputs have no Record tag, ID or
native struct padding; their expected schema comes from the export signature.
All numeric tag/reserved/bit-width rules apply to each leaf. Input fields are
copied into execution storage; the host retains ownership and may reuse the
buffer after the call.

For a record result, `capacity` is the number of tagged elements available in
`fields`. The required size is the declared field count. On success the adapter
writes that many fields in ascending field-ID order and sets `result.value` to
`{ CILICA_V3_RECORD, 0, record_id }`; extra elements remain untouched. A scalar,
handle or void result uses the ordinary `result.value` and writes no fields, so
`fields = NULL, capacity = 0` is valid. This function supports all module exports.
Ordinary `invoke` and `invoke_capture` reject exports having any record parameter
or result with `INVALID_ARGUMENT`.

`result` must be nonnull. Nonzero `count` requires `arguments`; nonzero `capacity`
requires `fields`. Supply valid, suitably aligned buffers whose advertised sizes
are accessible for the call, and keep outputs disjoint from inputs and one
another. Host memory is never retained. An undersized field buffer returns
`BUFFER_TOO_SMALL` before managed execution and leaves all outputs untouched;
read the required count from the manifest instead of rerunning an export to
discover it. Capacity is checked before runtime admission and input tag checking.
Other admission/input failures also leave outputs untouched.

Passing a nonnull `exception` requests capture and requires the optional capture
feature; otherwise the call returns `INVALID_ARGUMENT`. Passing null avoids a
capture reservation. Enabled capture follows the same preflight reservation,
lease ownership and fatal-fault rules as `invoke_capture`. No successful numeric
record result consumes a handle. On `EXECUTION_FAILED`, fields remain untouched
and the ordinary fault result plus requested exception output are written.
Managed effects already performed are not rolled back. Do not retry execution
to retrieve details; inspect the captured exception instead.

Result scratch and expanded arguments consume the existing execution-byte quota.
Return scratch is held by an additional internal frame/borrow scope through the
managed call and cleaned up on success or failure. Allocation/borrow failures use
the existing copied execution fault codes. No raw host borrow enters managed
code and no allocation occurs while publishing successful numeric fields.
The 64-leaf bound has local O0/O2 ASan/UBSan evidence under the existing 2 KiB
native-frame warning gate. Reference-bearing records and phone platforms remain
unverified/unsupported by this slice.

### Persistent session composition (7c.6)

A host can keep application state behind one declared reference alias, return
numeric observations through `invoke_records` and obtain immutable text snapshots
through ordinary handle returns plus `string_copy`. Release imported string
leases after an export has stored the reference; the session's managed field then
keeps it alive. Clone a session lease when independent host owners need it. A
retained immutable text snapshot remains independent of subsequent session edits.
Copied numeric/UTF-16 buffers remain valid after releasing handles or destroying
the runtime. Serialize all calls on each instance as usual.

A scripted real-kernel adapter exercises this pattern with four exports (open,
advance, read and canonical text), matching the milestone 6 .NET transcript through
separate C11/shared-library consumers. Public `EmbeddingSessionTests` additionally
composes wide record fields and captured exception recovery. The
[7c.6 outcome](milestones/07-embedding-and-phone-libraries.md#slice-7c6-outcome)
records exact local scope and counts. This is evidence for existing operations;
it adds no ABI capability or general command adapter and makes no phone claim.

### Apple static package (7d.1)

[The Apple builder](../tools/Apple/README.md) consumes the generated v3 source,
header and manifest and emits an arm64 device/simulator XCFramework. The original
C ABI and fingerprint are unchanged. Each library is static: its registry belongs
to the host image where it is linked; module tokens must remain within that image.
Use the platform's matching slice and link libc++ (the Clang module map requests
it for Swift clients). Package receipts record exact input/output/toolchain hashes.

Swift imports functions and layouts from the C module. Numeric macros expressed
through UINT32_C/UINT64_C are exposed by `cilica_apple.h` as typed constants named
`<Module>_<OriginalMacro>`. Inline `cilica_<name>_apple_default_config()` and
`cilica_<name>_apple_contract_hash()` helpers avoid duplicating config defaults or
hash strings in Swift. These helpers own no state and add no ABI capability.
Keep existing host serialization, handle lifetime and buffer ownership rules.
The [Swift smoke](../examples/Apple/README.md) validates import/link/lifecycle;
it is not a full application or retained-handle wrapper. Device cross-linking and
simulator execution remain separate evidence; physical-device validation is open.
