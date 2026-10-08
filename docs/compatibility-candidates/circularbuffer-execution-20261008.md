# CircularBuffer selected-root trial: constructor-chain gate

The user directly approved the frozen proposal with “Ok use Circular buffer”.
One isolated correctness trial ran on 2026-10-08 and stopped at a new compiler gate,
as required by the proposal. **The candidate and Cilica test builds passed; the one
explicit differential test failed before the .NET oracle or native generation.**
No candidate semantic comparison, successful native execution or compatibility
claim follows from this result.

The unchanged `joaoportela/CircularBuffer-CSharp` source is pinned to
`9ebb911dfe27a62c2793d0b02c642f79bbbf524b`, Unlicense. Only LICENSE and
CircularBuffer/CircularBuffer.cs were fetched and independently blob/SHA256 verified.
The owned recipe selects netstandard2.0 with existing NETStandard.Library2.0.3;
no upstream project, test, script, generator or binary ran. Compiler/library baseline
was `87fb7e5f6f3752cf684c90601c1efa4e59af132f`, plus the four hashed owned proposal
files from `3625157` (including offline-package inventory). Source was not patched.

Translation reports:

```text
CIL2001 Invalid reference IR: invalid void call or base initialization.
CircularBuffer.CircularBuffer`1::.ctor token 0x06000003 IL_0008
```

The capacity-only constructor delegates to another constructor of the same closed
generic class with an empty T[] argument. Read-only inspection of ObjectIrValidator
shows BaseCall accepts only a base-owner constructor, not this same-owner chain.
This is a reachable compiler admission gap, not an observed candidate semantic
failure. An independently authored generic-constructor reduction is being retained
with the next owned feature batch. Fixing the constructor state rules needs its own
semantic and malformed-IR tests; broadening validation without those tests is unsafe.

The exact [differential log](circularbuffer-execution-20261008/differential.log)
records “no oracle invocation yet”, then 1/1 failure in1.056s.
[Runner evidence](circularbuffer-execution-20261008/result.json) records31.969s total;
the watchdog exported evidence and removed the container after35.9s. The original
48-scenario adapter remains unchanged and unexecuted. No native source/binary hash
receipts exist because the run never reached generation; this differs from the
historical CRC trial's successful-output retention omission.

Candidate assembly SHA256: `313b61c51f1b7b7a4b36e89d79133507fc3a612538a11409977d38fed762ce07`.
[Input provenance](circularbuffer-execution-20261008/input-provenance.json),
[actual isolation](circularbuffer-execution-20261008/container-inspect.json),
[receipt hashes](circularbuffer-execution-20261008/receipt-sha256.json) and
[Unlicense](circularbuffer-execution-20261008/CircularBuffer-LICENSE) are retained.
The container was offline, non-root, read-only except bounded tmpfs, with no host
mounts/credentials,2CPUs,8GiB RAM/swap,256PIDs and30-minute total timeout. Tooling was
existing SDK10.0.401/runtime10.0.12, Clang18.1.3, Python3.12, Ubuntu24.04ARM64.

Cleanup verified1,880 disposable input files against the frozen archive, overlay,
package and candidate hashes, then removed only the task staging and input image.
The shared tooling image, Cilica/peer source, owned tests and unique evidence remain.
[Cleanup receipt](circularbuffer-execution-20261008/cleanup.json) records exact IDs.
No candidate container or process remains active. Any retry must retain this failure
and freeze/review the changed compiler baseline under a revised approval proposal.
