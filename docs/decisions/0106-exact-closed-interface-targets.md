# ADR 0106: Exact closed interface targets

Status: Accepted

## Context

Closed interface assignments alone do not identify executable bodies. The kernel's
interface calls need class mappings that retain type arguments through inherited
contracts, explicit implementations and virtual overrides.

## Decision

Add the opt-in metadata API `ResolveClosedInterfaceMethodTarget`. Authenticate the
supplied declaration against its metadata and validate each closed owner and its
constraints using a shared bounded ledger. Preserve arguments in the receiver,
mapping, canonical slot and effective implementation.

Reuse the existing exact interface and class-slot algorithms. Closed binding
contexts substitute owner arguments before comparing signatures or MethodImpl
rows. Existing nongeneric APIs keep their admission boundaries. Closed snapshots
and signature-node checks share the caller's member budget, and provenance is
checked before identity deduplication.

Only an exactly implemented contract is selected. Variance-only target selection,
generic methods, default interface bodies, abstract class hierarchies and array
helpers remain separate gates. This API authenticates metadata choices; it does
not verify IL, grant executable access or execute input assemblies.

## Evidence

Forty-eight mappings and authored calls agree with .NET across Int32, Int64 and
String arguments, inherited/hidden/reimplemented slots, private explicit bodies,
closed generic bases and two instantiations on one receiver. Each successful case
also passes with its exact metadata budget and fails one unit below it. Negative
tests reject forged signatures, open owners, violated constraints, mismatched
arguments and variance-only requests. Existing exact-dispatch tests still pass.
