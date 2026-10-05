# ADR 0177: Bounded milestone 6 completion

Accepted 2026-10-06. Milestone 6 is complete for the required owned-library subset
and exact real-kernel closure on the locally tested macOS ARM64 target.

## Exit evidence

The private adapter calls the unchanged source-built turn kernel at revision
`a8942127ee6d26545de6daa2259cc2793c13be8e`. Kernel/primitives hashes match the
recorded clean build. The oracle runs separately on .NET 10.0.11; the compiler
reads assemblies as metadata/IL and emits its own C++ runtime. No application
rules were changed or copied into compiler/runtime packages.

Ten command steps exercise rejection, immediate/framed execution, priority passes,
terminal behavior, events, sequence/identity observations and canonical state.
Each pass checks 130 scalar values and 766 UTF-16 code units. Three repeats across
O0/O2, normally and with collection before every allocation, produce 10,752 exact
comparisons. All pass with Apple Clang 17 ASan/UBSan, C++ exceptions/RTTI disabled,
clean execution/root frames and stable retained objects after warm-up.

The selected 6d.23 core has 218 recorded inputs and 88 exact grants. Its image is
`1ce29bc14637577934ce7426575e57da0d70fbe920fadc607f63241640b81020`; its independently
supplied local fixture manifest digest is
`bd23d3e3bf23aea9b0d4bd052b7f7937b99b54dad1090570bb5ea9a684bb5591`.
The pinned reference/source/license evidence remains in `docs/library-baseline.json`
and the library source notices. This trial digest is not a production approval.

## Closure and filter decision

The validated compiler closure has 358 closed methods, including 148 library
instances. Its 287 distinct source definitions (77 library definitions) all have
managed bodies, with zero exception regions/filters. Logical/scalar call and
complete dispatch validation, exact intrinsic authorization and source-body
inspection establish this bounded executable closure. No required executable body
is deferred. General-filter stage B is not needed by this root.

The separate conservative metadata walker remains incomplete: 34 intrinsic/body,
950 array-helper, 78 variance and 8 receiver reports. It lacks the compiler's
closed helper/intrinsic selection and visits fallback declarations outside the
emitted closure. Preserve that report as unresolved; do not misrepresent it as a
successful whole-library audit. General filters remain unimplemented.

## Final validation and review

A public regression combines List/Dictionary value storage, read-only views,
boxed formatting, type-test filters, catch allocations and finally updates under
forced collection. O0/O2 supply 294 additional repeated .NET comparisons, including
exact/short execution-frame bounds and cleanup. Filter/finally region presence is
asserted explicitly.

The full local suite ran 2,630 checks and found three stale budget expectations.
Test-only corrections restore exact boundaries for the previously implemented
512-method limit and six-unit cold member-name lookup. A 163-check follow-up,
including all affected classes and the two new composition cases, passed; the
final two composition checks passed again after region assertions. Together these
runs cover the final 2,632-test scope. The full suite was not rerun after test-only
changes. The final solution build has zero warnings/errors.

Each implementation slice was reviewed and committed. Final review covered
arithmetic/undefined behavior, switch source order, exact value-array identity and
helper packets, constructor initialization/receiver ownership, rooted allocations
and unwinding, repeated collection cleanup, provenance/grants and filter closure.
No unresolved defect remains in the admitted scope. Local main `48ecf14` remains
an ancestor; no pushes, hosted jobs, additional agents or paid services were used.

Private reproducibility records are under `artifacts/milestone6-harness`:
`evidence-6e.json`, `closure-6f-reviewed.json`, their hashed inputs, native harnesses
and four execution logs, plus full/targeted/final-composition test logs.

## Boundaries and next milestone

Completion is a bounded compiler-library capability gate. It does not establish
public ABI, iOS/Android/browser conformance, general filters, arbitrary library
members, primitive-width/enum/nested arrays, escaping locations or general boxed
interface dispatch. Milestone 7 separately delivers the public C ABI and mobile
library/device lifecycle evidence. Broader APIs stay actionable build diagnostics.
