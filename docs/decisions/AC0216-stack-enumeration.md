# ADR AC0216: Stack cursors, mutation versions and empty identity

Recorded 2026-10-08 for AC8.72, after AC8.71.

The frozen interface-body scan requests nongeneric Stack enumeration through
OfTypeIterator.Count from ContributeHandCardsModifier.Read. Add reusable Stack
traversal; keep application code and inputs unchanged.

Adapt the pinned System.Collections Stack<T>.Enumerator. A concrete cursor retains
its stack, captured version, next index and copied current value. MoveNext validates
version before reading the current backing array and moves top-to-bottom. Exhaustion
clears Current and sets index -1. Dispose only sets index -1 and retains Current;
Reset validates version, captures Count and clears Current. Both typed and boxed
Current return the stored value without an extra state check. A default cursor's
MoveNext/Reset produces the managed null fault; Current/Dispose are still available.
Use existing copy/borrow and boxing operations, with internal accessors for stack
storage/version and exact nested Enumerator mapping in the core binding plan.

Push increments an unchecked version only after a successful array store (including
successful growth), then updates size. Pop checks emptiness first, then increments
version, updates size and returns/clears the old slot. Extra scalar slot clearing is
still unobservable: later MoveNext/Reset reject mutation and Current already holds
its copy. Nonempty interface factories create boxed cursors; empty factories reuse
existing SZGenericArrayEnumerator<T>, sharing array/list empty identity and mutable
empty Current/Reset behavior. Concrete empty cursors do not use that cache.

No new intrinsic, compiler profile, native operation, ABI or quota. CIL102/object IR62/
interface-plan19/native79 remain unchanged. CopyTo, capacity/enumerable constructors
and other unimplemented Stack members remain diagnostic. Direct cursor mapping is
now included alongside the existing parent Stack mapping.

## Validation and review

All ten new roots validate at both stages (1.959s). Initial 33-check validation passes
31; two mutation comparisons expose a one-character message-length difference between
CoreLib and System.Collections resources. Retain the exact pinned Collections
Strings.resx and use its message: Collection was modified after the enumerator was
instantiated. Strengthen message comparisons to hash all code units and add cursor
quota recovery. Final 35 checks all pass in 76.346s on the isolated 954fdbc snapshot.

Ten new cursor roots and the converted former traversal-negative root provide
11 x 49 pairs x 3 repeats x O0/O2 = 3,234 new native/.NET comparisons. Coverage includes
concrete and boxed copies, Int32/Int64, reference-bearing values across allocation,
nongeneric Current boxing, OfType.Count/iteration, reset/dispose/exhaustion, successful
push/pop invalidation, failed empty-pop stability, empty cursor identity shared with
array/list and default cursor null faults. Existing eight Stack growth/pop probes and
two native reference-release/allocation-failure checks pass unchanged. Two new native
quota checks cap the heap before growth: failed Push leaves Count 8 and the cursor
valid, successful retry reaches Count 9 and invalidates it. All temporary state is
collected with exact permanent-root and borrow/frame cleanup. ASan/UBSan, forced
collection, exact/one-short frame budgets and 2 KiB frame warnings remain enabled.

Review checked value-copy ownership, reference-bearing Current roots, mutation/fault
ordering, unchecked version wrap, empty-cache identity, default/null receiver behavior
and preservation of existing scalar-clearing semantics. All 100 pinned upstream files
and six frozen input hashes verify. Main 48ecf14 remains an ancestor. Later character
trimming work remains separate and is not part of this snapshot's evidence.

## Frozen follow-up

Core 3bfd9b049a315bb035971b1a2250f0f10744a57c6f114662fb7876d9e219b600;
manifest 54a344e7c5d70aeff6c202c31b8a79841e98125809837fda3dc254f55da946c9.
With unchanged diagnostic allowances, discovery advances through Stack traversal,
then exhausts 3,072 cached type snapshots: 939 nominal declarations, 2,232 closed generic
snapshots, 5,953/6,144 field declarations; explicit roots 219 values/2,223 references/
221 vectors. The next request is an ArrayInterface<T>.EnumerateObjects helper.
This is partial discovery, not emitted layouts. The engine charges 2,672,934 metadata
units in 11,555ms; StateLookup remains 15 methods/11,616 units in 507ms. Evidence:
artifacts/alchemy-stack-enumeration{,-refresh,-engine}. Full closure admission,
native game execution, fingerprints and saved-game replay remain unfinished.
