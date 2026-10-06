# ADR AC0049: Protected immediate-base constructors

Accepted 2026-10-07 for bounded AC8.9.

PriorityPassedEvent calls the protected constructor of abstract DomainEvent. Add
this case to the explicit integer-object CIL profile using existing constructor
initialization checks, rather than admitting general protected method access.

Discovery validates a direct call from a constructor to its exact closed immediate
base constructor. A shared access helper accepts Family or FamORAssem; FamANDAssem
also requires image identity. The base owner must be public or visible to the caller
under existing same-image/lexical rules. Public/assembly access keeps its existing
rules. Newobj does not use the new family path.

Logical and scalar BaseCall validation independently verifies the immediate base,
constructor kinds, original parameter-zero receiver, exact arguments, direct
nonvirtual call and fault exit. Existing constructor-prefix validation requires
exactly one base initialization and rejects skipped/repeated initialization,
re-entry, local recovery around the prefix and early receiver escape. Value/borrow
argument lowering remains unchanged. General protected methods, direct protected
allocation from unrelated owners and this-constructor chaining are not added.

Six authored probes pass 1,764 repeated native/.NET comparisons at O0/O2 on macOS
ARM64 with ASan/UBSan, forced collection, exact cleanup and frame bounds. They cover
base allocation/throwing behavior, deeper inheritance, generic value/reference
payloads and private-protected/protected-internal construction. Twenty-nine focused
existing constructor and forged-IR checks pass; forged receiver, duplicate call,
grandparent selection and invalid fault target are rejected at both stages.

Six metadata-only cross-image fixtures validate successful Family/FamORAssem base
calls through scalar lowering, and reject FamANDAssem/private access, unrelated
inheritance and direct protected allocation. These are access/translation tests,
not separate cross-image native differential execution. Initial fixture setup tried
to add a dependency after resolver creation; the resolver correctly keeps a fixed
directory snapshot. The corrected setup writes both inputs before creating a fresh
resolver and reconstructs the exact library plan. All six pass. The initial test
compile error and resolver failures are retained in artifacts/alchemy-protected-
constructors alongside corrected evidence.

Review checked access intersections/unions, closed base identity, owner visibility,
newobj isolation, receiver provenance, exception ordering and unchanged resource
bounds. Integer-object CIL advances to v11, object IR to v21 and native to v48;
object layout v19, delegate profiles v3 and exception IR v5 are unchanged.

Frozen engine discovery now reaches missing Enumerable.Select in
SpellContributionSources.ReleaseFinished IL_0049. Its selector returns SpellId,
followed by Distinct and ToArray, so inline delegate results are also required.
State discovery retains 257/256 identities. Inputs and game rules are unchanged;
complete engine execution and saved-game replay remain unverified.
