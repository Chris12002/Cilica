# ADR AC0118: Bounded exception message construction

Recorded 2026-10-07 for AC8.30.

## Decision

Frozen 6f56f22 passes internal exception access and rejects the message constructor
in EffectOperationRegistry.Require IL_000b after 229,419 metadata units.
StateLookup remains eight methods/9,921 units. No engine emission/execution.
Evidence: artifacts/alchemy-internal-exceptions-{refresh,engine}.

Admit exactly a fieldless exception's one-string constructor that loads this,
a constant prefix, the unchanged argument, a constant suffix, calls the selected
managed String.Concat(String,String,String), calls the direct selected Exception
or InvalidOperationException string constructor, and returns. Nops are ignored;
locals, branches, handlers, extra statements, lookalike calls and other base
constructors remain diagnostic. Exact forwarding children inherit the verified
message plan. Existing method, IL, literal and call-argument budgets apply.
The selected base constructor and reached string operations still require their
normal exact grants; no application-specific name is recognized.

Allocate/root the exception shell before evaluating constructor literals and
concatenation. Then a checked InitializeExceptionMessage operation initializes
that same object's message and returns its identity. This preserves allocation
order and keeps the shell alive across the ordinary managed concat call. No
constructor behavior is silently skipped. The operation requires a directly
allocated empty shell, an earlier typed string and an explicit fault edge; a
per-block set rejects duplicate initialization in linear validation work. Both
value and scalar validators enforce this contract.

Native initialization checks instance ownership, exception/string descriptors,
nonnull message, fresh payload state and collector state. It performs no allocation,
preserves pending fault provenance, stores the traced message and rejects a second
initialization. No new public API or exception payload layout. Integer CIL v44,
managed/scalar exception IR v7 and native v65 record the extension; layout v24,
owned core, grants and production limits remain unchanged.

## Validation and review

Six probes provide 1,764 repeated O0/O2 native/.NET comparisons: internal/public
constructors, forwarding inheritance, null/empty/UTF-16 arguments, argument faults
and two independent exceptions. Six IR mutations cover wrong/forward operands,
result, missing fault edge and repeat initialization at both stages. Three negative
constructors retain side effects, lookalike calls and unsupported bases.

The native runtime tests exercise allocation pressure with the shell already rooted,
foreign/stale/null references, wrong descriptor, collection in progress, one-time
initialization, exact identity, failure atomicity and message tracing after its
external root is removed. The raw allocator returns null on exhausted quota;
managed wrappers materialize the out-of-memory status. All 19 focused/runtime
checks and 67 exception/boxed-use/public-baseline regressions pass: 86 total.
Forced GC, ASan/UBSan, 2,048-byte frame warnings and 30-second subprocess caps remain.
Public baseline: 43 controls/four gaps. Review found no outstanding issue in source
shape/grants, forwarding cache propagation, allocation/evaluation order, roots,
checked native ownership or resource budgets. Main 48ecf14 remains an ancestor.
Full engine compilation and saved-game replay remain unfinished.

Evidence: artifacts/alchemy-exception-messages/tests-{c,d,e}.log and hashes.json.
Core: fbf06d7abd311dbc38fa5c8bb5939533165ef08bbd1c6581165dbddeb0ff1a6c.
Manifest: 674a4747df758861ecbcc43cb7c6006173f9da4a8c8f41d27b8bc6219b02b779.
Fixture: 32cf503c508b81b7ccddb9971c654c5fab7033721c400676bd46b450aa2f75c6.
