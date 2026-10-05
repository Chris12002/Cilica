# ADR 0161: Exact runtime-owned String.Empty field

Status: Accepted

## Decision

Supply the pinned public static readonly String.Empty declaration without a managed
initializer. It is runtime-owned, not an ordinary default-zero static. Add an exact
StringEmpty field capability to build evidence and admission, preserving field token,
owner and signature digest. The producer selects one field by name with a null
parameter count, rejects ambiguity and bounds metadata work. No method or type token
can impersonate a field grant.

In the integer-object compiler, reads of the selected String field require the exact
public/static/initonly flags, string signature, no explicit storage, generic owner,
reference image or initializer. Writes and other selected String statics fail. Require
both the field grant and the existing String layout validation. Lower the read to
an empty StringLiteral, retaining its allocation safepoint and reserved per-instance
identity shared with empty literals, Concat and StringBuilder snapshots. No new
runtime representation or native ABI is introduced.

## Evidence and review

Native O0/O2 tests provide 294 repeated comparisons of identity, length, whitespace
and bounds faults, with forced collection, ASan/UBSan, exact frames and cleanup.
Missing field approval, writes, incorrect token kinds, selector ambiguity and tight
selector budgets are rejected. Composed core, producer, admission and string tests
pass; full solution builds cleanly. Review verified that default-static folding cannot
intercept the field, the canonical literal is precisely rooted, and producer output
still requires independently approved evidence.

The kernel passes Empty and reaches an aggregate compiler limit while expanding
List.AsReadOnly. Native transcript equivalence and filter closure remain pending.
