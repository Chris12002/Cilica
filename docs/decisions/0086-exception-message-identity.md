# ADR 0086: Exception message identity and final semantic probes

Status: accepted. Recorded 2026-10-04.

Final milestone review compared string identity as well as message text. A default
Exception/custom-exception message is formatted afresh on each getter call in the
local .NET oracle. Fixed runtime resources are shared across exception instances;
supplied messages retain their original reference. Caching every default message
on its exception preserved text but changed observable reference equality.

Formatted type-name defaults now allocate a fresh string. The nine selected fixed
resources use a separate instance-owned cache and precise root list, distinct from
user literals. All allocations remain charged to heap object/byte limits. Supplied
and contextual cast messages retain per-exception identity. The bounded cache rejects
forged references and cycles without authorizing collection. Its visitor is a separate
noinline helper to preserve the 2 KiB O0 stack gate.

Differential probes cover repeated-getter, cross-exception and resource-versus-literal
identity. Native helper probes compare every selected message with .NET and verify
allocation failure/retry, all nine resource roots, malformed lists and reclamation.
O0/O2 ASan/UBSan and existing tracing/collection checks pass.

Final composed probes also verify rethrow identity after allocating work, mutations
and embedded roots across a borrowed-struct call that throws, and fault clauses that
run only on exceptional exit. This extends the prior handler/cleanup/generic/boxing/
delegate probes without adding general filters or broad framework exception APIs.
