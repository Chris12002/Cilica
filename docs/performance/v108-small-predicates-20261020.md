# Small predicate inlining: v107 to v108

2026-10-20. Base `1e1e9a6` (committed v107 plus investigation), isolated
`codex/profile-v107` checkout. Apple M4 Pro ARM64, Apple Clang 17.
Design and review: [ADR 0215](../decisions/0215-small-predicate-inlining.md).

Paired release medians: **34.961 us/diff -> 31.754 us/diff**, **9.17% less time**.
Three alternating processes per variant, seven samples of 1,024 diffs each;
checksum 6146/diff throughout. C++20 O2/NDEBUG, no C++ exceptions/RTTI. Candidate
C++ is freshly emitted from this compiler; source hashes remain unchanged through
final review. The baseline is the fresh committed-v107 binary from the preceding
investigation. No builds, profiling or sanitizer checks overlapped timing.

This promotes 70 short predicates; six broad checked predicates remain noinline.
The release executable is 570,600 bytes. Maximum native frame is 432 bytes in
release and 384 in both O1 sanitizer acceptance builds. All 523 DiffPlex observations
match the retained .NET oracle in all three builds, including both diagnostics-off
and diagnostics-on forced-GC modes. Each acceptance run requests a 512 KiB pthread
stack and checks an actual size between 512 and 576 KiB.

Twelve focused descriptor, field-access and shared-module checks pass. The initial
batch passed eleven and exposed an oversized O0 test-harness main; all seven
module checks pass after moving the added assertions into a helper, retaining
the 2 KiB test warning. Tests include O0/O2 ASan/UBSan, invalid/colliding descriptor
addresses, field/root predicate pointers, one method per translation unit,
shared identity, forced collection and runtime cleanup.

The owned-core DLL remains SHA-256
`655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1`.
Input DLL/PDB hashes, compiler hashes, commands, raw samples and validation logs
are in the adjacent evidence directory. Unity was not retimed. This establishes
the change on this workload and target, not general platform performance.
