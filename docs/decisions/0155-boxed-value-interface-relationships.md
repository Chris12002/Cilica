# ADR 0155: Boxed value interface relationships

Status: Accepted for casts and storage in the integer-object profile

## Decision

Retain each boxed value's nominal interfaces and their validated reference ancestry,
including admitted variance, in native descriptor predicates. Use the same bounded
relationship calculation in discovery and detached validation. The descriptors
still describe the existing privately allocated payload; reference fields remain
precise roots and value copies remain separate from boxed identity.

Allow boxing and exact unboxing of interface-bearing values in the composed profile.
When CIL assigns a freshly boxed value directly to an interface, prove the conversion
from its exact payload type and emit an ordinary typed checked cast. This covers
locals, fields, array stores, arguments and returns. Box provenance lost through
untyped stack joins still needs an explicit cast or typed local in this profile.

Keep boxed interface method calls diagnostic, including mixed class/boxed receiver
sets. Class-only dispatch tables cannot safely implement a value receiver. Discovery
and both logical/scalar detached validation enforce this restriction. A follow-up
must lower the actual box payload receiver before enabling those calls. This slice
does not implement value-type isinst or general object virtual dispatch.

Boxed value IR v2, scalar boxed IR v3 and native emission v33 carry this contract.
Legacy catalogs still reject value interface declarations.

## Evidence and review

Three independent CIL probes provide 882 repeated native/.NET results at O0/O2 with
ASan/UBSan and collection before allocations. They cover direct/inherited/variant
casts, unrelated casts, null/wrong unboxing, fresh identity, implicit field/array/
return conversions, copy isolation and retained references. Existing native box
quota, malformed payload, borrow and collector tests remain green. Missing/forged
interface metadata remains rejected. Mixed and all-boxed interface dispatch stay
diagnostic.

The full solution builds without warnings; 90 distinct focused/composed checks
pass. Review checked descriptor authentication, private allocation, type-relation
work bounds, reference catalog agreement and the separation of casts from dispatch.
This is a prerequisite for the kernel's formatted registry error, not evidence of
completed native kernel transcripts.
