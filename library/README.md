# Managed library trial

`Cilica.Library` exposes a portable SHA-256 digest helper (AC7c.1) and four
invariant decimal text helpers for
Int32, UInt32, Int64 and UInt64. They compile as ordinary managed IL through
Cilica's integer-object profile. This is not a replacement CoreLib or an
implementation of the System numeric formatting APIs.

The two-digit division and signed-magnitude algorithms are adapted from the
pinned dotnet/runtime source in `upstream/dotnet-runtime`. That original file is
retained unchanged for review; it is **not** compiled wholesale. The adaptation
uses checked managed strings instead of unsafe pointer writes and runtime-private
allocation. [provenance.json](provenance.json) records the commit, hashes,
upstream paths and deliberate changes. The upstream MIT license and third-party
notices are retained alongside the original source.

The contract is decimal text, including all signed/unsigned boundaries. Format
strings, providers, precision, spans, UTF-8, numeric string-cache identity and
framework member mapping are outside this trial. No intrinsic privilege is
granted to this assembly by its name or namespace.

`LibraryFormattingTests` compares every output code unit and length against .NET
at native O0/O2 with ASan/UBSan, collection before each allocation, repeated calls,
frame exhaustion and ownership cleanup. A managed randomized comparison covers
additional integer bit patterns. Public probes contain no application game rules.

The separate [Cilica.Core trial](Cilica.Core/README.md) supplies Cilica-owned
runtime declarations with exact string/exception mappings and build evidence.
The core image also compiles this helper into four bounded System numeric
ToString(IFormatProvider) implementations with directly proven InvariantCulture
and tested cache identity. This leaves the standalone helper's text-only contract
unchanged. General formatting and the real kernel comparison remain pending.

### Portable SHA-256 helper (AC7c.1)

ManagedSha256.Digest implements FIPS 180-4 with fixed per-call working storage,
explicit big-endian bytes and existing managed integer operations. It has no
platform dependency or mutable static state. Native byte-by-byte, padding-boundary
and repeated-result tests supply 4,746 differential comparisons; managed tests
also cover the million-a known answer and 128 seeded randomized inputs.
Framework SHA256/HashAlgorithm binding and disposed-object behavior remain the
next slice. See docs/decisions/AC0129-portable-sha256-algorithm.md.


### Bounded Decimal route arithmetic (AC8.70)

The owned Decimal value implements Int32 construction/conversion, multiplication,
division and directly proven invariant-provider text. A reusable managed helper
preserves the 96-bit coefficient, sign, scale, nearest-even rounding, overflow and
division-by-zero behavior. Fixed six-word scratch arrays use existing managed
operations; there is no new native numeric ABI or floating-point approximation.
CIL102/object IR62 record Decimal provider admission; native79 and production limits
are unchanged. Constants, five-part/other constructors, parsing, other operators,
equality/hashing and general formatting remain diagnostic.

The final merged snapshot passes 71 focused/adjacent checks, including 38,766 new
O0/O2 native/.NET comparisons with ASan/UBSan, forced collection, exact root/borrow
cleanup and frame-budget boundaries. The frozen engine advances past Decimal to
missing Enumerable.SequenceEqual in redirect-edit admission. This remains partial
closure discovery, without full engine execution, fingerprint parity or replay.
See ADR AC0214 and artifacts/alchemy-decimal{,-merged,-refresh,-engine}.
