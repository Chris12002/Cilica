# ADR AC0017: Concrete list enumeration

Accepted 2026-10-06 for bounded AC4.1.

## Decision

Implement List<T>.GetEnumerator and its exact public nested Enumerator value
type, including the IEnumerator<T>/IEnumerator/IDisposable nominal contracts.
Use a captured list reference, version, index and current value. MoveNext checks
the version before inspecting size, captures one current value, and resets current
and sets index to -1 on completion. Dispose is a no-op. Generic Current returns
the stored value even before/after iteration or after invalidation. The existing
list mutations already update version; capacity changes and failed removal do not.

Ordinary list Count/index and an internal version accessor keep generic lexical
private-field access within its existing gate. No callbacks intervene between
these reads in the single-threaded profile. No native operation, grant or runtime
profile changes. Interface factories and nongeneric Current retain diagnostic
stubs; exact boxed-interface dispatch needs payload receiver lowering first.
Reset has its pinned managed state/version body but is not claimed as executable
through a boxed interface in this slice.

The first probe exposed nested generic arity double counting in both metadata
snapshot readers. Nested declarations repeat outer parameters, so taking the
maximum required count along the owner chain replaces summation. This retains
the existing rejection for an under-declared nested owner and does not relax
visibility, private access, closed-argument matching, constraints, lexical facts,
owner-depth or metadata budgets. Exact List<T>/Enumerator mappings are explicit.

## Evidence and review

Twelve authored probes plus public ListForeach supply 3,822 repeated native/.NET
comparisons on macOS ARM64 at O0/O2, with ASan/UBSan, forced GC, frame and cleanup
checks. Cases include foreach/finally/break, empty/default/ended states, independent
copies, all implemented mutation categories, allowed capacity/failed-removal
changes, stored reference identity, inline-value current copies, allocating loop
bodies retaining the list through the enumerator, and two-argument nested generic
owners with different closed field layouts. Three reduced metadata cases verify
inherited parameter counting for type, method and declaration snapshots.

Eighty-two existing metadata, generic, constrained-target, nested-access and
public-baseline regressions pass. The negative legacy-Current probe hits the
existing boxed-interface dispatch gate before its unsupported member body; its
assertion now pins that earlier diagnostic. An analyzer-required cancellation
token was added to the test read. Review checked version/fault order, retained
current after failed MoveNext, value/reference copying, end reset, disposal and
private-access/arity admission. The ledger has 24 gaps and 23 controls. Dictionary,
array and interface enumerators remain future work; private replay is unverified.
