# ADR 0092: Approved library build evidence and exact intrinsic grants

Status: accepted for slice 6b.3 compiler admission. Recorded 2026-10-05.

## Decision

Mapped compilation may substitute reviewed runtime operations only when the
compiler host supplies a build manifest and an independently approved SHA-256
of its exact bytes. `CreateTrustedLibraryCompilationScope` validates this record
against the plan's selected implementation snapshots. The digest must come from
the trusted build/release policy, never from the input assembly, attributes or an
adjacent manifest. The API does not discover files or silently approve evidence.

The `cilica.library-build` version 1 record contains:

- the exact compiler/runtime intrinsic profile, `cilica-library-intrinsics-v1`;
- each selected implementation's full identity and content hash, exactly once;
- effective compiler arguments and hashed inputs labeled source, compiler,
  reference or options, including at least source and compiler bytes;
- an explicit list of granted capabilities, including empty lists where appropriate.

This is an approved build **attestation**. Admission checks its structure and
output correspondence; it does not rebuild the source or prove that arbitrary
claimed inputs produced an output. The trusted library build must generate the
record from the actual compiler invocation and effective inputs. That producer,
source imports and shipped-artifact records accompany the trial library build;
the planning baseline still contains no trusted shipped outputs.

Each grant identifies a type or method token, owner token/name, member name and
SHA-256 of the raw declaration signature within an exact output image. Type
layout grants use the empty-signature hash; the output hash pins the complete
type metadata. Grants are versioned enum capabilities, not arbitrary unsafe
permission. Existing structural and call-form checks still apply after approval.
Changed overloads, members, images or capability names cannot inherit a grant.

The current set covers String layout/length/code-unit/equality/inequality/concat,
and exception layout/construction/message access. Both object and value/exception
compiler paths enforce applicable grants when using a mapped compilation view.
Custom exception constructors still undergo forwarding-body verification; their
selected framework base constructor needs its own approved grant. A plain mapping
view has no grants. Existing strict bounded profiles retain their prior explicit
runtime selection contract; this does not grant new unsafe operations there.

Admission is atomic and bounded: 1 MiB JSON, depth 16, 256 outputs, 4,096 inputs
and grants in total, 1,024 arguments per output, and 4,096-byte signatures.
Unknown and duplicate JSON properties, integer/unknown enum values, duplicate
outputs/inputs/grants and incomplete evidence fail. `CIL3016` reports invalid build
evidence; `CIL3017` reports a missing exact intrinsic permission. The approved
digest is retained on the view for later build/audit evidence.

## Validation and review

Thirty-five admission cases cover independently pinned bytes, changed output
identities/hashes, missing source/compiler evidence, invalid/duplicate inputs,
arguments, owner/member/token/signature mismatches, missing outputs, profile and
version changes, JSON ambiguity/unknown properties, malformed and oversized
documents, absent layout/member permissions, and separate exception capabilities.

An independently authored metadata fixture translates only with the applicable
grants. Its UTF-16 string length matches .NET at O0/O2 with UBSan. Exception
translation succeeds with all grants and fails when each reached capability is
removed. Related mapping, string and exception regressions also pass: 137 local
checks, zero failures. The solution builds with zero warnings/errors.

Review checked trust-anchor origin, non-constructible admission state, snapshot
pins, declaration-specific permissions, both compiler call paths, atomic failure,
resource bounds and unchanged structural validation. Signature hashing was
bounded before allocation. No actionable findings remain within this admission
gate. Synthetic test attestations are not shipped-library provenance; no shared
library source was imported or hosted work used in this slice.
