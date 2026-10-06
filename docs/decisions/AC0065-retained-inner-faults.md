# ADR AC0065: Retained InvalidOperationException inner faults

Accepted 2026-10-07 for AC8.16.

Sorting needs to wrap comparer failures without losing the original exception.
Admit only the exact selected InvalidOperationException(String, Exception)
constructor through an independent intrinsic grant. Retain its message and inner
identity in the existing traced exception payload. A null message uses the pinned
InvalidOperation resource; empty and arbitrary UTF-16 messages remain exact.
Other inner-constructor families and effectful property-reading filters stay diagnostic.

Both logical and scalar validators constrain inner operands, message presence,
parameter/type-name exclusions, exceptional edges and selected framework identity.
The native constructor rejects wrong kinds, foreign/invalid inner objects and
preserves pending exception provenance if allocation fails. No payload layout or
resource bounds change. Review checked pop order, exact grant/signature identity,
GC roots across allocation, corrupt references and unsigned native arithmetic.

A null-message construction with no Message getter exposed an existing catalog
bug: String was included during discovery but omitted from operation dependencies.
Message, parameter-name and type-name operands now retain that dependency even
when the operand is null. NullRead is the reduced regression.

Seven probes pass 3,024 repeated native/.NET comparisons on macOS ARM64, O0/O2,
ASan/UBSan and forced collection. Two native ownership tests check retained graphs,
allocation failure and exact cleanup. Six detached-payload/identity cases validate
both stages; three unsupported cases and grant removal remain negative. All 18
slice checks and 76 adjacent exception/embedding/core/public-baseline checks pass.
The final identity gate also passed 17 existing managed-exception IR checks.
The native test harness was split into small functions after O0 sanitizer spills
exceeded the unchanged 2048-byte frame limit. Identity-tamper tests were corrected
to mutate the scalar stage's authoritative logical metadata, not only native layouts.
Logs: artifacts/alchemy-inner-operation/ac8p6-*, ac8p7-*, ac8p8-*, ac8p9-*, ac8p10-*.

Integer-object CIL advances to v16, managed/scalar exception IR to v6, native
object frames to v50. Layout v20 is unchanged. The pinned constructor source and
adaptation are recorded in library/provenance.json. Frozen engine still needs
IOrderedEnumerable at ReconcileControlOpportunity IL_0079; state remains at 257/256
discovered identities. Core SHA-256:
7d4ad90c1fd6df6c5b99f235314fdde89d254ee91f85229801345174f7b21295;
manifest 26c23bcc6550932a7200cdd27dc23d6adb1248e98bb68166cbd52212f8e3e6cb.
Public baseline remains 31 native controls/16 gaps. Full replay is unverified.
