# ADR AC0057: Declaration-only type visibility

Accepted 2026-10-07 for AC8.11.

AC8.10 shows that source-name access checks share a structural snapshot budget
with layout discovery. Visibility depends on declaration nesting, not instance
fields, interface signatures or initializers. Add ReadTypeAccess to read that
exact evidence and use it only for CheckSourceTypeUses. It conveys no permission
to allocate a type, access its members, classify an enum or execute a method.

ReadTypeAccess and ReadTypeInput share their owner-chain traversal. Both retain
cycle detection, a 64-level depth bound, maximum inherited generic arity and exact
public/nested visibility. Names are formatted only after checking depth. The
resolver owns the input, and each visited row charges the existing caller ledger.
Structural reads retain their prior field/interface/initializer validation and
work accounting. Source checks reuse a structural snapshot if one exists, then
a declaration-only cache holding at most 256 entries. Further identities are
read without retention and charged on each read. There is no larger type limit.

Twenty-nine focused checks pass. A reduced native fixture instantiates an ordinary
identity method with 257 distinct, otherwise-unused generic reference identities
and repeats the last call beyond cache capacity. Its 294 repeated comparisons pass
at O0/O2 with forced GC, sanitizers and fixed frame bounds. Those declarations can
contain unused malformed field signatures because only their names are needed;
a separate structural read still rejects those fields. Nested/public/private facts,
exact row budgets, depth and resolver ownership/disposal are checked. The earlier
static-owner fixture exposed the separate DelegateType structural classification
path; this slice does not change that path or advertise its optimization.

Seventy existing private-generic, detached array evidence, internal/nested enum,
owned-core and public-baseline tests pass. Review checked that private explicit
names still fail, opaque generic transport stays scoped, enum classification still
validates its payload and the new cache is no authorization source. No IR/native
profile changes are needed: emitted representations and access rules are unchanged.

Frozen EngineStep gets past the snapshot-limit failure and reaches CIL3015:
IOrderedEnumerable<T> is unmapped at ReconcileControlOpportunity IL_0079. The
actual pipeline is OrderBy<ParticipantState,Int32>, ThenBy<ParticipantState,String>
with StringComparer.Ordinal, Select to PlayerId, Concat and ToArray. StateLookup
still reaches 257/256 discovered catalog identities. Full native engine execution
and saved-game replay remain unverified. Evidence is in artifacts/alchemy-lexical-reads;
the core hash remains ccb5b124686d6d0e494ae976fb6686d997f4ca2841bf91ab04cb4fe00de69977.
