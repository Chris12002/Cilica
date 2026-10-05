# Embedding ABI: persistent Int32 profile v1

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
