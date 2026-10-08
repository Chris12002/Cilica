# Compatibility batch 008 results

Implemented checked native array indices: conv.ovf.i and conv.ovf.i.un retain the
emitted target width and fault ordering, then use a bounded index encoding that
cannot wrap a large invalid index into a valid one. Only same-block array element
consumers are admitted. General native integer flow and scalar addresses retain
diagnostics. Profiles: CIL 86, IR 56, native 78; no layout/grant/quota changes.

**128 distinct checks pass; zero remaining failures.** The focused gate passed 72
checks and the final gate 78, with overlap removed. The checked-index suite has 29.
Seven 64-case probes, two six-input raw-IL conversions, the 49-input static-owner
probe and 35-input promoted WideIndex give **3,264 repeated normal .NET/native
comparisons**. Two 64-case store probes add **768 labelled 32-bit width-emulation
comparisons**. This is not an actual 32-bit ABI run. O0/O2, ASan/UBSan, forced GC,
frame caps and root/borrow checks remain enabled. Existing UInt32, scalar conversion,
exception, boxing/confinement, closed-generic and value-array packet checks pass.
The initial old rejection is now a native control. Other development failures were
fixture syntax, invariant-format analyzer checks and an incorrect expected negative
diagnostic ID; final outcomes are recorded in the evidence, not hidden by reruns.

The HashAlgorithm-subclass static-root/singleton pattern passes independently.
The selected Crc32.NET static source requirements are now mapped with no currently
known feature gate. Actual candidate compilation, IL closure, results and instance
HashAlgorithm providers remain unverified. CRC execution is outside the separate
DiffPlex approval and needs its own reviewed plan/approval. Pinned source was read
online again; no upstream algorithm was compiled or executed.

**DiffPlex is approved but unexecuted.** The read-only feasibility check found
Docker Desktop/CLI installed but its explicit local daemon socket absent. An
existing VM disk does not establish an available reviewed image. Host SDK/Clang are
macOS binaries; no offline guest toolchain was verified. NETStandard.Library 2.0.3
is absent from the configured global package cache. No service was started, no
image/container provisioned, no package downloaded and no credentials accessed.
The approved recipe/driver match Cilica 268024e exactly; use that isolated baseline
for the authorized 64-scenario run when prerequisites are available. Later CRC/GUID
changes are separate. See [feasibility](diffplex-isolation-feasibility.md).

Review milestone 6 was contacted before edits and at completion; its GUID commits
are preserved. Build outputs are isolated, while source hashes record the final
shared working capture. No full repository suite was run. No candidate copies
were created, so cleanup had nothing to remove. No push, merge, CI or install.
[Manifest](batch-008.json) and [evidence](batch-008-evidence.json) retain next actions,
source/binary/log hashes and final test identities; logs remain in
artifacts/compatibility-batch-008/.
