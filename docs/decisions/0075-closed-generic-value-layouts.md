# ADR 0075: Closed generic value layouts

Status: accepted for unconstrained closed structs and static methods on closed
owners in the compiler-library generic profile. Recorded 2026-10-04.

Closed type identities retain their original image and TypeDef token plus a
separate argument specialization key. Field identity includes the closed owner;
reusing one FieldDef in two instantiations does not merge their storage. Box native
symbols likewise include the specialization, preserving exact unbox checks even
when two instantiations have similar layouts.

The metadata resolver substitutes closed fields, base/interface signatures and
method/field operands in the caller environment. Value discovery admits constructed
struct signatures and TypeSpec operands through its opt-in generic entry point.
The existing layout planner expands nested closed fields, derives embedded roots,
checks cycles and charges each specialization to the combined type/field/expansion
limits. Static methods on closed owners receive distinct method identities; class
allocation on generic owners and value instance constructors remain unsupported.

Generic arguments used by calls and inline type operands/signatures retain the
profile's visibility checks. Unused constrained generic arguments cannot bypass
the constraint gate. Open arguments and constraint-bearing declarations still
produce diagnostics; nested generic owners whose metadata requires broader owner
binding remain outside this profile. Primitive-only programs, constraint execution,
generic reference layouts and dispatch remain subsequent work.

Review found and corrected an older-profile diagnostic change caused by routing
all layouts through the new reader. Nongeneric catalog APIs retain their original
reader and generic rejection. Reduced fixtures cover inaccessible generic arguments,
direct recursive containment and expanding constructed layouts with inclusion paths.

All 105 selected local checks pass. Real C# tests exercise swapped Int32/reference
arguments, nested generic fields, static methods on closed struct owners, borrowed
field mutation, generic returns and boxing. O0/O2 ASan/UBSan probes compare .NET
results and null/wrong-specialization failures under allocation-time collection,
then verify original IL provenance, unchanged packed failure output and complete
root/frame/arena cleanup. The 2 KiB stack gate remains enabled. No hosted work ran.
