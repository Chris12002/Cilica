# ADR AC0242: Branched constructor initialization prefixes

Recorded 2026-10-09 for AC8.98 after a3ad60c.

AuthoritativeTargetingContext evaluates a cached lambda before its base call;
BeforeShieldActivationOccurrence conditionally throws while computing a base
argument. Both frozen constructors have one initialization call after branches.
The converted AuthoritativeTargetingContext constructor preserves the receiver
through G_B2_7 and evaluates Select/ToArray before its base call
(Alchemy.Engine__5.cpp:7785-7850, SHA256
`0d78ff9a14ab19c9a872ba767fb4d5ff6bcd5555cd9b79746598f4e1523607e7`).
This guides receiver lifetime and evaluation order; it is not substituted code.

## Decision and review

Replace the entry-block-only check with a bounded worklist assigning each block a
single incoming initialized/uninitialized state. Disagreeing incoming states fail.
Exactly one syntactic direct-receiver immediate-base or same-owner initialization
call is required; normal returns and handler completion must occur after it.
Preinitialization branches and loops may calculate arguments, allocate independent
objects/delegates, invoke independent delegates, or throw an independent exception.
The existing whitelist still rejects reading, borrowing, aliasing or escaping the
uninitialized receiver. Its direct declared-field initialization remains allowed.
Branch conditions and transported stack arguments also cannot observe/alias it.
The translator's existing direct-Parameter0 rematerialization supplies identity
across valid joins; no new emitter alias or pointer representation is introduced.

Handlers and filters start initialized. A linear difference map identifies every
protected try block, and any uninitialized entry to such a block fails. This keeps
initialization faults from being locally recovered. Entry-prefix reentry, repeated
initialization and same-owner constructor cycles remain rejected. The worklist
visits each admitted block once and checks every normal edge; region marking uses
linear block/region work. Existing method/IR limits remain the bounds. CIL122/object
IR75; native86, layouts, owned core, grants and production capacity are unchanged.

Review covered branch merges/backedges, receiver rematerialization versus mutable
aliases, prefix field stores, handler seeds/protection, state after the call and
constructor-chain cycle integration. Native allocation/root publication is unchanged.
No outstanding findings within this slice.

## Evidence

119 distinct selected checks pass: 20 new checks in 12.442s and 99 existing
constructor/array/hierarchy checks in 50.718s. Six C# roots plus two raw-IL branch/
loop roots add 2,352 O0/O2 native/.NET comparisons under ASan/UBSan, forced collection,
cleanup and exact/one-short frame limits. They cover conditional and throwing base
arguments, readonly initialization, same-owner chains, reference arguments, cached
lambda allocation/invocation and postinitialization catch/finally. The cached case
asserts exactly two persistent static objects (closure singleton and Func), not
zero or an unspecified retained heap. Early fixture checks exposed and corrected
that test expectation. Prefix delegate invocation now has the same no-receiver-
escape check as ordinary calls.

Logical/scalar tampering rejects missing/repeated calls, receiver aliases and a
protected prefix. Raw malformed paths reject bypassing initialization, early field
reads, local/static escape, receiver branch tests and repeated calls. Existing
constructor chains, protected constructors, readonly joins, receiver addresses,
array escape controls and allocation-failure recovery remain passing.
Receipts: artifacts/alchemy-constructor-prefix-flow.

The diagnostic full-IR inventory clears both targeted constructor failures, leaving
one delegate capture failure in SummonComposition. All 15,254 bodies translate;
there is still no validated full engine program, emitted native engine or saved-game
replay. Diagnostic limits and frozen input/core/manifest pins remain unchanged from
AC0241. Receipts: artifacts/alchemy-constructor-prefix-ir-inventory.
