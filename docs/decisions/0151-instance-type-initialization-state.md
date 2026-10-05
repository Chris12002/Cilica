# ADR 0151: Instance-owned type initialization state

Status: Accepted for authored integer-object IR; CIL discovery remains pending

## Decision

Introduce a dedicated InitializeType statement and a TypeInitializerName binding
assertion. The initializer has an exact private static, parameterless void signature
with constructor flags. Bound names and distinct closed owners identify at most
256 initializers. Ordinary calls and delegate captures cannot invoke these bodies.
Readonly static stores are admitted only within the exact owning initializer.
The statement participates in reachability, fault routing, lowering and call
safepoints. Intrinsic exception shells cannot declare initializer bodies.

Each runtime instance owns a lazily allocated, quota-charged initialization table.
A slot is unstarted, running, succeeded, managed-failed or fatally failed. Running
initializers can reenter their own initialization check without executing again.
Completion is permanent within the instance and separate instances remain isolated.

Before user IL runs, reserve and root the type name and fully formatted exception
wrapper. A preparation failure leaves the initializer unstarted and may be retried;
no user side effect has occurred. After execution starts, a managed failure is
materialized, attached as the wrapper's original inner exception and cached without
further wrapper allocation. Repeated checks raise that same wrapper. If materializing
a fault exhausts memory, the existing emergency out-of-memory object supplies the
inner exception. Fatal ownership/depth/execution-storage failures remain fatal and
are retained, preventing partial initializer effects from being repeated.

The collector traces active/prepared and cached wrappers, validates slot state and
accounts for the table separately from managed allocations. Success releases the
unused wrapper. The table and managed objects are disposed with the instance.
Callbacks during tracing/allocation retain their existing no-reentry contract.

Typed reference IR v10, object operations IR v11 and native v32 carry the change.
This slice does not insert CIL triggers or admit initialized metadata types. That
separate frontend work must implement BeforeFieldInit timing, strict method/field
triggers and closed identities before C# initialization is advertised.

## Evidence and review

Eleven focused checks validate native O0/O2 reentry, repeated calls, instance
isolation, permanent wrapper/inner identity, collection, preparation quota failures
and retry, materialization exhaustion, fatal caching and corrupt pointers/states.
Detached cases reject invalid declarations, ordinary calls and wrong targets;
readonly stores require the exact owner. ASan/UBSan and the 2 KiB frame limit pass.

The full solution builds without warnings. A 24-test composed run passes; all
11 focused checks pass after review added slot-state invariants and the readonly
ownership regression (25 distinct checks). Review covered every allocating step,
root publication, exception provenance, table accounting and unreachable bodies.
