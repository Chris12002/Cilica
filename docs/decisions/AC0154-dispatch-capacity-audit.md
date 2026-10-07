# ADR AC0154: Diagnostic metadata and dispatch-method capacity

Recorded 2026-10-07 for diagnostic-only AC8.46.

After 1090e51 clears narrow array Count, the copied metadata budget exhausts at
786,428 charged units against 786,432 (the next declaration needs more). Increase
only the copied metadata allowance to 1,048,576. It reaches the combined method
limit after 817,418 units in 3,660 ms. Add diagnostic counts without changing limits:
4,246/4,096 distinct combined identities, with 2,997 discovered bodies, four
abstract methods, 273 interface declarations, 1,017 mapping facts and 38 exception
declarations. These categories overlap; they must not be added as distinct bodies.
Other partial-discovery counts are 1,207 type roots, 72,751 instructions, 5,870
parameters and 3,239 locals. They are not emitted native layout/frame counts.

Raise only the copied method allowance to 6,144. The unchanged frozen engine then
reaches missing HashSet<string>(IEnumerable<string>, IEqualityComparer<string>)
mapping at HandLimitHandler.Resolve IL_00dd, with a direct StringComparer.Ordinal
getter immediately before the constructor. It charges 820,241 units in 3,477 ms.
StateLookup still admits 15 methods after 11,511 units. The original frozen IL was
read as metadata to identify the constructor; no input was rebuilt or executed.

Evidence: artifacts/alchemy-narrow-metadata-engine, alchemy-metadata-1048576,
alchemy-dispatch-method-capacity-audit and alchemy-dispatch-methods-6144. Each
successful diagnostic records commands, base commit and exact copied-source hashes.
The counter-only audit initially had a Python string quoting error; it was fixed
before any compiler probe ran. No retry followed a test timeout or unexplained
failure.

Review confirms production still has 256 types, 512 methods, 65,536 metadata/
instruction units and 4,096 aggregate parameters/locals. The diagnostic copy now
has 2,048 types, 6,144 methods, 1,048,576 metadata units, 131,072 instructions,
8,192 parameters and 4,096 locals. Its 60-second command cap remains. No native
engine emission/execution, unchanged fingerprint execution or saved-game replay
is established. No source implementation changed in this documentation slice;
no additional conformance tests are warranted. Local main remains ancestor 48ecf14.
