# ADR AC0020: Concrete dictionary view enumeration

Accepted 2026-10-06 for bounded AC4.4.

## Decision

Map the exact three-level nested Enumerator types under Dictionary<TKey,TValue>
KeyCollection and ValueCollection. Implement concrete GetEnumerator/MoveNext/
Current/Dispose with the pinned slot walk, version check and end-state reset.
Use existing internal dictionary accessors and independent current payloads of
TKey or TValue. Reusing the pair enumerator would retain an extra current value
or key after dictionary removal/clear, violating the intended ownership behavior.
The separate small managed loops mirror the pinned library's separate types.
No new compiler grants, generic private access or runtime operations are needed.

The current dictionary reference retains the collection during iteration. Current
is a copy, so struct mutation does not change its stored payload, while reference
identity is preserved. New insertions invalidate; remove/clear/overwrite and failed
TryAdd do not. Dispose is a no-op. Reset bodies preserve state/version semantics
but boxed interface calls/factories and nongeneric Current remain outside this
slice. The earlier nested-arity fix admits these three-level generic types while
retaining full lexical owner facts.

## Evidence and review

Eight authored probes supply 2,352 repeated native/.NET comparisons on macOS ARM64
at O0/O2 with ASan/UBSan, forced GC, frame budgets and cleanup checks. Coverage
includes foreach order after reuse, key/value synchronization, removal/clear,
overwrite/duplicate/add behavior, copies, default/ended state, disposal, reference
and inline values, and allocation after clearing the dictionary.

Four native GC checks keep an enumerator in a rooted holder. Eight objects start
alive. Clearing releases the entry and value for key iteration (six remain), but
releases only the entry for value iteration (seven remain). Ending value iteration
releases its cached current (six); dropping the holder releases the remaining
cycle (only the emergency exception remains). All 35 view/baseline regressions
pass, including extended exact factory mappings and the unknown-equality boundary.
Review checked selected payload ownership, struct copies, index advancement,
version/fault order and exact three-level type identities. Native v41 and interface
plan v5 remain unchanged. The ledger remains 22 gaps and 25 controls; this slice
fills additional concrete declaration gaps without claiming interface enumeration
or private saved-game execution.
