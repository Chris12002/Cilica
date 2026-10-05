# ADR 0175: Constructor control flow after a validated entry prefix

Accepted 2026-10-06; milestone 6c.60.

The kernel uses readonly property assignments with null-coalescing throws. C#
keeps the direct receiver on the evaluation stack across a branch. Preserve that
provenance by re-reading argument zero only for integer-object class constructors
whose receiver argument slot is never addressed; direct starg.0 is already rejected.
The existing field validator still requires the constructor's own direct receiver.

Replace the blanket one-block constructor restriction in this profile with a
narrow prefix rule: the entry block must initialize the immediate base exactly
once, following existing pre-base escape rules. Other blocks may not invoke base
constructors, return to entry, or catch failures from a try region covering entry.
All normal body paths therefore follow successful initialization. This avoids a
general uninitialized-object dataflow extension while supporting ordinary C# body
branches, throws and subsequent loops. Object operations IR v14 records the change.

O0/O2 native tests compare readonly null-coalescing fields, exception order and
reference survival under forced GC (294 repeated .NET comparisons). A constructor
that takes the address of argument zero before a joined readonly store is rejected;
reloading that slot would not preserve a previously captured receiver. Detached
re-entry and repeated-base-call mutations fail validation, as do existing missing
base and uninitialized receiver escape tests. The kernel next reaches cast-name
resource validation, so milestone native equivalence remains unproven.
