# ADR 0108: Closed interface dispatch plans

Status: Accepted

## Decision

Extend the existing interface preparation session with an explicit `BuildClosed`
entry point. Keep constructed type identities through catalog construction,
declaration snapshots, exact target selection and canonical slots. Authenticate
closed method signatures against metadata before publishing detached facts.
The legacy builder retains its nongeneric admission rules.

Closed plans admit Int32/UInt32, Int64/UInt64 and supported reference signatures.
Native-width equality never merges generic arguments: method and owner keys
retain each exact specialization. Class fields, interface argument facts and
reference vectors use the existing explicit integer-object catalog. Unmodeled
Array and String contracts remain diagnostics. Struct/borrow signatures and
variance-only mappings remain unsupported.

Declaration, catalog and mapping work share the caller's bounded metadata ledger.
Plans still contain metadata facts, not verified executable bodies. The same
detached validator checks coverage, slot consistency, signatures and identities.
Methods/maps are ordered by image, token and specialization for stable output.

## Evidence

Forty-two planned mappings match .NET interface maps across Int32, Int64 and
String arguments, inheritance, hiding, reimplementation and explicit bodies.
Tests cover JSON round trips, exact/insufficient budgets, forged/open inputs and
distinct Int32/UInt32 maps despite their equal native width. All 77 distinct
focused checks pass, including existing native interface regressions. Review
found no remaining actionable issue.

Automatic CIL body discovery is the next integration boundary. This plan API
alone makes no claim of native closed interface CIL execution.
