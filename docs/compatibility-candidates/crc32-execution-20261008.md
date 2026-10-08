# CRC32.NET: selected roots verified

The separately approved trial passes **41 scenarios and all 729 transcript
observations** on frozen Cilica `c64312e6993a5bce4078cb23961e737cd1df8e24`, with
only the three owned recipe/adapter/driver files from proposal commit `6068708`
overlaid at their recorded hashes. O0 and O2 each run three repetitions:
**4,374 native/.NET comparisons**, plus exact-frame and undersized-frame checks.
This is Cilica IL-to-C++ execution with forced collection, ASan/UBSan and
root/borrow cleanup, not a benchmark or a substitute runtime.

The unchanged two implementation files from
[force-net/Crc32.NET](https://github.com/force-net/Crc32.NET/tree/26c5a818a5c7a3d6a622c92d3cd08dba586c263c)
are pinned to `26c5a818a5c7a3d6a622c92d3cd08dba586c263c`, MIT Copyright (c) 2016 force.
All ten screened blobs were verified before compilation. The owned netstandard2.0
recipe defines NETCORE/NETCORE20 and uses existing official NETStandard.Library
2.0.3 references, rather than upstream's implicit 2.0.0 preference. No upstream
project, target, generator, test suite or binary was executed.

Verified roots are Compute(Byte[]) and Append(UInt32,Byte[],Int32,Int32), including
the reachable SafeProxy helper and 4,096-entry UInt32 static table. Scenarios cover
empty and boundary-sized buffers, offset slices, high-bit seeds, split appends,
null/negative/out-of-range and unchecked offset-plus-length overflow cases, and
ASCII `123456789` → `0xCBF43926`. Each transcript compares every checksum bit,
exact input-byte preservation, exception kind, full message UTF-16 and parameter
name. Translation completed before any oracle invocation. All expected fault
categories and the known checksum were independently checked against normal .NET.

[Complete differential log](crc32-execution-20261008/differential.log) records both
optimization levels completing all observations; the explicit test passes 1/1 in
29.045 seconds. The runner finishes in 58.60 seconds and the container is removed
at 61.2 seconds. These durations are correctness-run bookkeeping, not performance
measurements. Candidate build has zero errors/warnings; Cilica build has zero
errors and one existing CS0108 fixture warning.

Candidate assembly SHA-256:
`8953221ca64062d565e3694b1ea452952f809b1c6ae22e349d7d88b4604c0636`.
[Input provenance](crc32-execution-20261008/input-provenance.json) retains frozen
export, overlays, candidate blobs and every offline package hash. **Evidence
limitation:** the existing VerifyTranslated helper deletes successful temporary
native files, so generated C++ hashes were not retained as the proposal requested.
There is no emitted-source-hash provenance claim. Preserve this limitation rather
than imply it was checked; future approved trials should collect those hashes
before helper cleanup. No redundant external execution was performed to fill it.

The [inspected container controls](crc32-execution-20261008/container-inspect.json)
confirm offline networking, UID/GID 10001, read-only root, no host mounts or
credentials, dropped capabilities/no-new-privileges, 2 CPUs, 8 GiB RAM/no extra
swap, 256 PIDs, bounded 10 GiB scratch and 1 GiB noexec evidence. The 30-minute
watchdog was active. Existing SDK10.0.401/runtime10.0.12/Clang18.1.3 tooling and
hashed offline packages were reused; nothing was installed.

[Result and commands](crc32-execution-20261008/result.json), owned recipes,
[MIT licence](crc32-execution-20261008/Crc32-LICENSE), receipt hashes and
[cleanup receipt](crc32-execution-20261008/cleanup.json) are retained. Only this
trial's container, input image, export and source staging were removed. Shared
Cilica work, tests, evidence and the approved tooling image remain intact.

Mark these two roots **verified** and skip them unless a changed pin/profile or
concrete regression warrants another trial. Instance HashAlgorithm use,
ComputeAndWriteToEnd, CRC-at-end validation, CRC32C, upstream tests and other
platforms remain unverified. No new production feature was needed for this trial:
prior UInt32 vector and contract support supplied the reachable closure. Continue
the bounded CircularBuffer gaps separately; no permission for its execution is
inferred from this CRC32 approval.
