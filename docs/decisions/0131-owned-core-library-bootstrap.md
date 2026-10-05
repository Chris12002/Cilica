# ADR 0131: Cilica-owned core library bootstrap

Status: Accepted

## Decision

Build a minimal Cilica-owned core declaration image using the explicit
zero-reference producer profile. Primitive identities and compiler-required
attribute declarations let Roslyn emit the image without referencing an installed
CoreLib. Extern internal-call declarations expose supported string/exception
operations to the existing native runtime. They do not grant privilege; exact
build-specific grants and independent approval remain required.

Keep the source and intrinsic requests separate from the imported decimal
algorithm. Map exact declarations against the pinned .NET Standard reference
image. Add no namespace-only fallback, CLR execution, fabricated upstream build
attestation or application-specific behavior. Unsupported runtime members have
no placeholder implementation.

## Evidence and review

The combined 136-check local run passes. Three probes provide 882 repeated native
O0/O2 ASan/UBSan results matching .NET, covering UTF-16 code units, concatenation,
equality, argument exception payloads, arithmetic faults and the ported decimal
helper. Forced collection, exact/short execution frames and cleanup remain
checked. Unapproved scopes and unsupported string hashing fail explicitly.

The actual core build has no assembly references and records 200 inputs and 57
requested declaration grants. Its image SHA-256 is
`165f8619eb15a1e58f2ea19a87f09e6dc12ee2cd6a07ac14612b1d4068bc7f96`.
Tests simulate independent approval only for their local reviewed fixture build.
Production baseline approvals remain empty.

Review corrected the string indexer's emitted name to get_Chars and retained the
reference image's sealed IndexOutOfRangeException declaration. Mapping errors now
identify the mismatched type. No native ABI change was needed. Numeric System
formatting APIs, culture providers, collections, complete implementation audits
and native kernel equivalence remain later gates.
