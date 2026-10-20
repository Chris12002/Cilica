# ADR 0225: Defer compact dictionary storage until array lowering is ready

Status: deferred; experimental implementation rejected. Recorded 2026-10-20.

## Decision

Retain the current reference-entry implementation. The experiment replaced
separately allocated dictionary entry objects with a managed storage
owner holding four equal-length arrays: hash, next link, key and value.
Insertion into spare capacity allocates no entry object. Array storage retains
ordinary exact types, bounds checks and precise tracing. The compiler, native
profile, quotas, grants and public embedding ABI did not change.

The candidate fails existing UInt64-key dictionary tests because its TKey[]
requires unsigned-wide array support that is currently diagnosed. That is an
existing dictionary capability regression, not a newly unsupported request.
The roughly 0–2% preliminary DiffPlex timing change does not justify broadening
this slice to new array semantics or retaining a slower fallback unnoticed.
The candidate and reduced tests are preserved as a patch with evidence; they
are not enabled in the working compiler/library.

The candidate is a general owned Dictionary implementation, without workload recognition.
It uses Cilica's existing typed primitive/reference array reads. An inline
Entry-struct array experiment instead generated generic managed-location loads,
lost typed lookup execution and regressed DiffPlex substantially. That experiment
is retained as evidence for improving scalarized array lowering, not accepted.

## Candidate callback and lifetime contract

Find returns captured storage plus an index, the bounded equivalent of the
upstream FindValue result location. It captures storage after hashing; TryInsert
captures it before hashing and captures buckets afterward. Remove hashes first,
then captures buckets and storage. Equality receives a copied key. Reads and
writes after a callback use the captured location even if that callback resized
the dictionary. Resize copies payloads into independent new storage, never
rewrites the old storage's chains, and publishes only after all allocations and
copies succeed.

Removal retains scalar-only key/value bits and clears payloads containing
references. This matches the pinned runtime's observable behavior when a callback
removes the entry being inspected. Clear resets all used slots. Free-list encoding,
prime capacities, slot order and version updates retain the existing contract.
Enumeration and CopyTo read only the columns required by their result, without
materializing temporary whole-entry structs.

The captured owner is an ordinary traced reference. Its arrays and any old
payloads needed after a callback remain rooted across allocation/collection.
No pointer into native storage escapes and no unsafe code, new intrinsic or
weakened ownership check is introduced.

Allocation budgets intentionally change: a storage expansion creates one owner
and four column arrays plus buckets, whereas insertion thereafter creates no
entry. Small dictionaries pay a larger fixed cost; larger ones avoid per-entry
headers and allocations. Reduced allocation count does not by itself establish
faster execution. Keep these tradeoffs separate in reported measurements.

## Validation, review and prerequisites

See [measurements and evidence](../performance/dictionary-compact-20261020.md).
Forty-nine selected candidate checks pass, including callback differentials,
each resize-allocation failure, recovery, precise collection, shared modules,
copying and enumeration. Fresh full DiffPlex outputs pass release and both
sanitizer modes on constrained stacks. Broader dictionary checks expose the
UInt64 regression, so these successes do not establish acceptance.

Before revisiting compact storage:

- Preserve the entire existing dictionary key/value type set. Never alias
  UInt64[] to Int64[] merely because their physical widths agree.
- Lower proven scalarized-array reads without materializing generic borrow
  carriers, retaining null/bounds/order, storage identity and safepoint roots.
- Retain captured-storage callback probes when changing representation; the
  reference-entry implementation is not evidence of these subtle .NET behaviors.
- Recheck small-dictionary fixed costs as well as larger tables and DiffPlex.
- Require a demonstrated execution benefit, rather than allocation counts alone.
