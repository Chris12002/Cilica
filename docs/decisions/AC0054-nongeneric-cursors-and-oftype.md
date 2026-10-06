# ADR AC0054: Nongeneric vector cursors and bounded OfType

Accepted 2026-10-07 for AC4.7 and AC6.8, following AC1b.5.

ZombieSiege.AdvanceRound filters ICollection<DomainEvent> with
OfType<RoundStartedEvent>. Add the pinned lazy OfType algorithm and its required
nongeneric cursor foundation. Arrays may be candidates in the same conservative
closure even when the immediate source is a custom class, so their nongeneric
factory is an independently checked prerequisite.

ArrayInterface<T>.EnumerateObjects requires the new exact
ArrayInterfaceEnumerateObjects grant. Selection preserves the actual vector
specialization and requires the selected System.Collections.IEnumerator result.
Logical/scalar validators independently check the receiver, arity, name, result
and helper marker. The managed ObjectArrayEnumerator<T> follows the pinned index,
Current and Reset algorithm. It returns fresh boxes on each value read and original
references for reference elements; empty arrays get fresh cursors. It implements
neither IDisposable nor generic enumeration. ICloneable is not mapped. Generic
array cursors and owned Where/Select/Distinct/OfType reference-class cursors now
provide nongeneric Current through ordinary boxing/copy rules. No boxed collection
struct factory has been replaced with a reference-class implementation.

OfType validates source eagerly, retains the typed shortcut for non-nullable value
results, acquires nongeneric enumeration lazily, tests each object, excludes null,
and conditionally invokes IDisposable. Current, clone/reuse and failure ordering
match the pinned iterator. Count/materialization/first queries traverse the raw
source without consuming the OfType cursor. Managed buffers replace private
pooled/span storage. A new private KeyOperations<T>.IsValueType helper requires the
exact ClosedTypeIsValueType grant, selected core caller and Boolean/no-parameter
internal-call declaration. Compilation derives its result from the closed type;
no input code runs and no general Type.IsValueType reflection API is added.

Projection behavior matters: upstream may fuse reference-result OfType over a
generic object source, using generic Current and preserving its original cursor.
Value and nullable results use the ordinary OfType cursor. The owned traversal
path represents this distinction. Reference-result OfType.Select currently fails
closed because its closure requires variance-only interface dispatch, which is
not implemented. Both generic-object and nongeneric reference-projection probes
pin that diagnostic. Nullable and named-value projection pass native comparisons.
Public List factories remain diagnostic. These boundaries are explicit rather
than silently changing callback or cursor behavior.

Fourteen OfType probes pass 4,116 repeated native/.NET comparisons, covering
subtypes, scalars, nullable and reference-bearing value results, typed identity,
null/empty cases, cursor reuse, terminal state, optional disposal and get/move/
Current/dispose failures. Twelve direct cursor probes pass 3,528 comparisons,
covering fresh boxing, copied wide/reference fields, nullable elements, covariance,
retained references, exact cursor faults, Reset and owned-query Current before
iteration/after disposal. All use O0/O2, ASan/UBSan, forced GC, exact cleanup and
fixed frame bounds. The public compatibility baseline remains 31 native controls
and 16 gaps. Grant removal rejects both capabilities; an external type-kind call
fails at the earlier lexical-access gate. Both-stage detached array packets reject
wrong result/receiver/arity/name/marker facts. Adjacent box/type-test checks pass.

Initial logs preserve discoveries of missing array/nongeneric Current support,
authored field-access restrictions, the AC1b.5 scalar-box prerequisite and the
existing variance boundary. The external-call test initially expected a later
private-call rejection; it now pins the actual lexical rejection. Corrected targeted
checks pass. All logs and frozen evidence are under artifacts/alchemy-of-type.

Review checked fresh boxing and rooting, no disposal for raw array cursors,
conditional disposal for nongeneric sources, state after callback faults, nominal
helper result identity, private grant authentication, projection boundaries and
unchanged resource limits. Integer-object CIL advances to v13 and interface plan
to v9. Native v49 and object layout v20 remain unchanged. AC1b.5 separately records
boxed value IR v8 and scalar boxed IR v9. Pinned source/provenance is retained.

Frozen engine discovery now needs predicate Single in AdvanceRound IL_0063. State
still reports 257/256 discovered identities. Owned core SHA256:
3f284d2a5925ef9424f6a570abdfdc0c675af3d7f973e7073712f07d0fa33419.
Full engine execution and saved-game replay are not yet validated.
