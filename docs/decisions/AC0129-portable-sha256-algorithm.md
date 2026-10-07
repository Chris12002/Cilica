# ADR AC0129: Portable managed SHA-256 algorithm

Recorded 2026-10-07 for AC7c.1.

After committed AC6.16 (20f87a7), frozen discovery reaches SHA256.Create in
SemanticFingerprint.Hash IL_0009 after 282,498 metadata units. StateLookup remains
eight methods, now 9,923 units. The full framework hashing path also needs disposed
exception/lifecycle support. Split the algorithm from that framework binding.
No engine discovery progress is claimed for the standalone helper alone.

Implement Cilica.Library.ManagedSha256.Digest(byte[]) as ordinary managed code,
following NIST FIPS 180-4 sections 4.1.2, 4.2.2, 5.1.1, 5.3.3 and 6.2:
https://nvlpubs.nist.gov/nistpubs/fips/nist.fips.180-4.pdf
This is Cilica-owned code implementing the standard, not copied platform-provider
code. It uses existing Cilica integer arithmetic, vectors and constant-array data.
No native cryptographic provider, runtime intrinsic or platform-specific path is
introduced. This is implementation evidence, not FIPS certification.

Use fixed private per-call constants, eight state words and a 64-word schedule.
Int32 vectors hold raw word bits; UInt32 locals perform shifts and modulo-2^32
round arithmetic. Synthesize padding while reading blocks, avoiding a padded
input-sized allocation. Int64 positions/padded length and UInt64 bit length avoid
Int32 overflow when padding an otherwise valid vector. Write digest bytes in
explicit big-endian order. No mutable static state or caller-input writes exist.
Returned 32-byte arrays are independent; null input is rejected before allocations.

Eight focused checks and 28 selected library-formatting, constant-array and public-baseline regressions pass (36 distinct checks). The public baseline stays at 43 controls/four gaps. Native O0/O2 tests compare every output byte and length
for 19 deterministic lengths from zero through 257, including both sides of
55/56, 63/64, 119/120 and 127/128 boundaries, plus published abc and 56-byte
messages. Repeated-output independence and null handling bring the total to 4,746
repeated native/.NET comparisons. Managed known-answer checks also cover the
million-a vector and 128 seeded randomized inputs up to 8,192 bytes. The million
vector/randomized set is managed evidence, not native coverage. Forced collection,
ASan/UBSan, exact heap cleanup, frame-quota recovery, the 2,048-byte frame warning
and 30-second subprocess limits remain enabled.

Review checked all 64 constants, initial state, rotation counts, signed storage
reinterpretation, unchecked additions, byte order, padding length arithmetic,
per-call roots and input/result ownership. Maximum-size vectors are not allocated
by these tests. Core/grants and compiler/IR/native profiles are unchanged. The
public SHA256 API and unchanged SemanticFingerprint still require the next slice;
full engine execution and saved-game replay remain unfinished.

Evidence: artifacts/alchemy-sha256-algorithm/tests-c.log and hashes.json.
