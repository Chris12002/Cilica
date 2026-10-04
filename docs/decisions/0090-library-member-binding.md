# ADR 0090: Structural library member correspondence

Status: accepted for slice 6b.2. Recorded 2026-10-05.

## Decision

Extend the explicit type plan with `ResolveLibraryMethod` and
`ResolveLibraryField`. Resolve the original declaration using the ordinary strict
resolver, then match its mapped owner, name and complete structural declaration
signature against the implementation. Compare declarations before substituting
owner/method arguments: `C<int>.M(!0)` and `C<int>.M(int32)` must remain distinct.
MethodSpec arguments and owner arguments retain separate environments.

Successful results retain both original and implementation resolved members,
including image identities/hashes, tokens and constructed signatures. Like other
resolved members these are session-owned; they are not execution permissions.
Unselected application owners keep their identity while selected types appearing
inside signatures are projected consistently. Ordinary resolver APIs are unchanged.

Missing and duplicate matches are errors. Access, dispatch, special-name, field
storage and mutability flags must agree. Literal constants must exist on both
sides and have identical type/bytes, bounded to 4,096 bytes. Generic method
parameter indices/attributes must agree; nominal/dependent constraints remain
unsupported. Existing signature and caller-owned metadata work limits apply.
Inherited-owner binding, executable body verification, library interface/layout
compatibility and intrinsic authorization remain separate obligations.

## Validation and review

Eighteen new cases cover generic overloads, MethodSpecs, mapped named signatures,
missing/duplicate members, attribute changes, literal changes/absence, nominal
constraints, direct definition tokens, application namesakes, work budgets and
session ownership/disposal. They pass with related type-plan, strict resolution,
signature and contract inventory checks: 112 local tests, zero failures. The
solution builds with zero warnings/errors.

Review checked declaration-before-substitution, original evidence retention,
ambiguity, reference-stub fallback, bounded work and constant comparison. No
actionable findings remain within this metadata profile. No input was executed,
native behavior changed, library source imported or hosted work triggered.
