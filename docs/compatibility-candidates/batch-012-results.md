# Batch012: ArraySegment views and actual CircularBuffer gate

Implemented ArraySegment<T> constructors, Array/Offset/Count, normal copied value
storage and the existing boxed Count path. Tests preserve null/range order, fault
message/parameter hashes, default/empty distinctions, backing identity and scalar,
reference/value, narrow/wide arrays. Other members remain explicit diagnostics.
No compiler/runtime/grant/quota changes; CIL99/IR60/native79.

**Final 48/48 focused checks passed in 87.252s.** This includes the new segment tests,
the original independent candidate reduction and adjacent Copy/Clear regressions.
Nine new segment roots plus the original SegmentView reduction provide 2,940 repeated
native/.NET comparisons. Four forced-GC tests prove empty segments retain complete
backing arrays and payloads across copies until the final copy is cleared. Native
checks use O0/O2, ASan/UBSan and existing frame/root checks. Full suite/replay not run.

Initial 14 failures correctly rejected a mismatched Count dispatch declaration. Required
interface metadata was restored without relaxing binding checks. The next run passed
15/16: its Count-negative expectation was wrong because existing boxed dispatch works.
Count now has positive native coverage; unimplemented Add supplies the negative.
The final negative test also independently reproduces the actual constructor-chain
gate at IL_0008. All initial logs are retained; no remaining owned selected-test failure.

[Evidence](batch-012-evidence.json) retains source/assembly hashes, full test logs and
native source/executable hash receipts before cleanup. See the [final log](batch-012/final-tests.log)
and [design review](../decisions/compatibility-014-array-segment-views.md).

Separately, the directly user-approved [CircularBuffer trial](circularbuffer-execution-20261008.md)
built its one unchanged pinned source, then failed CIL2001 during translation before
oracle/native execution. The run has zero semantic comparisons and no generated C++.
Its container, input image and verified staging were removed; pins, Unlicense, hashes,
raw evidence and owned regressions remain. No retry is active. The original 48-scenario
adapter remains unexecuted. The [manifest](batch-012.json) separates this compiler gate
from semantic failures and directs future work to constructor-state support, then a
new frozen/approved trial. Broader segment and candidate iterator APIs are unverified.

Parent coordination was delayed: direct send attempts to its supplied thread ID
returned thread-not-found. The actual human approval in this lane was “Ok use Circular
buffer”, following the linked frozen proposal; no peer message was used as execution
authority. Review lane was informed of the exact attempt and failure. No production
performance changes were made, and no timing window was active during owned builds.

Profile correction: the retained tested compiler binary embeds CIL99; the earlier
CIL98 label missed the concurrent peer update. Original logs and hashes are unchanged.
