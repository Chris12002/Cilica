# ADR 0049: Precise reference visitation before collection

Status: Accepted for private native enumeration; marking and reclamation remain planned.
Recorded 2026-10-03.

## Scope and decision

Milestone 3 established retained objects and phase A root publication. Milestone
4 first needs one precise way to enumerate the references a collector will follow.
Native v10 adds two private visitors, without changing allocation behaviour or
introducing marking, sweeping, managed handles or a public embedding API:

- `visit_roots` enumerates the supplied context's published frame slots, from the
  innermost frame outward, then the instance's literal cache. Null slots and aliases
  are included. A cached literal remains a root after managed frames disappear.
- `visit_object_references` enumerates one allocation's managed children: flattened
  inherited reference fields, then dynamic reference-array elements. It visits each
  slot once, including nulls and repeated values. A null source has no children.

Integer fields/arrays, string code units, native execution packets, descriptor
pointers, ownership links and the retained allocation list are not reference roots
or object edges. String literal cache links locate separate roots; they are not
String children. Statics, retained host handles, managed exception objects and
reference-bearing structs are not implemented and add no current root category.

Each borrowed `ReferenceEdge` includes the value and its provenance: a published
frame and slot index, a literal ID, or an owning object with field byte offset or
array index. Offsets select generated typed field accessors; tracing does not
manufacture member pointers with byte arithmetic. The existing flattened field
maps cover base fields once. Reference-array callbacks return actual typed buffer
slots. A String has no child references and requires no payload scan.

## Validation, bounds and callback contract

Before delivering a non-null value, compare it against the instance's allocations
before dereferencing it, verify its owner, and require exact known descriptor
membership. Validate source objects before accessing their maps. Cached roots must
also have the exact String descriptor. Copied/unknown descriptors and foreign or
unreadable managed reference values fail. These checks rely on runtime-owned frame
storage, allocation headers/list topology, canonical descriptors and buffer extents;
this is not validation of arbitrary native memory or corrupted allocation layouts.

Walkers are iterative and allocate no memory. They do not recurse into child
objects; the consumer supplies graph traversal/bookkeeping. Frame walks enforce
the existing 64-frame and 1,024-slot limits and agree with the context depth.
Literal walks cannot exceed the retained object count or 257 entries (256 authored
contents plus implicit empty). Bounds reject cycles in otherwise readable frame
or cache links without unbounded iteration. Array/field work is bounded by existing
validated layouts and allocated buffers.

Callbacks return true to continue or false to stop. `TraceResult` distinguishes
complete, stopped and invalid walks, including a null callback. Stopping or failing
may already have delivered a prefix. An incomplete walk can never authorize a
future sweep. Edge records are borrowed only during callbacks. Callbacks may keep
external bookkeeping, but cannot allocate managed objects, change references or
topology, or re-enter managed execution. Walkers preserve heap counters, execution
storage, published roots and pending fault/origin state on every outcome.

The supplied context is the single active managed context in the current instance
execution model. The visitors do not discover additional independently created
private contexts. Supporting multiple active contexts requires registration and
an explicit collection coordination contract; it cannot be inferred from this API.

## Evidence and next boundary

Native tests cover exact edge identity/provenance, inherited fields, nulls, aliases,
cycles, reachable versus merely retained objects, empty/reference/integer arrays,
string leaves and instance-local literal roots. A test-owned iterative consumer
shows how the two visitors compose without putting marking in the runtime.
Invalid roots/children/descriptors, owner mismatch, early stop, malformed readable
frame/cache chains, and exact/excess frame/slot/literal limits have regressions.

An authored graph probe executes under .NET and native code. At each native
allocation, the observer enumerates real caller/callee frames and follows their
edges, including partially initialized constructors and allocating string calls.
All expected objects are reachable before allocation; after normal or forced
allocation-fault unwinding, only literal-cache roots remain published. Pending
fault origins survive subsequent diagnostic visits unchanged.

O0/O2 use ASan/UBSan, warnings as errors and static frame reports. Review split an
oversized O0 sanitizer test helper into smaller checks while preserving the 2 KiB
baseline; no runtime frame limit was raised. Other targets and actual collection
stress remain untested. Next: bounded marking with cycle/alias handling and failure
semantics, followed by reclamation and collection at allocation safepoints.

Review and validation: the final solution build has zero warnings/errors; all 194
selected local regression checks pass, including native/example regressions and
programs without String catalogs. The final O0/O2 tracing runs pass ASan/UBSan;
maximum measured static frames are 544/432 bytes respectively, below 2 KiB.
Local main remains an ancestor. No push, hosted work or additional agents ran.
