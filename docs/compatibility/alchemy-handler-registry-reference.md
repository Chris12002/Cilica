# Alchemy handler-registry reference regression

Recorded 2026-10-09 against Cilica source
`232c8d6a95dab498913ccffa7f38839ba358f782` (AC8.80).

The captured Alchemy C++ gives a concrete composition to test: a closed generic
kernel obtains a handler from a dictionary, dispatches through its interface,
and retains the allocated result across subsequent allocations. The new owned
`HandlerRegistryProbe` tests that composition using existing compiler/runtime
support. No compiler profile, capability, production quota or library behavior
changes. This is supplementary regression evidence, not another completed
compatibility feature or full engine-closure claim.

## Reference provenance

Read-only local capture:
`/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/evidence/alchemy-reference-20261008T2312Z`.
The capture report pins Alchemy revision
`445c192ca078519ca69feda41de2b5da1a6191a3` and Unity 6000.3.2f1, Mac ARM64,
Release/OptimizeSpeed, Minimal stripping and incremental GC.

The inspected file is
`generated/Library/Bee/artifacts/MacStandalonePlayerBuildProgram/il2cppOutput/cpp/Generics__25.cpp`,
SHA256 `045c114787632ce6a33a2c168c4f7742079f8750c1da01401b13bada4d327747`.
Relevant operations are the interface handler ID and dictionary insertion at
lines 15060/15064, handler lookup at 15106, and the interface call returning a
preparation result at 16080, retained in a local and consumed at 16085.

The regression is a reduced analogue, not a port of Unity-generated C++.
It uses Int32 keys and owned state/event classes rather than Alchemy's record
identities, full command graph or canonical SHA256 state hashes. Cilica keeps
its own dispatch plan, typed roots, tracing and collection implementation.

The Unity capture's Engine DLL hash is
`266b349757726ebb751f728229cc41321628e877d73eb78633763e9f039cdafd`;
the frozen Cilica audit uses
`f42d9306229a76d833e1578a1a9bc924a5e1eeb4b56b7e326f493b68e2a9ccee`.
These are separate evidence sets. No Alchemy input was rebuilt or modified.

## Owned regression

`HandlerRegistryProbe.cs` defines a closed generic handler interface and kernel,
two handlers (ordinary and explicit interface implementations), a dictionary
registry, mutable state and returned events with payload/state references.
Five entry roots cover:

- Eight alternating commands with an ordered checksum of event fields, state
  identity and allocation-pressure observations, followed by final state.
- Removal, replacement, clearing and registration while prior events remain live.
- Null registration, duplicate IDs, missing handlers, null mapped handlers and
  null state, including exact managed messages where observed.
- A returned event retaining its payload and state after the registry is cleared
  and additional objects are allocated.
- Native lifetime wrappers exposing the same composition for exact collection
  assertions while the kernel and returned event remain separately rooted.

## Validation and review

Isolated snapshot: `/Users/christian/Cilica/artifacts/alchemy-handler-registry-reference`.
The build and both IR validation stages pass. The two differential tests pass:
five roots x 77 argument pairs x three repeats x O0/O2 = 2,310 native/.NET
comparisons, including Int32 extremes. This counts executions, not unique inputs.
Checks retain ASan/UBSan, collection before every allocation, exact frame/borrow
cleanup, exact/one-short frame budgets and the 2 KiB native frame warning.

The two O0/O2 native lifetime checks pass after correcting a test expectation.
The initial check omitted the two private dictionary entry objects released by
Clear; inspection of Dictionary.Remove/Clear established the correct ownership
counts. This required no runtime change. Relative to the rooted registry's
initial count, returning an event adds three live objects (event, payload and
state); removing its handler and bias leaves +1; replacing the other handler
has no net effect; Clear releases that handler/bias and both entries, leaving
-3. A second Clear preserves that count. Dropping the event releases three
more objects (-6); dropping the kernel leaves only the emergency exception,
with no frames or borrows charged to the context.

Five distinct focused checks are therefore passing across `native-tests.log`
(three passes, two initial expectation failures; 31.079s) and
`lifetime-retry.log` (both corrected checks pass; 7.098s). The unchanged
differential tests were not repeated after the lifetime-only correction.
`native-evidence/` retains source/executable hashes for both harnesses at O0/O2.

Review checked call/exception order, explicit interface dispatch, unchecked
arithmetic, returned state identity, roots across allocation and dictionary
reference release. The corrected expectation and added no-op Clear assertion
resolve the test defect; no remaining findings in this slice. No hosted tests
or full suite ran. The independent polymorphic-text work and frozen full-engine
closure/saved-game replay remain outside this regression's acceptance scope.
