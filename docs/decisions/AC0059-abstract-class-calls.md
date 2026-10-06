# ADR AC0059: Bodyless abstract class declarations

Accepted 2026-10-07 for AC8.13.

StringComparer requires abstract class dispatch. AC8.12 resolved metadata slots;
this slice carries their signatures through the compiler without invented bodies.
ObjectAbstractMethodDeclaration is stored separately from executable Flow.Methods.
Its binding must name a public virtual abstract method on an abstract class. The
first admitted signatures contain scalar/reference parameters and scalar/reference/
void results. Closed owner specializations remain exact; generic methods, inline
values and borrows are rejected. Abstract signature adapters used in lookup tables
are never passed to body validation, rooting, frame construction or emission.

Discovery reads a bounded MethodDef flags row before attempting body discovery.
Ownership and metadata work are checked. Exact abstract signatures and canonical
slots are authenticated once and call-site access is checked on cache reuse. They
share existing method and parameter budgets, including exception discovery; limits
are not raised. Concrete overrides discovered through complete receiver maps retain
ordinary body translation. Abstract catalog types are excluded from runtime receiver
maps. Null-only abstract calls may have empty maps: argument evaluation and receiver
null checking precede dispatch as usual.

Logical/scalar validators require matching flags, owners, slots, signatures and
bindings. Abstract declarations cannot duplicate executable bodies, be unused,
serve as dispatch implementations, bypass null/virtual checks, be captured or be
allocated. Reachability follows concrete targets without visiting bodyless class
signatures. Boxing confinement sees their receiver signatures. Interface plans
retain abstract mapping/slot facts but require translated concrete implementations.
Native dispatch signatures can refer to abstract declarations; no abstract C++
function is emitted. Existing rooted call packets, pending exceptions and cleanup
are reused; no native ABI or layout change is needed.

Nine probes cover scalar/Int64/reference/void/property calls, inherited overrides,
re-abstraction, hidden new slots, closed generic owners, interface mapping, null-only
closures, argument faults and callback faults. They pass 3,402 repeated .NET/native
comparisons at O0/O2 on macOS ARM64 with forced collection, ASan/UBSan, exact cleanup
and the unchanged 2048-byte frame bound. Both IR stages reject forged facts and
missing/wrong/abstract targets. Metadata tests reject direct call, ldftn and abstract
allocation and check exact flags-read budget/ownership. Forty-two adjacent dispatch,
interface and boxing tests pass. Logs: artifacts/alchemy-abstract-slots/ac8m-*.
Initial failures found the boxing signature lookup and fault-probe oracle wrappers;
a later failure corrected expected diagnostic wording. An empty combined xUnit
filter was rerun as separate selections, not counted as validation.

Review checked emitted method lists, null/fault order, reference ownership across
allocations, shared budgets and legacy gates. Integer-object CIL v14, object IR v23,
delegate/value IR v5 and interface plan v10 express the expanded representation.
Native v49 and layout v20 remain unchanged. This is a compiler prerequisite, not an
ordinal comparer or full saved-game replay claim. AC9 remains incomplete.
