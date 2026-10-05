# ADR 0160: Budget-owned member name indexes

Status: Accepted without raising metadata work limits

## Decision

A caller-owned MemberResolutionBudget retains complete method and field name
indexes keyed by exact resolver-owned type identity. Building an index charges
one unit for the owner and one for every scanned member, including unrelated
names. Subsequent searches inspect and charge every candidate with the requested
name. The index stores handles only; signatures, constraints, projected library
contracts, closed generic arguments and ambiguity checks still run for each lookup.
Both strict binding and library mapping use this path. Unbudgeted strict calls
retain their previous full scan. Failed partial indexes are never published.

Indexes end with their caller's ledger. A new budget cannot inherit scan work.
Owner and member charges bound index storage even for empty or differently named
owners; no global cache or cross-plan semantic result is retained. The 65,536
metadata-work bound is unchanged.

## Evidence and review

Temporary local budget tracing found repeated full member scans among the largest
costs in the concrete kernel. A focused regression performs 400 searches on owners
with 64 unrelated methods/fields, preserving distinct Int32/Int64 results and fresh
budget rejection. Before this change, repeated scans exceed the focused bound.
129 binding, mapping, generic, dispatch, audit and protected-input checks pass.
Full solution builds cleanly. Review checked resolver-owned keys, complete-scan
publication, bounded storage, all overload candidates and unchanged signature/
attribute/constraint checking. Temporary instrumentation was removed.

The unchanged kernel now passes the previous metadata gate and reaches the missing
String.Empty field. Native transcript equivalence and filter closure remain pending.
