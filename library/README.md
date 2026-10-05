# Managed library trial

`Cilica.Library` currently exposes four invariant decimal text helpers for
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
Its native probes also compose this managed formatter with the owned core image.
System numeric formatting APIs and the real kernel comparison remain pending.
