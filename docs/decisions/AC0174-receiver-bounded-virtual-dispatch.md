# ADR AC0174: Receiver-bounded virtual dispatch

Recorded 2026-10-08 for AC8.54.

The frozen Dictionary<EffectiveEffectNode,...> uses inherited object identity
hashing. Its constrained reference call selects Object.GetHashCode, whose old
owner-wide discovery includes unrelated comparer overrides. A reduced identity-key
dictionary plus ordinal comparer reproduces that exact unsupported-body failure.
Retargeting to an override cannot solve a class that inherits the original body.

Retain proven ordinary static receiver classes on class dispatch maps. The existing
same-block zero-argument proof supplies one source; validated constrained reference
operands supply another, including calls with arguments. Resolve canonical slots
as before, but retain the receiver bound even if the implementation is inherited.
Bounds union across calls sharing a declaration; any unproven call makes that
map unrestricted. Discovery only adds facts, revisits the growing catalog and
includes every valid subclass implementation. Interfaces and unproven Object
aliases keep their existing restrictions. Final-call/null behavior is unchanged.

ObjectVirtualDispatch now carries ReceiverBounds. Empty means the original
owner-wide contract. Nonempty bounds require the integer-object profile, distinct
compatible ordinary catalog classes, and bounded aggregate storage. Every typed
virtual call must be assignable to a declared bound (or null), and every concrete
catalog class within those bounds must appear exactly once in the map. Existing
nearest-slot, final-override, signature and fault validation remain. Scalar lowering
preserves the bounds. Native emission uses the same validated target tables; no
native ownership, rooting or dispatch algorithm changes are needed.

Six new native probes cover inherited identity hashes/equality, distinct receiver
bound unions, base/derived key overrides, null receivers and void calls with
allocation. They provide 1,764 repeated O0/O2 native/.NET comparisons under forced
GC and ASan/UBSan. Eight detached corruptions check missing/default/duplicate/array
bounds, uncovered calls, missing targets, unjustified widening and wrong overrides;
a separate Object-alias control remains diagnostic. All 88 distinct selected checks
pass after correcting a fixture that initially used an unsupported nominal generic
constraint. Existing constrained-reference, known-receiver, old object dispatch,
virtual value/borrow and reference-key tests pass, together with the 46-control/
one-gap public baseline.

Review checked monotonic bound union/unrestricted fallback, closed identity,
argument-evaluation-before-constrained-load, call-site validation before native
consumption, exact coverage, canonical override choice and bounded storage. CIL v75,
object IR v48 and native v74 record the extended accepted dispatch contract.
Layout v26, interface plan v16, grants and all production/native/diagnostic limits
remain unchanged.

Evidence: artifacts/alchemy-dispatch-receivers{,-engine}. The diagnostic copy reuses
the exact AC7b.19 core/manifest. Frozen discovery advances to the missing two-generic
SelectMany(source, selector) declaration in ConstructionInvitations, at 955,482
charged metadata units in 3,905 ms. StateLookup remains 15 methods/11,529 units
(454 ms). Exact caller IL is archived. No full engine/fingerprint/replay execution
or general application compatibility is claimed.
