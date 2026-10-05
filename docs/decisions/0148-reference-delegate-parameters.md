# ADR 0148: Exact reference parameters in single-target delegates

Status: Accepted for the integer-object profile

## Decision

Extend the existing exact single-target delegate declaration with explicit typed
parameters. Admit Int32 and catalogued reference types (including strings,
interfaces and supported vectors); retain Int32/void returns. Target signatures
must match exactly. Wide/value/byref parameters, reference returns and
variance-based target adaptation remain diagnostic. Recursive delegate parameter
closure also remains explicitly unsupported.

Use the existing managed argument packet's reference member rather than encoding
pointers as integers. Caller values remain rooted until the target publishes its
parameter roots. Capture identity, target tables, null checks and invocation
exception order are unchanged. No new native callback ABI is introduced.

Catalog validation checks parameter count, storage shape and referenced identities,
and includes parameter references in closure checks. Detached interface catalogs
compare parameter sequences structurally. The backward-compatible Int32 parameter
initializer is bounded before allocating, even for malformed count input.
Object layout v9 and native v29 describe the extended representation.

## Evidence and review

Five new probes supply 1,470 repeated native/.NET comparisons at O0/O2 with
allocation-time collection. They cover captured receivers, object/interface/string/
array arguments, integer-returning callbacks, mutation, null invocation and fault
order. The existing generic suite also exercises Action<string,int>.

Review checked exact target matching, argument assignment, caller/callee root
handoff, catalog closure and bounded deserialization. Negative tests reject wide
parameters, unsupported target variance, wrong call arguments, invalid counts,
wide detached storage and unknown reference identities. Parameter declarations
round-trip independently through JSON and remain valid in a detached dispatch
catalog. All 43 focused delegate checks pass (42 in the composed run, plus the
corrected isolated serialization regression); the full solution build passes.

The concrete kernel still requires initialized static delegate-cache storage.
This prerequisite does not complete milestone 6.
