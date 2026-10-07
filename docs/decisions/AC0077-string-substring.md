# ADR AC0077: Managed String bodies and bounded Substring

Accepted 2026-10-07 for AC7b.8.

The audited Substring gap needs the one- and two-index overloads. Their ordinary
owned IL preserves the pinned String.Manipulation.cs null/empty/full-range
shortcuts and overflow-safe range checks. Nonempty partial results copy UTF-16
through a rooted Int32 buffer and the existing FromCodeUnits helper. Embedded
NULs and unpaired surrogates remain code units, without transcoding. Negative
argument errors preserve actual Int32 values and use the existing invariant
numeric-text policy; ambient-culture numeric error formatting is not implemented.
The temporary buffer is an explicit bounded allocation cost of this adaptation.

A reduced probe exposed that selected String bodies previously admitted static
calls only. Discovery now allows ordinary managed nonvirtual String instance
bodies through normal IL/access/receiver/fault validation. Private helpers are
still checked against their declaring scope. Both logical and scalar validators
require the exact selected String receiver and reject virtual slots, constructor
bindings and type initializers. Runtime-owned String fields remain inaccessible
through ordinary object fields; existing helpers retain exact signatures/grants.
No new intrinsic capability, runtime storage or quota is introduced.

Six authored probes pass 4,356 repeated native/.NET comparisons: both overloads,
full/empty identity, distinct partial copies, UTF-16, null receivers, signed-limit
indexes, argument precedence, message/parameter content and repeated allocation.
A raw CLI call regression adds 294 comparisons against an independently emitted
.NET oracle. A direct call with null receiver and startIndex zero returns through
the body shortcut; callvirt still checks null before entry. Three binding mutations
are rejected at both IR stages. All 86 selected checks pass, including adjacent
string suites and all public expectations (ac7b8d-tests.log, 95.227 seconds).
Native O0/O2 use forced GC, ASan/UBSan, a 2,048-byte frame bound and unchanged
30-second subprocess caps. No hosted tests were run.

Review checked access gates, nonvirtual flags, direct versus virtual null checks,
unsigned range arithmetic, exact fault order, identity, copy offsets and rooting.
Integer-object CIL v21, object IR v29 and native v55 record the admitted bodies;
physical layout stays v21. The public baseline is now 33 native controls and 14
diagnostic gaps. Pinned provenance records the adaptation.

Frozen inputs are unchanged. Engine admission still stops at UnitRoster.WithViews
IL_000d with 256 snapshots; state still reaches 257/256 catalog identities.
Full engine execution and saved-game replay remain unverified.
Evidence: artifacts/alchemy-substring/ac7b8c-*, ac7b8d-*, engine-and-state.json.
Core: 75fe2d4a3c4d7f676fe9bfdd70f18796fc5b3e58f2c281775e56feec962de3d2.
Manifest: 3246291f8343feb33518dfdfa4795140ddedda374c72ba40657827088ba84357.
