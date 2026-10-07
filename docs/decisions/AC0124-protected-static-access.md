# ADR AC0124: Proven protected static base-method access

Recorded 2026-10-07 for AC8.32.

AC8.31's 4,096-method diagnostic copy reaches private-protected static
ActiveEffectSource.RequireText in EnvironmentEffectSource::.ctor IL_000d after
258,578 metadata units. Preserve that frozen application code.

Allow direct calls to Family/FamORAssem static methods on the caller's actual
base-class chain, and FamANDAssem only in the same assembly. Preserve declaring
class visibility. Walk closed base identities with the existing type/metadata
bounds and reject cycles. Include the caller's real class chain even for static
callers that never allocate an instance. Nested lexical family scope and protected
instance access remain diagnostic; no receiver rule is weakened.

Object IR independently validates the caller/target bindings and assignability
between ordinary class layouts. Only ancestry needed by a verified protected
static call is counted as catalog usage. The first focused run exposed this
missing operation-root link; keeping an unused-type exemption would have weakened
the catalog invariant. Both value and scalar stages use the same checked operation
roots. Integer CIL v48/object IR v38 record admission; native v65/layout v24,
owned core, grants and production limits are unchanged.

Six fixture probes cover constructors, indirect inheritance, private-protected
faults, closed generic bases, internal bases and void-return mutation. A raw
foreign-assembly probe adds native execution. Eleven raw access cases cover
family/assembly intersection and union, unrelated callers and private members.
Detached ancestry/private mutations must fail at both IR stages. Nested family
scope and protected instance calls remain diagnostic. All 158 selected checks
pass, including existing access/constructor/catalog/public-baseline regressions.
Seven native probes provide 2,058 repeated O0/O2 comparisons with forced GC and
ASan/UBSan. The 2,048-byte frame and 30-second subprocess bounds are unchanged.
The public baseline remains 43 controls/four gaps.

Review checks exact closed-owner ancestry, class visibility, assembly restrictions,
static-only scope, proof metadata retention and unchanged rooting/fault behavior.
Main 48ecf14 remains an ancestor. Full engine compilation/execution and AC9
saved-game replay remain unfinished.

Evidence: artifacts/alchemy-protected-static/tests-c.log and hashes.json.
Core: ab19cfec0d104da213a013f98cfdcd5fcfee0a66fa3ca727d4c00d83f32f7847.
Manifest: 1b6a938cd355436462ce72b62161fa0a4c42ee071fce3178d1e46c2bb3481f96.
Fixture: 9dabc2e33b231e3f64bc13e0f44517d205f0d671e01fc1c33293f953f7f57c68.
