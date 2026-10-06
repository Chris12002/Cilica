# ADR AC0050: Inline-value delegate results

Accepted 2026-10-07 for bounded AC2.4.

The frozen engine's Select callback returns SpellId. Build on ordinary value
returns and AC2.3 argument expansion rather than introducing a second native
struct layout or returning pointers into callee frames.

Discovery accepts named and closed non-enum inline result layouts. The logical
catalog and call target retain their exact nominal signature, including layouts
used only by a delegate result. Scalar lowering appends writable locations for
all result leaves and changes the scalar return to void. Calls allocate typed
caller-owned slots before invocation, reuse existing rooted location handling,
and read them only on the successful path. Nested Int64 leaves retain all bits.
Empty structs have no slots but the call itself is retained. Scalar validation
reconstructs the exact signature from the preserved logical catalog. Individual
and aggregate parameter bounds include result slots and reserve the receiver.

Eleven public probes cover static/captured targets, argument/result round trips,
nested and nullable values, references across allocation, empty values, null
receivers, target exceptions and repeated calls. They pass 3,234 repeated native
comparisons against .NET at O0/O2, ASan/UBSan, forced collection and exact cleanup.
Detached IR rejects unknown/incorrect result identities, missing/wrong/read-only
slots, invalid fault exits and an expanded 4,100-leaf return exceeding the existing
parameter bound. Adjacent delegate tests and the entire public compatibility
baseline pass (31 native controls, 16 diagnostic gaps). Evidence is retained under
artifacts/alchemy-value-result-delegates, including an initial test build failure
from a switch-pattern variable name collision and the corrected successful runs.

Review checked rooting before target entry, exception paths before slot loads,
exact nominal validation before erasure, wide-field preservation, empty-call
side effects and unchanged frame/resource limits. No native allocation/copy
algorithm was added. Native v49, object IR v22, object layout v20, integer-object
CIL v12 and delegate value/scalar IR v4 change; interface plan and exception IR
remain unchanged. Direct wide/narrow/enum scalar results, byrefs, multicast and
variance remain unsupported. Core source/mappings/grants are unchanged.

This is a prerequisite for Select, not full engine admission or replay evidence.
The most recent frozen probe still requires Select in ReleaseFinished IL_0049,
then Distinct; state discovery remains at 257/256 identities.
