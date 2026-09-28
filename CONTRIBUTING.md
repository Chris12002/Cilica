# Contributing

Cilica is at the design stage. Begin with the
[specification](docs/design-specification.md) and propose a bounded milestone
slice before implementing it.

## Implementation expectations

1. Describe the observable behaviour and compatibility boundary.
2. Implement a coherent slice with useful unsupported-feature diagnostics.
3. Run targeted local checks. For semantic work, compare with .NET and test
   relevant exception, allocation and boundary cases.
4. Review correctness, ownership and generated-code safety.
5. Update documentation and commit the slice separately from unrelated changes.

Compiler tests should exercise behaviour, not reproduce the implementation.
Reduce compiler failures to small independent examples. Game integration tests
complement the conformance suite; they do not replace it.

For validation rules, pair a rejected example with a nearby accepted example
and assert the diagnostic identifier and affected location. Cover interactions
between rules, especially exception nesting, prefixes and control flow. Random
corruption tests establish robustness; their absence of crashes does not prove
that invalid IL is rejected. Golden files establish the report contract, not
semantic correctness.

As executable semantics arrive, use .NET as an independent reference for
controlled conformance fixtures, including exception/filter/finally ordering.
Run those fixtures in a separate bounded test process; the compiler continues
to treat input assemblies solely as data. For reachability, use small dependency
graphs with known included and excluded members and expected inclusion chains,
including missing references, forwarding cycles and virtual/interface dispatch.
Keep these checks local and focused; larger corruption runs are opt-in.

Pinned prerequisites and the exact local build and test commands are in the
[README](README.md#development). New slices that add prerequisites or commands
must document them there.

## Scope and provenance

Use independently written code and appropriately licensed dependencies. Record
provenance and preserve third-party notices when importing code. Do not copy
Unity proprietary implementation code or include game assets without permission.

The public examples should work without access to a private game repository.
Alchemy is a planned integration target, not a bundled dependency.

## Hosted resources

Hosted CI is deliberately unconfigured. Do not enable workflows or paid hosted
services without explicit maintainer authorization. Any proposed hosted build
must state aggregate runner minutes, including matrix jobs and retries.
