# ADR 0200: Cached borrow registration lookup

Status: accepted. Recorded 2026-10-20.

## Decision

Native v94 keeps the original global, non-repeating 64-bit lifetime tokens and
64-registration bound. Each context may cache 64 registration pointers, indexed
by token modulo 64. Only an exact full-token match is usable. Collisions and
missing scratch storage use the existing bounded list lookup. Registration still
validates the active root chain, duplicate roots, storage extents and predicates;
non-top active root registrations remain supported. Token exhaustion is unchanged.

Every RootScope pop already invalidates surviving registrations before releasing
or reusing storage. BorrowScope teardown removes its cache entry before its
registration dies. Consequently lookup can trust that a registered, non-retired
root remains active, without walking the root chain on every access. It still
checks root slot pointer/count identity, slot bounds, width, readonly status and
reference predicates. No carrier contains an unchecked native storage pointer.
`CILICA_VERIFY_BORROW_LIFETIMES=1` restores both complete chain walks for private
corruption diagnostics. Public handles, heap/GC ownership and array checks do not
use this cache. It contains no managed roots and does not retain managed objects.

The optional 512-byte cache is allocated lazily with native nothrow allocation,
owned and freed by Context. Allocation failure is a cache miss, not a managed
failure. It does not consume execution-arena bytes or reduce token capacity.
Context grows by one pointer. A large embedded cache was rejected after stack
measurement. The bounded-stack harness now runs its two independent closures in
separate noinline host functions, retaining its 2 KiB frame warning and actual
256–320 KiB pthread stack check for the same recursion/depth/budget cases.

## Review and evidence

Review traced registration, root retirement, scope destruction, collision lookup,
full-token authentication, fallback and context isolation. Four mode/optimization
checks exercise non-top registration, collisions, missing-cache fallback, forged
same-index tokens, stale/reused frames and token exhaustion under ASan/UBSan.
Forty-five distinct focused checks pass across the final runs. Two old stale-array
native harnesses now select CILICA_VERIFY_INTERNAL_REFERENCES=1 as required for
intentional freed-pointer injection; their assertions remain unchanged.

The fresh v94 DiffPlex candidate is compared to the preceding v93 emission.
See [performance results and retained evidence](../performance/execution-slices-20261020.md).
