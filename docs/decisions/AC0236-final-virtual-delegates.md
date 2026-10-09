# ADR AC0236: Exact final virtual delegate binding

Recorded 2026-10-09 for AC8.92 after 74bbcf7 (AC8.91).

Frozen ForcedMovementOperation.Run (0x06000cb1) duplicates a HashSet receiver at
IL_0354, takes Contains with ldvirtftn at IL_0355 and constructs a delegate at
IL_035b. Contains is a final virtual interface implementation. The existing
single-target delegate representation can bind that exact method safely.

Converted Alchemy.Engine__4.cpp 15239-15243 shows the receiver/method pair captured
before Enumerable.Where, while its virtual-call helper at 8-17 resolves ordinary
virtual calls from receiver/slot data. SHA256:
`1b9ffafe2c693fc15fe8545fc8e397d1096140e11b18d60e9fbb816aca48debb`.
These are analogous reference patterns; this converted capture does not contain
the frozen ForcedMovementOperation.Run site and its assemblies differ. The
application and Unity runtime code remain unchanged and independent of Cilica.

## Decision

Admit only adjacent dup/ldvirtftn/newobj inside one block, with identical receiver
value identity and an exact final virtual class-instance declaration. Retain the
lookup offset in NewDelegate so both IR stages validate its exact six-byte
adjacency and final-method facts. Declaration-level scalar identity checks cover
the new construction pattern; logical/scalar packet checks remain exact. No method
pointer can escape, and no runtime virtual-dispatch table is introduced.

The captured receiver remains rooted by the existing delegate object and call
packet machinery. Hidden derived methods cannot replace the selected final slot.
Overridable and interface operands remain diagnostic, including a base-slot
reference even if the concrete object's override is sealed. That broader case
needs explicit receiver proof/target selection rather than guessed binding.

## Null semantics and review correction

The first implementation added a null lookup check and returned NullReferenceException.
The .NET 10.0.11 oracle instead returned ArgumentException for the final-method
pattern. [CoreCLR v10.0.0 importer.cpp](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/jit/importer.cpp#L8719)
corroborates the distinction: its final-method path folds ldvirtftn to direct
ldftn while preserving receiver side effects. Thus the delegate constructor, not
a virtual lookup, rejects null. Cilica now uses its existing constructor check,
which runs before delegate allocation. General virtual lookup semantics are not
inferred from this bounded final-target case.

Reference file SHA256:
`8a80afcd5fbc6dfd7f2322036fe34148a45710c556ee27199251768bf3c739de`.
The official source is corroboration from v10.0.0, not a claim of identical runtime
source to the measured 10.0.11 binary. No implementation was copied from it.

CIL116/object IR69/native85 identify the admission/IR contract. The native delegate
algorithm, core library, intrinsic grants, layouts and production budgets do not
change. Review checked final-slot identity, capture evaluation, exact signature
checks, null-before-allocation behavior, both validators and legacy rejection.
No remaining findings within this scope.

## Validation and inventory

61 distinct selected tests pass. The 54-test focused/adjacent run passes in
61.946s (FinalDelegate, DelegateTranslation, FrameworkDelegate,
NativeDelegateTranslation and ValueDelegate). The final 17-test focused run passes
in 20.730s after adding a value-type HashSet key and five malformed-IL cases.
Seven new roots add 2,058 native/.NET comparisons (49 pairs x three repeats x
O0/O2), with ASan/UBSan, forced collection, exact root/borrow cleanup and frame
budget checks. Probes cover HashSet/List capture, LINQ composition, receiver
mutation, hidden methods, reference-bearing allocations, inline Guid arguments
and null/argument fault precedence. Both IR stages reject stripped final/lookup
facts and forged offsets. Raw tests reject missing dup, intervening IL, branch
entries, static and overridable targets. No full suite or device run.
Receipts: artifacts/alchemy-final-delegates.

The rebuilt diagnostic inventory clears the sole ldvirtftn body failure:
15,247/15,254 bodies translate individually. Seven remain: six different-slot
joins and one derived-address join. Discovery charges 3,839,228 metadata units in
64,359 ms. Successful bodies total 61,569 blocks, 293,286 statements and 1,014
exception regions. StateLookup translates 15 methods in 325 ms (11,759 units).
No full IR program or native emission is returned; production aggregate capacity,
full native closure and saved-game replay remain incomplete. Six frozen input
hashes remain pinned.
Core SHA256: `9c18b9882eab4b53a1fc1b174dcfae1d29ccafa5a86b5b7f159e0aea7e6e4b86`.
Manifest SHA256: `8e95e8f55891137b93a7fbbc0b6795b4f1f1cc348b03543c7d990f50d3e05c8f`.
Receipts: artifacts/alchemy-final-delegate-inventory.
