# ADR 0144: Proven sealed virtual receivers

Status: Accepted for bounded zero-argument callvirt

## Decision

C# can name the Object.ToString slot even when the receiver has a sealed static
type with an ordinary managed override. Before body discovery, resolve that
public virtual declaration against the proven sealed receiver. Retain normal
virtual-slot facts, typed receiver validation and callvirt null checking; the
resulting dispatch map still validates complete coverage for its selected owner.
No body or behavior is invented for the unimplemented base declaration.

A proof is limited to the immediately preceding local/argument/field load,
constructor, or method result with a sealed reference type, on a zero-argument
callvirt. A branch or exception-handler/filter entry at the call prevents this
proof. Original declarations must be public on externally visible owners, so
narrowing cannot bypass access checks. Unproven Object parameters retain their
existing diagnostic. Existing resolver logic authenticates the slot and override.

Build predecessor/entry maps once per method to keep proof work linear in the
bounded instruction/edge input. No runtime IL execution or speculative allocation
analysis is involved, and no new native ABI operation is required.

## Evidence and review

Two reduced native probes cover locals, parameters, fields, constructors, method
results, override results and nulls: 588 repeated O0/O2 comparisons. Authored IL
rejects branch-entry and nonpublic-declaration proofs; an Object parameter cannot
borrow a caller's allocation fact. The full build and 61 combined compiler,
virtual-dispatch and StringBuilder integration checks pass.

Review added the access restriction and linear predecessor cache. Slot provenance,
receiver typing and null/exception order remain validated by existing lowering.
No remaining issue was found in the admitted scope. This prerequisite does not
complete the library or real-kernel milestone.
