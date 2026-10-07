# ADR AC0103: Enumerable HashSet construction

Recorded 2026-10-07 for AC3.15, composed with AC4.9.

## Evidence and design

Committed 511cdd2 clears FirstOrDefault in the existing diagnostic-only compiler
copy and reaches HashSet<long>(IEnumerable<long>) in SpellContributionSources
ReleaseGrants IL_00d6, via ReleaseFinished and FinalizeAccepted. Engine discovery
charges 138,086 metadata units; StateLookup remains eight methods/9,917 units.
Evidence: artifacts/alchemy-direct-first-{refresh,engine}. Production bounds are
unchanged and neither probe executes the engine.

Use an ordinary managed constructor calling a private copy helper, preserving
allocation of the destination before collection validation. Null throws for
collection. A general ICollection<T> source supplies one Count read and positive
initial capacity; enumerate rather than invoking CopyTo. Other IEnumerable<T>
sources start empty. Existing Add handles hashing, duplicate selection and growth;
managed foreach preserves disposal and fault precedence. Duplicate-heavy sources
trim using stored hashes without callbacks. Existing resource limits still apply.

When the source is a HashSet<T>, use its existing storage directly. An empty source
stays unallocated. A sufficiently dense source clones buckets/entries and free-list
state without hashing or equality; a sparse source initializes for its live count
and reinserts live entries. Preserve the pinned prime threshold, copied values,
callback mutation/fault behavior and postconstruction insertion/removal semantics.
All admitted sets use the existing default key profile; no custom comparer API is
introduced. Metadata nominal shape and serialization declarations stay unchanged.

The constructor-only attempt exposed unsupported set interface enumeration through
conservative dispatch. AC4.9 implements that dependency, without weakening closure
validation. The wide source probe then confirms a separate Int64 vector requirement
inside the empty set cursor. Keep this exact diagnostic and proceed with the
admitted Int32/string/inline-value construction profile; wide fields inside value
keys are covered. Full HashSet<long> enumerable construction remains unfinished.

## Validation and review

Ten construction probes provide 5,940 native/.NET comparisons at 99 input pairs,
three repetitions and O0/O2. Together with the seven cursor probes, this is 7,956
new comparisons. Cases cover duplicates/trim, wide tuple fields, string identity
and null keys, null/empty input, negative/zero/positive reported Count, forbidden
CopyTo, acquisition/movement/current/disposal faults, generated yield/finally,
hash/equality faults, dense/sparse/empty set copies and subsequent mutation.
A separate check pins the exact Int64-enumerable vector diagnostic.

All 38 final combined checks pass (ac3-15e-tests.log, 80.889 seconds), including
existing set mutation, reentrant removal, retained-reference lifetime, grant
boundaries and the 42-control/five-gap public baseline. Runs retain forced GC,
ASan/UBSan, exact cleanup, 2,048-byte frames and 30-second native subprocess caps.
Initial logs preserve the enumerator prerequisite, explicit long-array rejection,
its equivalent lazy-source diagnostic, and missing empty-cursor root expectation.
That fixture now primes the shared cursor and requires precisely one extra root.
The build-only d attempt required the standard xUnit cancellation token; it did not
run tests. No limit or negative expectation was relaxed to obtain these results.

Review checked source-before-count validation, foreach disposal, copied key payloads,
stored-hash trimming, sparse copy callback order, free-list independence, allocation
rooting and exact cleanup. AC4.9 and AC3.15 form one coherent committed dependency
slice. No grants, compiler/native profiles, runtime layout or production limits
change. Local main remains an ancestor at 48ecf14. Full engine/replay is unfinished.
Evidence: artifacts/alchemy-set-enumeration and artifacts/alchemy-set-construction.
Core: 8f67b4d565a228942e1878055386137b380ee5418054ac022b55979037c2e088.
Manifest: 254011b2cca636b03004ce6cb52219ecdbb76de0b7a131376398382108d1ba0d.
Fixture: c062a0f2445dff482fc2dbe6b49172a912747e84679bbd14c4805f5c6a897274.
