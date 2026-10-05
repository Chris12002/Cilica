# ADR 0176: Generated type names in cast exception messages

Accepted 2026-10-06; milestone 6c.61.

The concrete kernel includes ResolutionFrame+<>c, a normal C# compiler-generated
type. Cast resource generation rejected angle brackets even though type display
names are emitted as UTF-16 data. Admit < and > alongside the already supported
nominal/nested/generic name characters. Keep the 4,096-character individual name
limit and the existing aggregate message budget; do not change generic naming or
claim support for arbitrary reflection escape syntax. Diagnostics include at most
160 characters of the rejected name.

A closed anonymous-type cast probe hashes every UTF-16 message code unit and matches
.NET under O0/O2 sanitizers and forced collection (294 repeated comparisons). The
real kernel now passes logical and scalar validation and native C++ emission.
Transcript execution and final milestone closure review remain separate gates.
