# ADR AC0035: Instruction-scoped private generic arrays

Accepted 2026-10-07 for bounded AC8.4.

## Decision

Discovery records evidence only for an original bare type/method variable used
by newarr or the exact admitted Array.Empty<T> intrinsic. The original source-name
checks run first. A private reference argument carries its detached closed context
and variable index to that single IL offset and operation kind. MethodSpec argument
syntax is now exposed separately by the origin snapshot API.

Both logical and scalar IR validate the owner and method specialization hashes,
bounded context and use lists, exact reference argument/catalog identity, and a
unique matching new-array or empty-array expression at that offset. Proof scanning
checks aggregate block/statement limits before enumeration. Context/use work shares
the existing metadata budget. Value lowering retains the evidence; native allocation,
rooting, array identity, faults and collection code are unchanged.

The ordinary type visibility check remains. A validated array use supplies only
that missing permission; it does not permit casts, type tests, member access,
nonpublic value arguments or arbitrary explicit type names. Generic type-test
origins, nested vectors and wider private member access remain later work.

Object operations IR advances to v16 and integer-object CIL to v4 for the added
method evidence. Native v42, layout v17 and intrinsic grants are unchanged.

## Evidence and review

Five authored probes cover method and owner variables, Array.Empty identity,
List<private reference> across the owned-core boundary, copied/live reference
payloads and negative-length faults. A sixth raw cross-assembly array probe has an
independent oracle. These pass 1,764 repeated O0/O2 native/oracle comparisons with
ASan/UBSan, forced GC, frame quotas and exact cleanup. Previous transport probes
also pass. Eleven mutations are rejected in each of logical and scalar IR:
missing evidence, index, variable scope, offset, operation, context, owner context,
duplicate/default lists, null use and null context. Oversized block/statement
scans are rejected before enumeration. A legal array followed by a private T cast
still fails the retained type-test gate; a foreign explicit name equal to T fails
the source check.

117 selected generic, lexical, array, collection and public baseline checks pass;
46 affected checks pass after the review found and fixed early scan bounds.
Review checked scope confinement, matching specialized identities, preserved
lowering data, no blanket visibility relaxation, and no native allocation changes.
Logs, binary digests and commands are in
artifacts/alchemy-private-generic-arrays/validation and its parent directory.

The frozen EngineStep still reaches missing String.Format through its call at
IL_05a8, and StateLookup still reaches the unchanged 256-type limit. The reported
parent IL site is not necessarily the inner failing member call; improved call
resolution diagnostics are subsequent work. No full saved-game replay has run.
