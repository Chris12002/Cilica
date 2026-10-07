# ADR AC0115: Lexically visible nonpublic enum operands

Recorded 2026-10-07 for AC5b.12.

## Decision

After 4b73f00, frozen discovery reaches ScheduledInvocationState::.ctor IL_000b
through MarkPlayerEnd, after 225,774 metadata units. Its typeof/box/IsDefined
sequence names an internal Int32 enum in the caller's assembly. A reused generic
argument guard rejected this direct operand. StateLookup remains eight methods,
now 9,921 units after the preceding library declaration addition. No engine native
emission/execution. Evidence: artifacts/alchemy-ordinal-compare-{refresh,engine}.

Reuse OperandSignature's enum access proof for direct runtime tokens, scalar
boxing/unboxing, boxed type tests and constrained comparison. A top-level internal
enum requires the calling assembly; private nested enums require the admitted
lexical owner scope. The final value-operand check admits only scalar enum layouts
with a validated visible declaration. General hidden struct and generic argument
rules remain unchanged, including Nullable<hidden enum>. Protected nesting and
closed generic nesting still need separate proofs. No friend-assembly support.

The existing nominal scalar box, type identity, constants and comparison lowering
are unchanged. Access is authenticated during metadata discovery, as with existing
enum memory operands; detached value IR still validates its established nominal
payload and operation contracts. Integer CIL v41 records new source admission.
Native v64/layout v24, grants, owned core and production budgets are unchanged.

## Validation and review

Eight new probes provide 2,352 repeated O0/O2 native/.NET comparisons for internal
and private nested boxing, IsDefined, boxed type tests and constrained comparison.
Raw metadata tests extend same/foreign assembly and owner/nested-helper/unrelated/
protected scope cases through box and runtime-token operands. Both hidden generic
argument fixtures remain negative. The 34 focused checks pass.

All 41 selected Enum.IsDefined, constrained comparison, runtime identity and public
baseline regression checks also pass: 75 checks total. The baseline remains
43 native controls/four gaps. Forced GC, ASan/UBSan, 2,048-byte frame warnings and
30-second subprocess caps remain enabled. Review found no outstanding issue in the shared access proof, final
operand gate, nominal identity, unchanged runtime/rooting/fault behavior and the
separate generic argument boundary. Main 48ecf14 remains an ancestor. Full engine
compilation and saved-game replay remain unfinished.

Evidence: artifacts/alchemy-nonpublic-enum/tests-{a,b}.log and hashes.json.
Core: fbf06d7abd311dbc38fa5c8bb5939533165ef08bbd1c6581165dbddeb0ff1a6c.
Manifest: 73e071c7899332eda25c1b9efc3ff986e4d81e8cfe36b28d1867a4f74c0fd155.
Fixture: 754e0df3b2dff99c9c080896cf2ebc17fc74fdec62b9775b9fe21d6bd3de4008.
