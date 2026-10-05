# ADR 0111: Closed class dispatch in the composed pipeline

Status: Accepted

## Decision

The explicit integer-object CIL profile retains exact class virtual maps through
value/delegate/exception validation and scalar lowering. Discovery adds canonical
slot facts to virtual bodies, repeatedly selects targets for every compatible
catalog class, and inspects newly selected implementations until the closure
stops growing. Class and interface target counts share a bound; closed arguments
remain part of method, receiver and slot identity.

The existing native dispatcher, root assignment and managed fault routing execute
these maps. Direct base calls remain direct; callvirt evaluates arguments before
checking the receiver and selecting its slot implementation. Closed class and
interface calls may introduce each other during discovery.

This slice supports scalar/reference virtual signatures, including wide integers.
Bodies can use the existing value/borrow operations. Struct/borrow virtual
signatures, abstract hierarchies, selected Object runtime methods and class slot
remapping remain diagnostics. Legacy CIL profiles retain their earlier scope.
Detached maps must preserve owner specializations, cover compatible receivers
and select the nearest implementation of the canonical slot.

## Evidence and review

Five C# probes match .NET in 1,470 repeated native O0/O2 ASan/UBSan results with
forced collection, frame-budget boundaries and cleanup checks. They exercise
new slots, overrides, final implicit implementations, direct base calls, wide
values, allocating reference returns, mixed class/interface discovery, distinct
signed/unsigned specializations and caught argument/null fault order. Missing
maps, incorrect bodies and forged slot identities are rejected.

The private kernel probe now reaches String character indexing at IL_02f1.
Full native kernel execution remains pending. Review found no remaining
actionable issue; focused regression results are recorded in the milestone log.
