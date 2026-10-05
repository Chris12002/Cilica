# ADR 0124: Mapped library implementation audits

Status: Accepted

## Decision

Expose `AuditLibraryImplementation` only on an explicit library compilation scope,
with explicit definition roots. Reuse the bounded reachability walker while
retaining the full pinned type plan and every observed changed method/field
projection. Each member record includes its use image/token, both declaration
images/tokens and constructed signatures. Image tables retain identities and
hashes. This makes the selected implementation and generic environment reviewable.

Keep projection collection local to the walker; do not introduce mutable audit
state on the resolver. Deduplicate repeated uses and bound unique member records
by the configured edge limit. Ordinary audits and low-level reachability APIs
remain strict and retain their existing serialized profiles. The explicit report
uses `explicit-library-implementation-closure-v1` and renders mapping evidence
in text and JSON after resolver disposal.

Expand implementation bodies, fields and initializers. This API has no contract
deferral option and does not silently omit intrinsic bodies. Missing members or
bodies, unsupported dependencies and budget exhaustion keep the closure
incomplete and filter absence unresolved. Observed general filters remain visible.
If the scope was independently admitted, retain its approved build-manifest digest;
an unapproved mapping has no approval digest. Neither condition proves executable
compatibility or completes the milestone's actual trial-library closure gate.

## Evidence and review

All 147 distinct focused checks pass across mapped audits, ordinary audits,
reachability, mapping, build production, compilation scopes and dispatch. Reduced
fixtures cover implementation-only helper filters, fields/initializers, explicit
reference roots, closed owner/method arguments, missing bodies/members, graph and
projection limits, deterministic output and detached evidence. Actual build
producer evidence is carried through an admitted scope into its audit.

Review found no remaining actionable issue. Full framework mappings, the required
trial implementation closure and native kernel equivalence remain pending.
