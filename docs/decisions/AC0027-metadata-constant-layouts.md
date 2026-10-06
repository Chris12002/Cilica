# ADR AC0027: Metadata constants are not instance storage

Accepted 2026-10-06 for bounded AC5b.2.

## Decision

The resolver snapshots an IsMetadataConstant fact for exact primitive literal
fields. It verifies static/literal/default flags, a real constant row with the
correct parent/type/size, Boolean/null encoding and UTF-16 byte alignment, with no
RVA, marshaling or explicit offset. The constant row counts against the metadata
member budget. No input code runs and string blobs are not materialized.

Integer-object layout omits these declarations in classes and inline values,
including constant-only empty structs. The original explicit-storage fact remains
true so older profiles and direct field operations still reject them. Generic
snapshot substitution preserves this independent fact. There are no generated
slots, roots, initialization or ABI changes. Named enum constants, decimal static
initializers and floating execution are not added by this primitive metadata rule.

## Evidence and review

Five authored probes supply 1,470 repeated native/.NET comparisons at O0/O2 with
ASan/UBSan, forced GC and strict cleanup. They cover inherited classes, structs
containing references, arrays, empty values and closed generics. Twelve raw checks
cover valid integer/string/null declarations and rejection of missing constants,
instance/initonly/RVA/marshal/offset declarations, mismatched types, private-scope
members and direct ldsfld. All 97 metadata/layout/enum regressions and 21 public
baseline checks pass. Review checked no accidental static slots, unchanged layout
validation for nonconstants, bounded metadata reads and collector ownership.

Initial native fixtures exposed a separate direct-call String.Length limitation.
This slice uses the established callvirt path through a string-parameter helper;
the direct-call gap is retained for the next slice, not hidden in game changes.
The frozen private state-lookup input now advances past StatusKind to the private
nested Operation enum in EffectAmountExpression. Evidence is retained separately
in artifacts/alchemy-metadata-constants. Full saved-game replay remains unfinished;
public counts stay at 18 gaps/29 controls and native v41 is unchanged.
