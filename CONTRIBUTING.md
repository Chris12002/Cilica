# Contributing

Cilica has an early metadata frontend and a bounded integer backend. Begin with the
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

Follow the [test strategy](docs/test-strategy.md) for paired accepted/rejected
cases, rule interactions, independent expected results, slice 1c's dependency
graphs and later differential execution against .NET. It distinguishes current
coverage from planned checks and defines the local validation/resource policy.

Pinned prerequisites and the exact local build and test commands are in the
[README](README.md#development). New slices that add prerequisites or commands
must document them there.

## Code discipline

- Put semantic invariants and references beside non-obvious code. Explain why
  ordering, identity, overflow or lifetime matters; do not merely narrate the
  statements. Record consequential choices in an ADR and link their tests.
- Separate parsing, binding, analysis and presentation. Use scoped handles and
  structured identities internally; display strings are not symbol identities.
  Keep abstractions small enough that their error and ownership paths can be
  reviewed directly.
- Make resource ownership and lifetime explicit. Prefer immutable public
  results and one clear owner for readers, native buffers, roots and handles.
  State thread-safety and reentrancy assumptions at boundaries.
- Distinguish malformed input, unsupported semantics and compiler defects.
  Validate untrusted sizes/tokens before use, bound graph traversal, and catch
  expected input failures narrowly. Never turn an unknown result into an empty
  successful result or a guessed implementation.
- For generated/native code, specify widths, signedness, overflow, alignment,
  aliasing and rooting across allocations. Do not depend on undefined C++
  behaviour. Use narrow, justified warning suppressions and preserve sanitizer
  coverage as executable support arrives.
- Keep toolchains/dependencies pinned, output deterministic and default checks
  local. Require a measured workload and preserved conformance before accepting
  an optimization that makes correctness harder to inspect.
- Commit one coherent behaviour change at a time, with its regression tests
  and documentation. Review the staged diff, keep generated/private artifacts
  out of Git, and avoid unrelated formatting churn. Document limitations and
  implemented capabilities separately from plans.

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
