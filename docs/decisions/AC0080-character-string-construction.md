# ADR AC0080: Character-array String construction

Accepted 2026-10-07 for AC7b.9.

Admit the exact selected String(Char[]) newobj declaration by reusing the existing
List constructor-to-managed-factory mechanism. Rename the shared compiler map to
FactoryConstructorCalls. String.CopyCharacters requires the exact new
StringCharacterConstruction grant and static Char[] -> String signature; its
body passes normal method/access validation. The recognizer checks void results
before reading declarations, avoiding extra metadata work for ordinary String
methods. Existing shared metadata and closure limits remain unchanged.

The owned managed body returns canonical Empty for null/zero-length inputs. For
nonempty input it copies unsigned character units into a fresh Int32 buffer and
calls the existing granted FromCodeUnits helper. The original Char[] can mutate
without changing the string. Embedded NUL and unpaired surrogates are copied
exactly. The extra temporary buffer is charged against existing quotas; there is
no native allocation or packed-array ABI change. Constructor sections, repetition
and pointer overloads remain unsupported. The managed factory is public like the
existing List copy factory, while special newobj rewriting requires its grant.

Six probes cover code units, independence, object identity, null/empty, repeated
allocation and Substring composition. They pass 1,980 repeated comparisons against
.NET at O0/O2, with ASan/UBSan, forced collection, exact cleanup, 2,048-byte frames
and 30-second subprocess limits. Denied constructor/copy grants, raw call instead
of newobj, wrong array sources and section overloads reject. Four altered calls
(arity, missing operand, scalar argument and fault edge) reject at both IR stages.
All 54 selected checks pass (ac7b9e-tests.log, 106.902 seconds), including the List
construction suite and public baseline. The final cheap return-type filter passes
seven focused checks (ac7b9f-tests.log, 8.201 seconds).

Initial reductions exposed two admission details: the rewritten factory must skip
the original newobj intrinsic recognizer, and its managed body materializes array
length once to preserve the existing bounded Int32 branch contract. Neither fix
relaxes unsupported CIL or shared limits. Review checked exact declaration/grant
validation, receiver/argument accounting, ordinary call/fault lowering, rooting,
copy identity and List regression coverage. Integer CIL moves to v23; native v56,
object layout v22 and object IR v30 remain unchanged.

The public baseline is 36 native controls and 11 gaps. Frozen inputs remain
unchanged; engine/state still stop at their snapshot/catalog limits. Full replay
is unfinished. Evidence: artifacts/alchemy-char-string/ac7b9c-* through ac7b9f-*,
and final/engine-and-state.json.
Core: bbbd92b236950b7b00a373637dd5f8db574847531725470f4fd3f85494ebbcc8.
Manifest: bba731443cc16cc443e780d9f90f746981376ff1264d0ccb6e49c1c56fc9b69a.
