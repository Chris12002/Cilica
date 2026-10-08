# Compatibility batch 006 results

Implemented StringComparer.OrdinalIgnoreCase typed comparison, equality and
consistent deterministic hashing using pinned Unicode 16 data. Local work stays in
`codex/alchemy-replay-baseline`; Review milestone 6's Skip/range-search changes are
preserved. Initial ownership coordination succeeded; two later progress messages
failed with Transport closed. No compiler/runtime grants or limits changed here.

**103 checks passed**, zero failures. The main gate has 94 checks and the additional
boundary gate 9. The explicit external-candidate test is excluded from default runs
and was not executed. No full repository suite or full DiffPlex project execution.

- Managed oracle: all 1,114,112 scalar/unit values and 12,000 seeded malformed UTF-16
  pairs match exact .NET ordering/equality on the current .NET 10.0.11 macOS host.
- Native: **50,460 repeated O0/O2 comparisons** in the new Unicode suite:
  2229 changed scalars/neighbors, 1475 canonical pairs, two 48-by48 catalogue
  matrices and two 49-input dictionary/identity/fault probes, each repeated three times.
- Native execution uses ASan/UBSan, GC before allocations, root accounting and
  frame/borrow cleanup. Earlier dictionary/ordinal and handcrafted boxing/generic/
  dispatch/exception/GC tests pass. Old ignore-case rejection reductions are now
  native controls. Culture factories and nongeneric methods remain diagnostic.
- The network-free generator reproduces 1475 mappings in 203 ranges exactly; pinned
  source SHA256, width, idempotence and expansion checks pass. Unicode licence/data
  remain in their existing retained location; three pinned .NET originals added
  with the repository's MIT notice/provenance.

The implementation excludes U+0131/U+017F as required by pinned ordinal casing,
preserves malformed surrogates, maps valid supplementary pairs, and orders a valid
pair above an unpaired unit. Hashes use the same canonical UTF-16 mapping. No
culture collation, normalization, full casefold expansion or randomized .NET numeric
hash equivalence is claimed. Other host Unicode versions/NLS are untested.

DiffPlex's **selected source closure is mapped**, with no remaining known feature
gate from the original screen. It remains `mapped-blocked` for approval-dependent
actual execution, not for a known unsupported feature. The prepared
[execution plan](diffplex-execution-plan.md) has exact pins, commands, build surface
and isolation requirements. Its authored adapter/explicit test compare complete
result observations; real compilation/execution are still untested. No upstream
MSBuild, binaries or tests ran and no candidate snapshot was downloaded.

Next candidate: **Crc32.NET**, pin 26c5a818a5c7a3d6a622c92d3cd08dba586c263c,
MIT, selected netstandard2.0. Ten project/licence/source/test/build files were
inspected online before any download. Static CRC roots need a UInt32[4096] table;
the independently authored reduction confirms CIL2001 vector admission. This is
an unsupported feature, not an observed candidate semantic failure. Instance
HashAlgorithm and remaining integration are separate validation questions. No
candidate source retained or executed; no disposable copy to delete.

See [manifest](batch-006.json), [evidence](batch-006-evidence.json) and
[design decision](../decisions/compatibility-006-ordinal-ignore-case.md). Logs remain
under `artifacts/compatibility-batch-006`. Resume with `candidate_snapshot.py status
--batch batch-006`; select the unsigned-vector slice separately after coordination.
