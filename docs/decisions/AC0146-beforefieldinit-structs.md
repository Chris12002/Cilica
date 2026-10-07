# ADR AC0146: BeforeFieldInit struct storage

Recorded 2026-10-07 for AC8.40.

## Evidence and decision

The committed edc6514 frozen diagnostic reaches catalog layout validation of
Alchemy.Engine.Effects.EffectTarget after 372,047 metadata units. This readonly
struct declares a static readonly None value through a BeforeFieldInit
initializer. Discovery already admits the constructor and static storage, but
the value catalog still rejects all type initializers. No engine emission or
execution occurred; StateLookup remains eight methods/9,927 units.

In the integer-object profile, admit closed BeforeFieldInit structs under the
same initializer rule as classes. Separate ordinary static storage from inline
instance fields, including the CLR size-one representation of an otherwise
empty struct. This also admits mutable static fields on structs without an
initializer. Reachable accesses still pass the existing typed storage, access,
readonly-write and initializer discovery checks. Static fields are not recursive
inline containment: a struct may safely hold a static copy of itself.

Use existing first-static-field-access initialization, exact closed owner state,
reentrant zero-state reads, retained failure identity and per-instance roots.
No initializer executes in the compiler. Strict type initializers, module
initializers, explicit layout/storage, byref-like values and static addresses
retain their existing diagnostic boundaries. Integer CIL v63 records admission;
all downstream IR/native profiles, grants and budgets remain unchanged.

## Validation and review

Six probes supply 1,764 repeated native/.NET comparisons at O0/O2 with ASan,
UBSan and forced collection: numeric/self static values, distinct closed generic
slots, empty instance layouts, recursive initialization, reference identity and
repeated failure/inner-exception identity. Structural checks verify that static
fields do not enter inline layouts and closed initializer identities differ.
Two direct native checks verify persistence across contexts, isolation across
runtime instances, exact retained reference counts and release after clearing
static roots. Strict initialization and static addresses remain negative.

The first authored generic fixture accessed an instance field directly through
a mutable static value, emitting unsupported ldsflda. Copying the complete value
before reading its field keeps this positive test inside the existing storage
contract; a separate negative retains the address boundary. Frozen input code
and game rules are unchanged.

Review checked the class/value admission symmetry, empty-layout exception,
static-versus-inline ownership, read/write authorization, generic identity,
reentrant initialization, failure retention and collection across allocations.
All 88 selected struct/static/layout checks pass, using unchanged 2,048-byte
frames and subprocess caps. Evidence: artifacts/alchemy-initialized-struct.
Local main remains ancestor 48ecf14. Full engine, unchanged fingerprint execution
and saved-game replay are still unverified.

The four public-baseline checks also pass: 92 selected checks total, with 46
public controls and one retained gap. Evidence: baseline.log.
