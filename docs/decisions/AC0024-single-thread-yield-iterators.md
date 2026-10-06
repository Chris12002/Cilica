# ADR AC0024: Single managed thread identity and yield iterators

Accepted 2026-10-06 for bounded AC4.6, implementing one part of ADR 0001 stage 1.

## Decision

The selected owned core exposes Environment.CurrentManagedThreadId as positive
identity 1 within its independent runtime instance. Each instance has one managed
thread; serialized host entry on another OS thread does not create another managed
thread. Independent instances have independent identity domains and no shared
managed objects. This is a managed identity, not a host thread ID or a promise to
match the numeric ID chosen by a particular .NET process. A later multithreaded
profile must replace this core implementation with context-owned identities.

The member is an ordinary managed getter. No new intrinsic grants or native
profiles are required. Environment's remaining members and System.Threading.Thread
remain unmapped; this adds no managed threading, synchronization or async support.
The broader proposals in ADR 0001 retain their original planned status.

This lets unchanged Roslyn-generated IEnumerable/IEnumerator state machines use
their captured thread-identity reuse check. The first same-managed-thread call to
GetEnumerator reuses the initial object; later calls clone it. Existing field,
interface and exception lowering executes the generated IL directly, including
suspension, copied captures, disposal and compiler-generated finally helpers.
No compiler substitution or modification of application iterator code is needed.

## Evidence and review

Eight authored probes plus public Iterator supply 2,646 repeated native/.NET
comparisons at O0/O2 on macOS ARM64. The oracle compares positive/stable identity,
not its process-assigned integer. Coverage includes deferred execution, first reuse,
clones and independent positions, Current before/after iteration, unsupported Reset,
repeated disposal, early break, body/finally faults and reference/inline-value yields.
Twenty-four existing core/baseline checks and the negative environment/thread check
pass. Forced GC, ASan/UBSan and existing process/frame budgets remain enabled.

Two native ownership runs create and acquire an iterator on separate serialized
host threads, proving it still reuses itself as the same managed-thread object.
The heap has two objects before first yield (iterator and emergency exception),
three after yielding a reference, two after yielding null and three after the last
reference. Completion/disposal retains the generated Current field. Dropping the
iterator releases it and its payload, leaving the emergency exception.

Review checked the isolation/identity scope against ADR 0001, generated state and
finally behavior, and roots across suspension and allocation. This is bounded
compiler-generated iterator evidence, not arbitrary iterator, cross-managed-thread
or full replay compatibility. The ledger has 19 gaps and 28 controls.
