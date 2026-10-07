# ADR AC0149: Single-candidate interface variance dispatch

Recorded 2026-10-07 for AC8.42.

## Evidence and decision

The ef598aa frozen engine probe reaches List<DomainEvent>.CopyEnumerable with
an OfTypeIterator<RoundStartedEvent> receiver after 466,593 metadata units.
Type assignability admits IEnumerable<RoundStartedEvent> as IEnumerable<DomainEvent>,
but exact interface target resolution rejects the requested owner. StateLookup
admits 15 methods after 11,511 units. No engine emission/execution occurred.

Keep exact metadata target resolution as the authority. Enumerate the receiver's
nominal declared interfaces through class inheritance and interface parents.
If the requested closed interface is present, use its existing exact mapping.
Otherwise require exactly one compatible specialization of the same generic
definition. Bind the same open member on that selected declaration, then resolve
its exact implementation and effective virtual slot through the existing resolver.
A name/count overload ambiguity stays diagnostic; no metadata-order selection.

Retain the selected declaration on the target. Detached validation checks the
same definition/member identity, owner specialization, visibility, attributes,
nominal candidate uniqueness and mapping signature. Requested reference arguments
must transfer to selected parameters, and selected reference results to requested
results. Int32/Int64/void packets are unchanged. Inline values, borrowed locations,
generic variant methods, multiple candidates and boxed variant receivers remain
outside this slice. Arrays keep their separately validated helper recipes.

Integer CIL v66, object IR v43, interface plan v15 and native v69 record the
admission and retained evidence. Native dispatch already carries reference packets;
no new native cast, allocation, root lifetime or library grant is introduced.
Production/diagnostic budgets and 2,048-byte native frames remain unchanged.

## Validation and review

Seven new probes cover covariance, contravariance, inherited override dispatch,
explicit implementations, exact precedence, null receivers and the frozen-shaped
OfType<Derived> to List<Base> conversion. A formerly negative String-to-Object
producer probe is promoted with native evidence. Eight probes provide 2,352
repeated comparisons at O0/O2 with ASan/UBSan and forced collection. Generic
variant members and ambiguous candidates stay diagnostic. Seven mutated detached
plans are rejected both before and after scalar lowering; valid plans round-trip.

The focused run initially passed 126/128 checks: the promoted negative control
needed updating, and an existing value-plan serialization test needed to omit
absent optional enum constants. The new detached tests also needed the existing
abstract storage serialization tags. These are test serialization fixes, not
relaxed validator rules. Both corrected checks and 13 additional closed/boxed
checks pass, for 152 distinct focused checks overall. Evidence is retained under
artifacts/alchemy-interface-variance/{tests1,regressions,regressions2}.log.

Review checked exact precedence, nominal versus variance-derived ancestry,
inherited/private/virtual selection, per-target parameter accounting, malformed
facts, null timing, argument/result direction, scalar lowering and unchanged
native ownership. No frozen input or game rule changed. Full engine, unchanged
fingerprint execution and saved-game replay remain unverified.

All four public-baseline checks also pass: 156 distinct checks overall, 46 public
native controls and one retained diagnostic gap. Evidence: baseline.log. All six
frozen input hashes were verified. Local main remains ancestor 48ecf14.
