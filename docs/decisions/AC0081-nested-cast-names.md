# ADR AC0081: Nested type names in cast diagnostics

Accepted 2026-10-07 for AC8.22.

A differential UTF-8 reduction found that CLR cast messages identify the default
encoder as UTF8EncodingSealed, while its runtime identity name is
System.Text.UTF8Encoding+UTF8EncodingSealed. The existing formatter incorrectly
used runtime identity names for every cast operand.

TypeSource now carries an optional CastName independently of RuntimeName. Owned
metadata supplies the declared namespace and name, omitting enclosing types only
at the outer level. Closed generic arguments retain their full runtime names.
Array errors append [] to the element cast name. Existing bounded name traversal,
allowed characters, specialization evidence and aggregate text quotas remain.
The direct metadata name uses the same declaration row as the structural or
visibility read; no fields, methods or other assemblies are newly traversed.
Enum query text and type identities continue to use RuntimeName.

A first reduction incorrectly omitted enclosing names inside generic arguments.
The separate installed .NET oracle pinned that distinction before the corrected
formatter was validated. Six new native probes cover nested source/target classes,
nested generic arguments, nested generic roots, arrays and boxed values, adding
1,764 repeated O0/O2 comparisons. The existing generated-name probe adds 294.
A metadata assertion verifies both names remain distinct. All 77 selected tests
pass (ac8-22b-tests.log, 28.200 seconds), including closed type inputs, ordinary
cast paths and Enum.IsDefined. ASan/UBSan, forced collection, frame/cleanup checks
and 30-second subprocess limits remain enabled.

Review checked that no identity keys or layouts depend on CastName, full names
remain available, synthetic sources retain a bounded fallback and all output
names still pass validation. Native v57 records the corrected emitted text; no
other profile or resource bounds change. This slice does not finish the pending
UTF-8 library work or re-run the full frozen engine. Public baseline status remains
36 controls and 11 gaps from AC7b.9. Evidence:
artifacts/alchemy-nested-cast-names/ac8-22a-*, ac8-22b-* and oracle.log.
