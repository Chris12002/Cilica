# ADR AC0130: Selected disposed-object exceptions

Recorded 2026-10-07 for AC8.34, a prerequisite for the AC7c hashing lifecycle.

Admit the exact selected ObjectDisposedException(String) and (String, String)
constructors, ObjectName getter and Message behavior. Require the selected
InvalidOperationException base and independent layout/constructor/getter grants.
Null custom messages use the pinned disposed-object resource. Null names read
as canonical empty strings; nonempty names append the pinned newline/name suffix.
Preserve exact UTF-16, retained input identity, fresh named message identity,
ordinary typed catches and rethrow. Inner constructors, custom descendants,
serialization and other disposed-object APIs remain diagnostic.

Give NewException an explicit ObjectName operand and add ExceptionObjectName IR.
Both logical and scalar validators enforce the exact resource/type identity,
exclusive payload, string operands and fault edges. Include the operand in
liveness, box-use proof, roots and catalog use. Native storage reuses the existing
traced name slot: ParamName for argument exceptions and ObjectName for the exact
disposed exception, with disjoint identities checked before access. No object
layout change is needed. Resource validation now permits exactly the nine known
resource kinds; a combined Format/KeyNotFound/ObjectDisposed probe covers this
addition beside the six baseline resources. Discovery budgets are unchanged.

Integer CIL v51, managed/scalar exception IR v8 and native v66 record the new
semantics; object IR v38/layout v24 stay unchanged. New exact capabilities are
ObjectDisposedConstructor and ExceptionObjectName. The explicit derived Message
operand keeps its ordinary ExceptionMessage grant; raw metadata tests it because
C# usually references the original Exception virtual slot.

Six probes supply 36,864 repeated O0/O2 native/.NET comparisons across null/empty
and arbitrary UTF-16 names/messages, all output code units, identity, exception
ancestry, rethrow, null receivers and combined resources. Seven detached mutations
cover resource name/parent/identity and payload/fault edges in both IR stages.
Five grant removals fail; the positive raw getter and inner/derived boundaries are
checked. Two native GC runs check exact name lifetime, repeated formatting,
corrupted pointers, wrong getter identity and allocation failure both before the
base resource exists and while appending the name. Failures preserve pending
origin; dropping objects leaves only the emergency exception and cached resource.

All 89 selected checks pass: 14 focused, 15 box-confinement and 60 existing
exception/public-baseline regressions. ASan/UBSan, forced collection, frame-quota
recovery, the 2,048-byte frame warning and 30-second subprocess limits remain.
The native test harness is split into bounded helper frames; no limit was lifted.
Public baseline: 43 controls/four gaps. Main 48ecf14 remains an ancestor.

Review checked independent IR validation, all operand/root visitors, the shared
physical slot's exclusive identities, formatting roots and failure provenance.
The framework SHA256 path is still pending; full engine/replay is unfinished.

Evidence: artifacts/alchemy-disposed-exception, including oracle constructor and
Message IL, tests-final.log, tests-regressions.log and hashes.json.
Core: b16dd42de6a91b379049be696b2f75aac3c362d48469260b68adf49be6199566.
Manifest: 7d2b0a93e0569888dee3471ba12c5a5b76cc70a97d02a25464119c0e25184ef5.
Fixture: 599d7ec1e3f69bee05ba74ca04b2a4dabb504746313d8df4b68f25cf3674ce64.
