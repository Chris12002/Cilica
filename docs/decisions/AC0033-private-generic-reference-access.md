# ADR AC0033: Source-scoped private generic reference arguments

Accepted 2026-10-06 for bounded AC8.2.

## Decision

The integer-object discovery profile checks original explicit type names before
substitution. Declaration parameters/returns/locals, supported typed operands,
member owners/signatures and MethodSpec arguments are checked in the originating
method's lexical declaration scope. Same-image internal type names remain usable
from generic declarations; nested private names require the containing scope.
Protected nesting remains unsupported. Metadata-token kind validation precedes
origin reading, and image/token origin snapshots share the metadata budget/cache.
Original catch tokens are retained and checked; the earlier custom-exception
profile still rejects nonpublic exception declarations.

A bound reference argument then passes through opaque T parameters, locals,
returns, fields and closed generic calls without becoming a new explicit name in
the generic library. This does not grant access to the private type's members.
Existing member checks, constraints, nonpublic value/enum argument restrictions,
and detached IR checks remain in place. Other translation profiles are unchanged.

## Evidence and review

Three authored reference transport/holder/delegate probes plus a separate metadata
assembly transporting a private nested reference pass 1,176 repeated native/oracle
comparisons at O0/O2 with ASan/UBSan, forced collection and exact cleanup. Five raw
negative cases reject explicit foreign operands, locals, signatures and catch
names, and an unrelated same-image caller. In particular, explicitly naming the
private type is rejected even when that exact type is the method's bound T.
Catch-origin metadata and the still-unsupported private T[] allocation are pinned.

106 selected generic/enum/lexical/exception/baseline checks pass, followed by 60
focused generic/reference/delegate checks after adding the array and catch-origin
regressions. Review traced every admitted typed opcode, validated cache keys and
bounds, checked the distinction between original and substituted signatures, and
confirmed no rooting, emission or native ownership changes. An initial baseline
failure exposed generic internal-name checking; the corrected source-only lexical
check leaves closed member access unchanged. Initial failures and final logs are
preserved in artifacts/alchemy-private-generics/validation.

Frozen EngineStep advances from its legal private context argument at IL_055f to
CIL3010 for String.Format at IL_05a8. StateLookup still reaches CIL2003 at the
unchanged 256-type limit. Evidence: engine-and-state-final.json in the same private
evidence directory. No input binaries or game rules changed; no full replay ran.

## Remaining work

Detached array/type-test admission still needs explicit generic-origin evidence:
new T[] for a private reference T currently fails the retained IR visibility check.
Do not relax that check based merely on a matching bound type. Private value/enum
arguments and broader member access remain outside this slice. Native v42, layout
v17, grants and the public 18-gap/29-control counts are unchanged.
