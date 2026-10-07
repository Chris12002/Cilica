# ADR AC0151: Interface packets and declaration access context

Recorded 2026-10-07 for AC8.44.

## Evidence and decision

After 32513ea, the 786,432-unit diagnostic engine probe finishes in 2,951 ms and
reports an internal enum access failure after 776,942 units. One failure-only
compiler stack trace identifies WaveKind inside a constructed value-array helper
signature. The planner's type conversion callback uses GapProbes.Probe_EngineStep
as its lexical caller, although the signature is already bound metadata from a
selected runtime helper. Frozen inputs remain unchanged. StateLookup still admits
15 methods after 11,511 units.

Convert interface packets structurally without inventing the entry method as a
source caller. Existing source operand/declaration checks remain responsible for
lexical type access; exact closed binding, helper intrinsic authorization and
interface signature validation remain responsible for the selected packet.
This does not grant arbitrary access to hidden names in foreign IL.

The reduced cross-assembly Count probe fails before this fix and passes afterward.
A CopyTo probe then reveals the same false caller context while converting the
called method's local array. Establish the declaring method's Work identity first,
then convert its parameters, result and locals using that closed/lexical context.
Source declarations are still checked before any discovered method is queued;
explicit operand tokens retain their actual body caller. The temporary context
has no executable body or independently admitted packet.

Integer CIL v67 records this admission correction. Core, intrinsic grants,
interface plan v15, object IR v43, layout v26 and native v69 are unchanged.
Production and diagnostic capacities remain unchanged.

## Validation and review

A separately authored foreign entry assembly calls public fixture methods whose
bodies own hidden enum tuple arrays. Four probes cover internal enum Count and
CopyTo, private nested enum indexed reads, and CopyTo fault behavior. They supply
1,176 repeated native/.NET comparisons at O0/O2 with forced GC and ASan/UBSan.
The oracle invokes the owning assembly's authored methods; no input assembly runs
inside the compiler. The pre-fix reduced Count failure is retained in red.log.

A foreign entry that first calls the authorized body and then explicitly names
the hidden enum is rejected. Existing opaque versus explicit generic enum/value
access, lexical calls and malformed lexical facts remain covered. All 65 access
checks plus 136 value/array/boxed/generic/delegate/interface checks pass, including
older translation profiles. Evidence: artifacts/alchemy-hidden-enum-dispatch-audit/
{access-tests,regressions}.log. The intermediate CopyTo failure remains in tests.log.

Review checked source provenance versus bound transport, initialization of the
method's temporary signature context, opaque generic arguments, nested visibility,
selected helper grants, retained layout/packet checks, null/fault order and native
root ownership. No user code or game rule changed. Full frozen engine, unchanged
fingerprint execution and saved-game replay remain unverified.

All four public-baseline checks also pass: 205 distinct checks overall, with
46 native controls and one retained diagnostic gap. Evidence: baseline.log.
Local main remains ancestor 48ecf14.
