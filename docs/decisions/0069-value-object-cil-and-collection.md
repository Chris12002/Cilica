# ADR 0069: Value/class CIL and live-reference collection

Status: accepted for bounded compiler-library translation and local native probes.
Recorded 2026-10-04.

`ValueProgramDiscovery.ReadObjects` and `ValueTranslator.TranslateObjects` select
the explicit `value-objects-cil-v1` profile. It composes static struct methods with
nominal class/Object signatures, class `newobj`, direct base-constructor calls,
inherited scalar/reference fields and checked scalar heap-field borrows. The
Int32 vector operations of ADR 0067 remain available. Entry parameters and return
remain Int32; existing CLI profiles are unchanged.

Discovery authenticates constructor declarations, selected Object/ValueType
identities, member access and enclosing-type visibility. Classification snapshots
and layout discovery share the metadata-member ledger. Cached snapshots still
consume the catalog's combined type/field limits; caching cannot bypass a budget.
The shared object verifier enforces immediate-base initialization, uninitialized
receiver restrictions and readonly writes through the declaring constructor's
receiver. Both source IR and expanded scalar IR are checked.

Constructor bodies in this gate require scalar/reference operations, including
their parameters, locals, stack inputs and computed values. Review exposed why a
parameter-only restriction was insufficient: a struct-returning helper before
base initialization could introduce hidden output locations during lowering.
Value/borrow flow in constructors is now rejected before expansion, with a reduced
regression. Struct copies, borrows and returns remain available in static methods.
Ordinary instance methods, dispatch, string intrinsics, inline heap values, boxing,
generics and managed handlers remain separate capabilities.

Lowering reuses ADR 0068 and the existing native scalar-location backend. No new
allocator, collector, constructor bypass or public struct ABI is introduced.
O0/O2 ASan/UBSan probes translate real C# with allocations inside base and derived
constructors. Collection before every allocation preserves a six-object graph
through nested struct copies, borrowed replacement, sole callee-parameter roots
and caller-owned returns. A successful composed call makes 33 allocations under
a 16-object cap; clearing its final root permits full reclamation.

Native probes compare arithmetic and division faults with .NET, retain original
constructor IL fault locations and leave packed output unchanged on failure.
Exact heap/arena capacity succeeds; one byte less fails cleanly. Nested constructor
allocation failure unwinds all roots, borrows and frames, and the same context is
reusable. The 2 KiB native stack gate remains enabled.

The final focused verifier/metadata suite passed 256 checks, following 66 initial
CIL checks and two native configurations (these groups overlap). Review found no
remaining blocking issues. This completes the bounded class/value composition
gate, not milestone 5; boxing is next. No hosted work ran.
