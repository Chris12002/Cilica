# Approval request: selected Albireo.Base32 Decode

Approve one isolated validation of **Decode only** at
[`kappa7194/base32@1eabd2fcba515d0bd63881fd2a2879555c43648b`](https://github.com/kappa7194/base32/tree/1eabd2fcba515d0bd63881fd2a2879555c43648b),
MIT, compiling the unchanged single source file under its netstandard2.0 / C#7.3 /
checked-arithmetic contract through an owned project. No upstream project or test
runner is imported. Encode is not selected or claimed compatible.

The owned recipe compares 32 bounded input scenarios on normal .NET and Cilica
native O0/O2, observing all result bytes, exception type codes/message/parameter
UTF-16 units and repeated-result identity. Forced GC, sanitizers and existing
heap/frame limits remain enabled. The maximum input is 257 UTF-16 units; the run is
capped at 30 minutes with 30-second native subprocess limits. Translation failures
stop before oracle execution and are retained as mapping evidence.

Execution uses the existing verified tooling image and 24 hash-verified cached
packages. The container has no network, credentials or host mounts; it is nonroot,
read-only except tmpfs, drops capabilities, denies privilege escalation, and is
limited to 2 CPUs/8 GiB/256 PIDs. Retain all evidence/licences before deleting only
recoverable candidate/task artifacts. The shared tooling image remains untouched.

The required approval comes from the task instruction **“no upstream execution
without exact approval.”** Preparation has performed only owned tests, online
source inspection and read-only image/package verification. Neither upstream code
nor this candidate harness has been built or run. The owned API reductions are
successful, but they do not establish full decoder compatibility.

- [Frozen pins, hashes and limits](albireo-decode-proposal.json)
- [Owned step-by-step recipe](../../tools/CompatibilityCandidates/albireo-validation/README.md)
- [Transcript probe](../../tools/CompatibilityCandidates/albireo-validation/Probe.cs)
- [Explicit differential harness](../../tools/CompatibilityCandidates/albireo-validation/ApprovedAlbireoExecutionTests.cs)
