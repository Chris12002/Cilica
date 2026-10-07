# ADR AC0083: Ordinal String prefix checks

Accepted 2026-10-07 for AC7b.11.

The audited StartsWith(String,StringComparison) call uses Ordinal. Admit its exact
selected declaration under a new StringOrdinalPrefix grant. Reuse the existing
StringComparison enum validation with Equals. Discovery resolves a public managed
instance StartsWithOrdinal(String) helper, checked for its exact owner, signature,
body and attributes. The translator proves constant mode 4 before erasing that
argument. Aggregate argument accounting still charges the original mode operand.
The resulting ordinary IR call retains normal String receiver and fault checks;
there is no new native operation or extra allocation.

The owned helper preserves ordinal UTF-16 comparison, embedded NUL, unmatched
surrogates, longer prefixes, identity and empty shortcuts. callvirt checks the
receiver before entering the body; a null prefix raises ArgumentNullException for
value. Other comparison modes, unproven mode values and the default-culture
overload remain unsupported. Direct calls require a same-block StringLiteral
receiver; function pointers and other direct receivers reject explicitly.

That direct-call bound follows measured evidence. A raw call with null receiver
and literal empty prefix throws in the installed .NET JIT; the same call with a
dynamic empty prefix returns true. The pinned managed source alone did not reveal
the intrinsic expansion difference. Roslyn also emits call for known literal
receivers, so rejecting every call would reject the audit probe. Proving a non-null
literal avoids the observed difference without weakening receiver checks or
claiming support for JIT-dependent null behavior. Three raw-null reductions pin
the actionable diagnostic; direct-call-oracle.log records both .NET outcomes.

Seven probes pass 5,220 repeated O0/O2 native/.NET comparisons across 20-string
pairs, UTF-16 boundaries, identities, empty/null cases, Substring composition and
literal receivers. Four altered calls (arity, missing receiver, scalar receiver,
missing fault edge) reject at logical and scalar IR stages. Grant removal and
unsupported modes/aliases/default overloads remain diagnostic. All 43 distinct
selected checks pass across final runs: ordinal prefix and public baseline in
ac7b11d-tests.log (24 checks, 63.515 seconds), and Equals, Substring and character
construction in ac7b11b-tests.log. The latter run exposed the two superseded raw
call assertions; those were replaced after the independent oracle measurement.
Native builds retain ASan/UBSan, forced GC, exact cleanup, 2,048-byte frames and
30-second subprocess caps. The baseline uses the existing six-probe batches.

Review checked exact grants/signatures, mode erasure, original argument budget,
null order, literal receiver proof, UTF-16 bounds, shared enum validation and
ordinary call validation at both stages. Integer CIL moves to v24. Native v57,
object layout v22 and object IR v30 remain unchanged. No resource bounds change.
The baseline is 38 native controls and 9 diagnostic gaps. Frozen input assemblies
are unchanged; engine/state still hit snapshot/catalog limits. Full replay is
unfinished. Evidence: artifacts/alchemy-ordinal-prefix/ac7b11a-* through ac7b11d-*,
direct-call-oracle.log and final/engine-and-state.json.
Core: 6c4d71df86b355aab2f1f8a0cc038385ee2c010e066f2a3f8bb2c2b4a7493e17.
Manifest: d64d14afc74211a3273263a565d5c31f895a8424272a59dc182dc721f706c104.
