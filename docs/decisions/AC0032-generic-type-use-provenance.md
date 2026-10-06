# ADR AC0032: Preserve generic type-use provenance

Accepted 2026-10-06 for AC8.1 metadata foundation. Compiler access admission is a
following slice; this change alone adds no executable generic capability.

## Decision

Closed method snapshots retain their original declaration signature and locals.
A bounded resolver API snapshots original type, member and MethodSpec operand
syntax without replacing type/method variables. Explicit names and opaque generic
parameters therefore remain distinguishable even when substitution yields the same
runtime type. Type-use rows charge the shared metadata work budget and signatures
share the decoder's 4,096-node/64-depth bounds. No input assembly is executed.

The next compiler policy will validate explicit names against their declaring
lexical scope, then permit validated generic arguments to flow through parameter
substitution. Comparing only closed types would incorrectly deny a generic
library's T or incorrectly authorize an explicit foreign private name that happens
to equal T. Member access, constraints and exact binding remain separate checks.

## Evidence and review

A metadata fixture proves that an internal class and a generic variable resolve to
the same closed definition while their original syntax stays distinct. It covers
method parameters/locals, TypeSpec, MemberRef, FieldDef and MethodSpec forms,
forwarded method/type variables, exact row-budget exhaustion and unsupported tokens.
All 54 selected origin, closed-binding, generic constraint/translation and exception
input checks pass. Review checked snapshot immutability, no variable substitution
in the provenance API, aggregate signature bounds and lack of execution authority.
The API does not independently validate variable indices or member identity: its
consumer must also use the existing closed binder before execution.

Frozen Step IL_055f constructs ResolutionKernel<GameState, GameCommand, DomainEvent,
AlchemyGameEngine/AlchemyResolutionContext>. The private nested context belongs to
the calling owner; its name is legal there, but a generic library body must receive
it through T. The exact frozen IL excerpt is in
artifacts/alchemy-static-values/engine-step-il.txt. Access support and full replay
remain unfinished. Native v42/layout v17 and public counts are unchanged.
