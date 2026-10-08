# ADR AC0211: Narrow nongeneric enumeration and interface audit follow-up

Recorded 2026-10-08 for AC7a.8 and diagnostic follow-up AC8.67.

## Design

Frozen interface planning reaches Enumerable.Count -> OfTypeIterator.Count, whose
nongeneric IEnumerable.GetEnumerator must cover admitted narrow arrays in the closure.
The old blanket element-operation gate rejected Boolean/Byte/Char receivers. Reuse
ArrayInterface<T>.EnumerateObjects and ObjectArrayEnumerator<T>, including their exact
primitive boxing, state checks, fresh cursors/boxes and managed ownership. Admit that
single additional recipe alongside existing element-independent metadata operations.
The shared recipe predicate, discovery and detached-plan validator agree. Nongeneric
mappings retain the actual receiver element identity; exact narrow generic contracts
and signed-alias boundaries are unchanged. No core body, intrinsic grant, native
operation or GC/ownership implementation changes. CIL99/interface-plan18 record the
new admitted shape; object IR60/native79 remain unchanged.

## Validation and review

Eight positive probes cover Boolean/Byte/Char data, byte and character truncation,
isolated UTF-16 surrogates, fresh boxes on repeated Current reads, exact box type,
mutations visible through the live cursor, reset/end/before-start faults, empty and
null sources, mixed narrow receivers, and reference-result OfType.Count. Together
8 x49 pairs x3 repeats xO0/O2 give 2,352 new native/.NET comparisons. ASan/UBSan,
forced collection before allocation, exact root/borrow cleanup, exact and one-short
frame budgets, 2 KiB frame warnings and bounded native subprocesses remain enabled.
The exact enumeration grant is required; both IR stages reject substitution of a
Char mapping for a Byte receiver. Existing metadata-only, generic narrow-operation,
signed-contract and nongeneric cursor/boxing controls remain intact.

The first translation audit exposed a separate OfType<byte> narrow-memory requirement.
Preserve it as a negative alongside signed/UInt16 array construction, and exercise
reference-result query composition for this slice. All 36 focused checks pass on
6b804fe plus the isolated change (57.092s). After merging committed Array.Copy 87fb7e5
into a fresh isolated snapshot, the same 36 checks pass in 55.940s. These are 36 distinct
checks, not 72. No full-suite claim. Evidence: artifacts/alchemy-narrow-enumeration
and artifacts/alchemy-narrow-enumeration-merged.

Review checked exact narrow boxing identity, array-view rejection, cursor/root lifetime
across allocating Current calls, live mutation visibility, fresh-box identity, exception
order, preserved generic/signed boundaries and unchanged runtime emission. All 94
pinned upstream and six frozen assembly hashes verify; no application source or input
assembly is changed. Local main 48ecf14 remains an ancestor. The Array.Copy lane's
committed changes and subsequent documentation-only proposal are preserved.

## AC8.67 diagnostic follow-up

At the existing 2,097,152 metadata allowance the enumeration fix advances interface
planning until another metadata exhaustion (2,097,270 aggregate instrumented units,
including 118 setup units across separate ledgers; engine7,259ms/StateLookup457ms).
Increase only copied diagnostic metadata ceilings/defaults to 4,194,304, retaining
262,144 instructions and all other previous diagnostic allowances. This reveals a
semantic interface mismatch: boxed List<T>.Enumerator is accepted by variance but
its exact interface resolver is asked for IEnumerator<Base>.Current without selecting
its declared IEnumerator<Derived> contract. The frozen route is PhysicalDamagePayload
construction -> List<PhysicalDamage>.CopyEnumerable from environmental damage entries.
A diagnostic-only receiver annotation confirms the boxed cursor and get_Current.
No claim is made that all later interface/dispatch passes are complete.

The earlier pre-Array.Copy probe charges 2,187,872 aggregate metadata units in7,547ms;
StateLookup15methods/11,616units/444ms. The final combined probe charges 2,189,364 in
7,532ms; StateLookup15methods/11,616units/459ms. Core
bb90c5bc200ea0868b736424616921d033d190079dc444933d369f4b5acd3409;
manifest e03539fef6a9f0d2f2fbf4fd5d8079d890dd336498a880ee4d2a4571d9266b1f.
Evidence: artifacts/alchemy-narrow-enumeration{,-final}-refresh,
artifacts/alchemy-narrow-enumeration{,-final}-engine and artifacts/alchemy-metadata-4194304.
Production instruction/metadata limits remain65,536, with other production limits
unchanged. No full-engine native emission/execution, fingerprint parity or replay.
