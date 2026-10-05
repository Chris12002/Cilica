# ADR 0163: Exact Int32 enum scalar storage

Status: Accepted for the integer-object profile

## Decision

Validate named enums against the exact selected System.Enum identity and their
immutable metadata snapshot. Require an externally visible nongeneric sealed
automatic-layout definition, one public special value__ Int32 field, only nominal
literal constants, and no interfaces, initialization or explicit layout. Other
underlying widths remain diagnostic. Cache completed classification within each
bounded discovery/catalog session; do not cache failures.

Represent admitted values with existing Int32 operations and storage in parameters,
locals, returns, class/value/static fields and synchronous borrows. Copies and generic
memory operations retain existing behavior; closed generic identities still use the
original named signature, so different enum specializations never share statics or
method identity. Field/name/member resolution happens before storage lowering.
This is compiler metadata admission, without changing the detached Int32 IR or ABI.

Explicitly reject enum boxing before reference/inline fallback. Enum arrays, runtime
formatting/type tests, narrower/wider underlying types and general Enum APIs remain
unsupported. No nominal enum descriptor is invented by scalar lowering.

## Evidence and review

Two probes supply 588 repeated native O0/O2 differential comparisons for enum fields,
copies, borrows, generic reset/returns, independent generic statics and exact interface
calls. ASan/UBSan, forced collection, bounded frames and cleanup checks pass. Malformed
value__ names/flags, wide storage, extra fields, mutable statics, private owners and
initializers are rejected; boxing/arrays/byte/long enums remain diagnostic.
All 130 focused/composed checks and the full solution build pass. Review checked exact
base identity, complete shape checks, bounded caches, generic identity and boxing
rejection. The broader suite exposed an older detached field-budget fixture cloning
a type without updating its lexical owner token; the fixture now preserves consistent
identity and reaches its intended boundary. Production validation was unchanged.

The unchanged kernel now reaches unsupported Boolean storage in ResolutionState's
IsTerminal backing field. Native transcript and implementation/filter closure remain
pending.
