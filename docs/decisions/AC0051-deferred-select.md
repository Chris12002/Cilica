# ADR AC0051: Deferred Select with owned query traversal

Accepted 2026-10-07 for bounded AC6.6.

The frozen ReleaseFinished closure needs Select<SpellSourceGrantState,SpellId>.
Use AC2.3/AC2.4 single-target inline delegates and ordinary managed query objects.
No application logic, game input, compiler profile or intrinsic grant changes.

The owned iterator adapts pinned .NET 10.0.11 Select and Select.SpeedOpt. It keeps
array and reference-enumerator paths, deferred acquisition, first same-thread
reuse, cloned subsequent enumerators, cached Current, Reset rejection and exact
fault/disposal order. Empty arrays return the canonical result-element empty array
only after source/selector null checks. Private upstream class identity/layout is
not part of the bounded contract.

Upstream fuses Where/Select chains using generic virtual methods. Owned
IQueryTraversal<T> provides fresh traversal with an optional terminal mode instead.
This preserves original query state and per-item selector order without admitting
generic virtual dispatch. Where clones its own cursor while retaining its raw
source behavior. Select propagates terminal traversal through preceding Select
objects, preserving indexed IList behavior even through multiple projections.
A separate nongeneric cheap-count capability skips selectors for array/IList Any.
Count always invokes selectors. IList terminals read Count once and then index;
ordinary IList iteration uses GetEnumerator. No intermediate materialized sequence
reorders callbacks. Managed buffers replace private pooled/span scratch storage.

Eighteen authored probes pass 5,292 repeated native/.NET comparisons at O0/O2,
ASan/UBSan, forced GC, exact cleanup and existing frame bounds. Coverage includes
cursor identity/cloning, unstarted/active prior queries, mixed Where/Select chains,
callback ordering, mutation, custom IList enumeration versus terminal indexing,
collection Count avoidance on ordinary enumerable projections, empty/null order,
nullable/nested/wide/reference-bearing values, reference lifetime, Reset and
resuming after selector failures. Indexed Select and boxed List enumeration stay
diagnostic. Forty-two adjacent LINQ, core and public-baseline tests pass.

Initial local failures exposed existing bounded compiler requirements: array
length needs its Int32 carrier helper and struct-return field assignment needs an
explicit materialized copy. The managed adaptation uses those existing paths.
An authored failure sentinel initially collided with Int32.MinValue input; the
fixture now explicitly disables that sentinel. Failed and corrected logs are kept
with the frozen refresh under artifacts/alchemy-select.

Review checked source acquisition timing, selector/read/disposal ordering,
Current clearing, traversal identity, Count/indexer effects, caller-rooted result
copies and unchanged limits. Conservative closure admission still rejects boxed
collection factories; no substitute reference-class enumerator was introduced.
Pinned originals and adaptation notes are recorded in library/provenance.json.

The unchanged frozen engine now reaches missing Distinct in ReleaseFinished at
IL_004e. State retains the 257/256 discovered-identity diagnostic. Core SHA256 is
e04cf05d68c65a15eff369d2ad4faf67066d0f1e8c6735ed0cbd80b0b94f68e5.
This is partial compiler discovery, not complete native engine or replay evidence.
