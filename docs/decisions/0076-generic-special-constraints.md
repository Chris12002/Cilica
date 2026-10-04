# ADR 0076: Scalar generic closures and special constraints

Status: accepted for bounded special constraints in the compiler-library generic
profile. Recorded 2026-10-04.

A generic closure may contain zero inline value layouts. Empty initialized root
and layout arrays form a valid catalog; missing arrays, dangling roots and unrelated
layouts remain invalid. Older metadata translator entry points retain their inline
root requirement. Scalar/reference-only generic programs now use the same checked
lowering and native emitter without invented struct types or unused payload helpers.

Closed metadata snapshots validate `class`, nonnullable `struct`, and public
parameterless-constructor constraints before admitting a method or layout.
Validation reads declaration flags and signatures, never executes a constructor.
Struct constraints accept the selected ValueType constraint row; nominal base,
interface and dependent constraints remain explicit unsupported diagnostics.
Variance, contradictory flags, malformed indices and unsupported argument ancestry
also reject. Constraint validation is charged to the caller's metadata ledger.

Nested constructed arguments are validated even when the enclosing generic method
never uses its parameter. Selected Nullable identity is checked explicitly for the
nonnullable struct constraint. Constructor checks require a nonabstract class and
an ordinary public instance constructor declaration, or a supported value argument.
This establishes constraint admission, not execution of `new T()`/Activator helpers.
Original definition ownership is checked before constructing method snapshots.

Review covered empty catalog propagation, missing/invalid constructors, nullable
arguments, nested unused constraints and exact metadata budgets. The final broad
selection passed 135 local checks including O0/O2 native probes; a final 13-check
constraint rerun added the unused nested-argument regression and tightened constructor
flags (136 distinct checks across these runs). Native scalar-only and constrained
programs agree with .NET for extreme Int32 values, run under allocation-time
collection and leave no frames, roots, borrows or arena use. The 2 KiB sanitizer
stack gate remains enabled. No hosted work ran.

Generic reference layouts/allocation and generic dispatch are not established by
this slice. Existing generic method/value gates and diagnostics remain explicit.
