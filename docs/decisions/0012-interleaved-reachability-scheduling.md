# ADR 0012: Interleave ordinary discovery and dispatch

Status: accepted for slice 1c. Recorded 2026-09-30.

## Context

The previous walker drained the ordinary dependency queue before considering
receiver/call-site pairs. Broad library discovery could exhaust the node budget
without considering any implementation targets. This occurred in both a small
compiled primitive probe and the local application audit. Increasing limits would
hide the scheduling problem and spend more resources without guaranteeing useful
dispatch evidence.

## Decision

Alternate one ordinary node expansion and one receiver/call-site pair, as long
as either has ready work and no global limit has stopped analysis. Ordinary nodes
retain FIFO order and roots retain sorted, deduplicated selection. Ready dispatch
sites rotate through a FIFO queue, considering one receiver per turn. New sites
join its tail. This gives both classes of work turns rather than waiting for one
to reach a fixed point first.

Keep an append-only list of successfully included type-node IDs. Each unsealed
site has a cursor into that list; after consuming existing receivers it waits
until another type is included. New receivers wake waiting sites once. Sealed
constraints check their one exact named receiver once and never enter the waiting
list. A located binding failure still consumes its pair and preserves its issue;
other pairs remain eligible. Stop only when neither queue has work or a global
limit stops the analysis. Waiting sites alone do not imply missing work: all
currently included receivers have already been considered.

This replaces full graph snapshots and the visited-pair set. Scheduler storage is
linear in included types and sites, with at most one ready/waiting entry per site.
It does not materialize the cross product. Each cursor advances once per pair,
and a waiting site must consume new pair work before waiting again. Receiver
registration happens only after its incoming edge succeeds, so a node rolled back
at the edge limit cannot become receiver evidence.

Keep existing node, edge, method-instance, byte, pair and metadata-work budgets.
One node expansion and one pair remain atomic scheduling units. A large body/type
or costly ancestry walk can still exhaust a limit within its turn. Initial root
selection can also exhaust the node budget. This is bounded fair scheduling of
ready work, not a promise that every target or body fits in a selected budget.
Dispatch edges establish inclusion; they do not by themselves prove body scanning
finished before a later stop.

## Evidence contract

The supported binding/receiver rules are unchanged. Unlimited successful closure
still requires revisiting old sites for new receivers and new sites for old
receivers. Fixed-point and late-discovery regressions remain applicable. Node IDs,
first reasons, edge ordering and partial results can change with the schedule;
advance the analysis profile to `explicit-metadata-closure-v4`. JSON format
version 1 and existing fields are unchanged. Identical inputs, root sets and
limits must still produce deterministic reports.

A successful target cannot clear unrelated missing-body, runtime-array, dynamic
or budget gaps. The filter gate still requires the trial ported-library closure.
No input assembly is executed and no hosted tests are enabled.

## Validation and review

Reduced graphs establish three observable properties: an implementation body is
scanned before an unrelated ordinary chain consumes the node budget; an ordinary
body is scanned while dispatch pairs remain; and a later exact site gets a turn
before an earlier broad site consumes the pair budget. Incomplete reports retain
valid first-reason chains and repeat deterministically. Existing regressions cover
late receiver discovery, dispatch-discovered sites, abstract body promotion,
sealed one-pair bounds, malformed relationships and reference-only bodies.

The authored .NET primitive probe now independently identifies the concrete
`Int32.CompareTo(int)` target, with the expected image hash and constraint evidence,
while retaining its broader closure limit. All 308 local tests pass. The application
audit, using unchanged image hashes and limits, records 196 dispatch edges to 24
distinct target nodes before the node limit, compared with none in v3. Three
type-test filters and no general observation still leave the filter gate unresolved.

Review checked queue membership, cursor advancement after failures, receiver-node
rollback, exact constraints, global limits, deterministic ordering and metadata
reader ownership. Runtime execution semantics and array synthesis remain separate
work. Draining all pairs after each ordinary node was rejected because it can
starve ordinary bodies; prebuilding every pair would unnecessarily enlarge memory.
