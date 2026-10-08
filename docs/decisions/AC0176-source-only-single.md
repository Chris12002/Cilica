# ADR AC0176: Source-only Single terminals

Recorded 2026-10-08 for AC6.19.

The unchanged ContinueConstructionContribution selects strings and calls Single
without a predicate. Add the source-only Single and SingleOrDefault overloads by
adapting the already pinned Single.cs algorithm. Validate source first. IList<T>
reads Count once: zero returns default/not-found, one reads index zero, and every
other count throws without enumeration. Other sources acquire a cursor, read at
most two MoveNext results and one Current, then dispose before returning or
reporting cardinality. Disposal faults therefore take precedence over those
results. Do not infer ICollection or IReadOnlyList shortcuts. Explicit-default
value overloads remain unmapped.

Nine probes supply 2,424 repeated O0/O2 native/.NET comparisons with forced GC,
ASan/UBSan and exact frame/cleanup bounds. They cover null/empty/single/multiple
arrays, generic cursor acquisition/movement/current/disposal faults, adversarial
IList Count/index behavior (including negative counts), collection-only sources,
read-only wrappers, nullable/reference/reference-bearing values and composed
queries. Full UTF-16 message and parameter-name hashes are part of the oracle.
All 37 selected checks pass, including predicate Single, First and the public
46-control/one-gap baseline. Two obsolete missing-overload assertions were
advanced to positive binding checks; no unrelated diagnostic was weakened.

Review checked Count/index order, found/default handling, two-step enumeration,
using/finally cleanup, exact messages, generic rooting and unchanged profiles,
grants and limits. Main 48ecf14 remains an ancestor.
Evidence: artifacts/alchemy-single-source{,-refresh,-engine}.

Frozen discovery now reaches indexed Select<ShieldComponentState,ChoiceOption>
at ContinueShieldContribution IL_0661: 972,267 charged metadata units in 4,031 ms.
StateLookup remains 15 methods/11,529 units (444 ms). Exact caller IL is archived.
No full engine/fingerprint/replay execution is claimed.
