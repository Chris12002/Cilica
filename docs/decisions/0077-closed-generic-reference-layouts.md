# ADR 0077: Closed generic reference layouts and construction

Status: accepted for bounded concrete class specialization in the compiler-library
generic profile. Recorded 2026-10-04.

The existing object catalog builder now has an internal closed-type entry point.
It substitutes class fields and base types, retains specialization in field/type
identities and constructs one detached layout per closed instance. Nongeneric
public entry points retain their previous rejection. Closed identity construction
lives with object identity rather than depending on the value layout builder.

Value discovery admits closed class references and exact constructors. Constructor
receivers, base calls, readonly writes and inherited fields use the existing
source/target verification. Struct parameters or local value/borrow flow in
constructors remain rejected by the earlier constructor gate. Closed generic class
fields may use the existing scalar/reference storage kinds; inline struct fields
inside classes remain unsupported. Generic interfaces and generic dispatch are
also outside this slice.

The shared metadata ledger and combined layout budgets include each specialization.
Recursive references are legal and trace precisely; base cycles reject. Expanding
reference graphs fail within signature/type budgets and retain the layout inclusion
chain. Closed reference snapshots preserve constraint and visibility checks.

Real C# probes compose Int32 and reference instances of a generic base/derived
hierarchy, readonly constructor fields, cyclic references, generic struct returns,
boxing, mutable inherited-field borrows and allocation in constructors. Native
O0/O2 ASan/UBSan results match .NET for extreme arithmetic and fault cases under
collection before allocation. Allocation failure inside a constructor leaves no
frames, roots, borrows or arena use and permits context reuse; final collection
reclaims the cycles. Packed failure output and original IL provenance are checked.

Review checked closed owner equality, field uniqueness, reference maps, constructor
initialization, metadata accounting and preservation of nongeneric behavior. The
43-check initial selection and 116-check shared/native selection passed, followed
by a 114-check review selection and five focused checks for inclusion diagnostics;
these selections overlap. No blocking findings remain. The 2 KiB native stack gate
remains enabled, the compiler builds cleanly and no hosted work ran.
