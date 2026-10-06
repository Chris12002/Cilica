# ADR AC0042: List construction from proven collection sources

Accepted 2026-10-07 for bounded AC3.8.

Admit List<T>(IEnumerable<T>) only when the actual translated argument is
statically assignable to the exact ICollection<T> contract or null. Discovery
checks the original selected constructor declaration and retargets newobj to the
independently granted public metadata bridge List<T>.CopyCollection(ICollection<T>).
The bridge's original generic signature, static managed body and result owner are
checked. Translation proves the actual argument against its narrower parameter;
ordinary typed call validation retains that proof at both detached IR stages.
A declaration candidate alone never authorizes a widened IEnumerable alias.

The managed bridge allocates a list through a private uninitialized constructor,
then checks null, reads Count once, allocates exact nonempty capacity, calls CopyTo
once and finally publishes size. Zero count skips CopyTo. This preserves newobj
allocation order and avoids an extra empty-array allocation before null validation.
The existing bounded private empty backing cache is retained. Copies have version
zero; reference identity, value copies and GC tracing use existing storage rules.
No new native operation is introduced. Integer-object CIL v7 records the mapping;
native v45, object IR v18, exception IR v5 and layout v17 remain unchanged.

Eight authored probes plus the advanced public constructor control pass 2,646
repeated native/.NET comparisons on macOS ARM64 at O0/O2 with ASan/UBSan, forced
collection, strict cleanup and existing frame quotas. Custom collections verify
Count/CopyTo faults, zero/negative counts and exact callback counts. Tests reject a
missing bridge grant, widened enumerable aliases, iterator sources and unsigned
array aliases. Four malformed call packets are rejected at both detached stages.
42 adjacent checks pass; the former known-gap assertion advances deliberately and
both updated positive-control native runs pass. Ledger structural validation is
checked after recording this slice. Logs preserve initial grant-selection and
expected baseline-transition failures in artifacts/alchemy-list-construction.
Review checked allocation/root order, callbacks, count snapshot, version zero,
exact signature/grant admission and rejection of unsupported proof paths.

The frozen engine now reaches missing Enumerable.ToArray in BorrowedShieldPayload
IsLegalSelection IL_0035. This is discovery progress, not proof that every reachable
constructor argument passes the later translation check. In particular a widened
IReadOnlyList source in FinalizeAccepted remains outside this slice's proof.
StateLookup retains the 257/256 discovery identity limit. Fresh core and manifest
hashes and frozen-input probe results are preserved in the evidence directory.
General enumerable construction, boxed value/interface enumeration, direct base
constructor calls and full engine/state/replay execution remain unverified.
