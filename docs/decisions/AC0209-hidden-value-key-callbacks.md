# ADR AC0209: Checked callbacks for hidden value keys

Recorded 2026-10-08 for AC8.65 after AC3.36.

## Problem and design

The frozen engine uses internal WaveGroupId values in HashSet. The selected core
already chooses their public GetHashCode override and IEquatable<T>.Equals method,
but applying ordinary cross-assembly member access rejects the hidden owner. A public
key works; internal/private keys and explicit IEquatable implementations reproduce
the failure in reduced fixtures. Direct same-assembly hash calls already work.

Keep ordinary member access unchanged. After exact ClosedKeyHash/ClosedKeyEqual grant
checks, use the existing constrained-target resolver to authenticate the public
contract, exact receiver, override/interface implementation and MethodImpl selection.
Only when ordinary access fails, retain a ValueKeyCallContract on the call expression.
The contract contains its closed public contract, original value-instance binding
and explicit-mapping flag. Logical and scalar IR validation require the selected core
caller, exact original/lowered binding, owner visibility and lexical facts, hash override
or closed IEquatable<T> relationship, permitted method flags and precise borrowed
receiver/copied argument signature. Scalar validation checks flattened leaf locations.
Application calls cannot claim this privilege. Existing argument, dominance, exception
edge and ownership checks still apply. No generic private-access relaxation is added.

The existing call emitter and value scalar lowering are reused. No boxing, temporary
receiver copy, new runtime operation or owned-core body is introduced. This preserves
the managed collection's decision to borrow its hash key while copying equality
receivers, including mutations, reference-bearing values, allocation and faults.
Profiles: CIL97, object IR60, native79 unchanged. CIL96 is reserved for the separately
ongoing array-copy work. Production resource limits are unchanged.

## Evidence and review

Initial d37c2ad prototype: 16 checks pass in 37.702s. Merge onto committed Array.Clear
6d868aa before final validation. The expanded 64-check run has 45 passes and 19 failures
from one new private nested generic constructor fixture failing before collection
callbacks; the shared translation batch propagates that same failure. Preserve that
independent boundary as a negative, and exercise closed generic callbacks with an
internal generic value key. Translation validates all supported probes in 1.469s.
The final callback class passes all 23 checks in 39.061s. Together with the 42 passing
unchanged constrained-access, reference-key and Array.Clear controls, 65 distinct
checks pass. No whole-suite claim is made.

Eleven positive probes x49 pairs x3 repeats xO0/O2 give 3,234 native/.NET comparisons.
ASan/UBSan, forced collection before allocations, exact root/borrow cleanup, exact
and one-short frame budgets, 2 KiB native frame warnings and bounded subprocesses
remain enabled. Cases cover public/internal/private/explicit keys, dictionary lookup,
closed generic keys, mutating hash and equality receivers, reference fields, callback
allocations and hash/equality faults. Both IR stages reject missing/forged contracts,
wrong declarations/visibility/flags, wrong invocation/argument shape and absent fault
edges. Same-assembly application calls reject a forged core-only contract. Both exact
grants are still required. Unimplemented key text and private generic construction
remain separate diagnostics; existing foreign-private call rejection passes.

Review checked exact metadata provenance, no global access-policy changes, original
binding preservation during flattening, reference rooting across callbacks, mutation
and fault ordering, and unchanged runtime emission. All 94 pinned upstream source
hashes and six frozen input hashes verify. Local main 48ecf14 remains an ancestor.
Evidence lives in artifacts/alchemy-value-key-callback{,-merged,-refresh,-engine};
the reduced pre-fix audit is artifacts/alchemy-hidden-key-audit.

## Frozen closure follow-up

Frozen engine discovery passes WaveGroupId hashing and reaches an instruction-decoding
limit in ComparisonSort<Int64>.Swap, through SwapIfGreater/IntroSort. The unchanged
diagnostic instruction allowance is 196,608; no production allowance is raised.
The scan charges 1,394,159 metadata work units in 5,233ms. StateLookup still translates
15 methods, 11,616 units in 498ms. No full-engine native emission or execution occurs.
Core 76679328e233017b36a27078e2a7d4d6e5e70ee9b2784be99aa169d906f16e6c.
Manifest 87ec152387c1d90a4e9b68e392b3884825a5989f09cc3b31c7c3698b5e64f903.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.
