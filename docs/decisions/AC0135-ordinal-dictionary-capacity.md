# ADR AC0135: Capacity plus proven ordinal dictionary construction

Recorded 2026-10-07 for AC3.23.

After AC5a.4 (31844db), frozen discovery reaches Dictionary<string,
RegisteredDeliveryModifier>(Int32, StringComparer.Ordinal) in
DeliveryModifiers.Register IL_0008 through its initializer IL_006d after 324,617
metadata units. StateLookup remains eight methods/9,923 units. Evidence:
artifacts/alchemy-invariant-int32-parse-engine. This is discovery, not execution.

Extend the existing checked ordinal dictionary factory with an exact two-argument
constructor/helper pair and a separate exact DictionaryOrdinalConstruction grant.
The first argument must be Int32 capacity; the last is the existing directly proven
selected StringComparer.Ordinal getter. Only this authenticated ordinal path can
use two factory arguments. Validate and pop each argument against its own type.
Retain capacity expression evaluation before comparer evaluation, then invoke the
ordinary owned Dictionary(Int32) constructor. Negative capacities preserve existing
ArgumentOutOfRangeException parameter/message behavior; allocation, prime sizing,
publication and rooting remain the existing managed implementation.

Null, aliased, custom, joined, lookalike and non-string comparers remain diagnostic.
Comparer exposure and dictionary copy construction still need separate identity
work. Existing HashSet behavior is unchanged. Integer CIL v55 records the admission;
object IR v41, native v67, layout v25 and all resource limits remain unchanged.

Six differential probes cover UTF-16 keys, ordering, duplicate/null key faults,
retained key/value identity, reference-bearing struct values, iterator versions,
negative capacity messages and capacity-expression faults. The latter has exact
zero/one static-root assertions: a throwing capacity expression never evaluates
the comparer getter. Each factory overload is independently denied by removing
its grant while retaining the other. Existing ordinal dictionary/set and capacity
allocation-recovery checks protect the shared paths.

Review covered overload identity, parameter order, grant separation, getter proof,
managed exception order, precise roots and unchanged native emission. Main 48ecf14
remains an ancestor. Frozen application inputs and rules are unchanged. Full engine
execution, unchanged fingerprint execution and saved-game replay remain open.

All 37 distinct selected checks pass: 21 existing ordinal/capacity regressions,
11 new checks and five public-baseline checks. Six probes supply 1,764 repeated
O0/O2 native/.NET comparisons with forced GC, ASan/UBSan and exact frame quotas.
The initial test assumed a getter cache even when the first argument threw;
path-specific exact root expectations corrected that test. Evidence:
artifacts/alchemy-ordinal-dictionary-capacity/tests.log and tests-fixed.log.
