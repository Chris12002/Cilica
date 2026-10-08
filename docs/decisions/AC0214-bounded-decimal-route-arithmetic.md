# ADR AC0214: Exact bounded Decimal arithmetic and invariant text

Recorded 2026-10-08 for AC8.70, following AC8.69.

## Intent and implementation

Frozen SpellRouteSelectionInstruction.Run converts Int32 values to Decimal,
multiplies, divides by two and formats with InvariantCulture. Preserve that exact
application IL. Do not replace it with Int64/double or an application-specific
compiler rewrite. The independently reusable owned DecimalArithmetic helper uses
96 coefficient bits plus sign/scale, and fixed six-word managed scratch arrays.
Only existing integer, array, value-copy/borrow, string and exception operations
are used. The public helper DecimalParts is a validated readonly managed value;
its layout is not an external Decimal ABI.

The pinned Decimal.cs and Decimal.DecCalc.cs originals from dotnet/runtime v10.0.11
(commit 79d0c463f1b55624c874a11585f7e47731e8d675) are retained verbatim. Multiplication
uses nine UInt32 partial products with UInt64 carries, then decimal reduction and
nearest-even rounding with sticky remainders. It preserves DecCalc's distinct zero
scale/sign paths and rounding-carry reduction. Division uses one 96-bit binary
quotient/remainder pass followed by at most 56 decimal digits, bounded scaling,
nearest-even rounding, overflow unscale and conditional trailing-zero removal.
Invariant text divides the coefficient into at most 29 digits, retaining scale and
suppressing a zero sign as .NET does. No host Decimal/BigInteger calls, unsafe buffers,
platform intrinsics or floating point occur in the implementation. UInt32 compound
array updates are written as explicit loads/stores to stay within existing IL support.

System.Decimal wraps the helper through ordinary managed bodies. The supported API
is .ctor(Int32), op_Implicit(Int32), op_Multiply, op_Division and ToString(IFormatProvider).
Retain the bounded IConvertible declaration to match ToString virtual/final/newslot
flags. The exact new InvariantDecimalFormatting grant and directly produced invariant
provider are required. Logical IR admits this flag only for the selected core Decimal
value owner and exact provider signature/attributes; existing scalar lowering checks
logical IR before removing the flag as it flattens receivers. The body also rejects
other providers. Equality/hash/default text explicitly reach the unsupported marker.
Other constructors, constants (including One), operators, parsing and provider/format
variants are absent or diagnostic. Five-part construction was considered for framework
fixtures but would require Byte actual-value range exception support; retain that
negative boundary rather than approximate its exception payload. Full coefficient
coverage uses the independent DecimalParts helper instead.

CIL102/object IR62 describe the new admitted surface. Native79, interface-plan19,
production quotas, runtime and emitter are unchanged. The implementation was rebased
onto de62af0, including the other lane's d073463 constructor-chain fix; no application
source or frozen input was rebuilt. Main 48ecf14 remains an ancestor.

## Validation and review

An initial standalone managed audit passed 678,849 coefficient/sign/scale/text/fault
comparisons, including 100,000 seeded pairs and all scales for a small-coefficient
boundary grid. The final repeatable test checks 31,157 managed observations, including
10,000 seeded pairs, all 24x24 fixed cases and Int32 boundaries; faults also compare
messages. This is finite conformance evidence, not a proof for every Decimal pair.

Fifteen Decimal checks pass in the initial implementation (68.345s). The final merged
snapshot passes all 71 Decimal, numeric-provider, library-grant/request and metadata
admission checks in 72.582s. Two full-width helper roots use 84 operand pairs x 37 bit/text
positions x 3 repeats x O0/O2 = 37,296 comparisons. Five framework roots use 49 pairs x 3
repeats x O0/O2 = 1,470. Total new comparisons: 38,766. Native checks preserve ASan/UBSan,
forced collection, exact permanent roots/borrow cleanup, exact/one-short frame budgets,
2 KiB frame warnings and bounded subprocesses. Provider substitution, removed exact
grant and five unsupported API cases are negative controls. Framework roots cover
route composition, multiply/divide, copy/repeated fresh text and fault text. All 98
upstream originals and six frozen application assemblies are verified separately.

Review checked UInt64 partial-product carry bounds (each sum fits 64 bits), fixed scratch
capacity, borrow-safe allocation, 96-bit quotient overflow, scale bounds, sticky ties,
zero signs/trailing zeros, immutable value copies, exception messages and provider
authority. One test-only Decimal.One constant and an attempted five-part constructor
were outside the declared surface; those remain explicit negative tests. No generalized
Decimal, full-suite, performance or new platform claim is made.

## Frozen follow-up

The refreshed owned core is
d447d7d26b295b563e9c8ebc729a283bc6fc4b9bfbf11d1ea93adb8793e128ed;
manifest 5e991030cd2be6fbaf0cc1cdb0c46e164e31cd5bf4f1f06d2671f436a7fdd270.
With unchanged diagnostic allowances (types 3072, methods 12288, parameters 32768,
locals 12288, fields 6144, instructions 262144, metadata 4194304), engine discovery
advances to missing Enumerable.SequenceEqual at RedirectResolutionSpellEdit.CanApply
IL_0073, called from AlchemyEffectBlockHost.Resume. It charges 2,600,833 metadata units
in 10,512ms. StateLookup remains 15 methods/11,616 units in 494ms. Production probes still
stop at their 256-type allowance. Evidence: artifacts/alchemy-decimal-refresh and
artifacts/alchemy-decimal-engine; EngineStep emitted no C++; StateLookup retained its isolated C++ output. No probe output was compiled or executed natively.
Full closure admission, canonical fingerprints and saved-game replay remain unfinished.
