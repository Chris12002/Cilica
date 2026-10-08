# Compatibility batch 007 results

Implemented UInt32 vectors in the verified `codex/alchemy-replay-baseline` worktree.
Review milestone 6 was contacted before implementation and at final review. Its
Skip, provenance and source-list edits remain separate. Compiler profiles now use
CIL85/IR55/layout28/interface17/native77; no runtime grants or limits changed.

**211 distinct checks pass; zero remaining failures.** The new slice has 51 checks.
Twenty authored C# probes, three raw-IL loads and the 4096-entry CRC reduction
supply **4,602 repeated native/.NET comparisons**. Promoting the old unsigned-array
enumeration rejection adds 294 comparisons: **4,896 total new/enabled comparisons**.
Native O0/O2 runs retain ASan/UBSan, forced collection, exact root/borrow cleanup
and the 2 KiB frame cap. Adjacent Boolean/Int64/enum arrays, static data, array
contracts/enumerators, closed generics, managed exceptions and GC checks pass.

The broad gate initially had one obsolete rejection (UInt32 enumeration is now
supported). Its replacement native control and intrinsic-denial check pass in the
18-check enumeration gate. Final review reduced and fixed a genuine new-path
bug: literal-null ldelem.u4 lacked its fallback descriptor. The final 104-check
focused/array-contract gate passes. The union of final outcomes is 211; this is
not a claim that the whole repository suite was run after every edit.

Support preserves full storage bits, nominal runtime type, signed/unsigned/enum
array compatibility, requested generic boxing identity versus actual-element
nongeneric boxing, ordinary/generic loads/stores, copying, search, static data,
Empty identity, heap roots and null/bounds/allocation/evaluation order. Detached
models, both IR stages and interface packets reject malformed substitutions.
Scalar element addresses remain explicitly unsupported, including compound stores.

Pinned Crc32.NET remains **source-mapped, blocked, and unexecuted**. UInt32[4096]
allocation/lookup/logical shift now passes its independent reduction. The next
source-derived index pattern (`int + uint`) emits `conv.ovf.i`, independently
confirmed as CIL2001. Checked native-integer indexing needs a separate width and
overflow design. HashAlgorithm owner metadata and the complete singleton closure
still need re-mapping; no whole-candidate compatibility is claimed.

DiffPlex's selected source closure remains mapped with no known feature gate from
its existing screen, but real compilation/execution awaits approval and isolation.
The [execution approval plan](diffplex-execution-plan.md) specifies the pin, 14
unchanged source files, owned recipe, commands and proposed enforced quotas.
Neither the adapter nor actual candidate has been compiled/run. Full upstream
builders/renderers/three-way diff/test suite are outside that selected-root plan.

No third-party project, target, generator, test or binary ran. Immutable online
screening from batch006 was reused at the same commits. No candidate copies were
created, so cleanup had nothing to remove; notices and pinned evidence remain.
The [manifest](batch-007.json) records next actions; [evidence](batch-007-evidence.json)
retains source/binary/log hashes and final test identities. Validation used isolated
outputs against shared working sources; captured source hashes describe the final
capture, while concurrent library changes were preserved. Logs remain under
`artifacts/compatibility-batch-007/`. No push, merge, CI or software install.
