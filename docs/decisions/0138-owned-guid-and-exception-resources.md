# ADR 0138: Owned Guid and selected exception resources

Status: Accepted

## Decision

Adapt the pinned MIT Guid algorithms into ordinary managed IL in the owned core.
Four Int32 words preserve the numeric constructors, typed equality, XOR hashing,
unsigned field ordering and standard D/N/B/P/X text. Default readonly static
lowering supplies Empty. Packed words avoid a dependency on unsafe aliases or
native narrow-field layout; no public binary layout is promised. Hash evidence
is limited to the pinned local little-endian oracle. Retain source, resources,
license and adaptation records in library/provenance.json.

Declare the corresponding comparison/formatting interfaces with exact reference
mappings. Object overloads retain real bodies but currently fail on unsupported
value type tests. Parsing, byte/span APIs and random generation are absent and
remain actionable missing-member errors.

Extend selected exception resources with System, Arithmetic and Format.
FormatException admission is lazy so older bounded runtime fixtures need not
declare it. Its exact selected identity, layout grant, constructor grants and
SystemException ancestry are required. SystemException(string) forwards null
unchanged; ArithmeticException and FormatException substitute their resources.
The detached validator checks resource names, selected image and ancestry.
Advance exception IR to v2 and native profile to v22 for these semantics.

## Evidence and review

Seven managed probes supply 14,400 repeated native O0/O2 ASan/UBSan comparisons:
numeric field boundaries, comparison at each field, every formatted code unit,
invalid formats, provider/default behavior, and exception message identity for
null, empty, explicit and default constructors. Collection runs before every
allocation; frame exhaustion and ownership cleanup remain checked.

The full solution build passes. The composed run passes 58 checks, the preceding
argument/resource run passes 12 (overlapping Guid checks), and a subsequent check
removes each new exact grant and verifies rejection. Forged detached resource
names/ancestry and unimplemented Guid APIs also fail explicitly.

Review checked packed shifts and unchecked conversions, unsigned comparison,
managed allocation rooting, exact resource identity and constructor forwarding.
The expanded tests found and fixed ArithmeticException(null) resource behavior.
No remaining actionable issue was found. Local main remains an ancestor; no
hosted work ran. The real mapped kernel now stops at Dictionary`2; this slice
does not establish complete kernel equivalence.
