# ADR AC0167: Boolean string parsing

Recorded 2026-10-08 for AC5a.7.

Implement Boolean.TryParse(String, out Boolean) as ordinary owned managed IL over
AC8.51 width-preserving frame borrows. No parser-specific intrinsic or host parser
is introduced. Exact mapped metadata selects the only implemented overload. Null
and invalid inputs return false and clear the output; true/false are ASCII case
insensitive with optional surrounding Unicode White_Space and NUL characters.
Trimming uses the existing classifier, moved unchanged to UnicodeWhitespace.cs
so String.IsNullOrWhiteSpace, Guid and Boolean parsing share one implementation.
No substring allocation, span, unsafe access or unchecked indexing is needed.

Pinned Boolean.cs from runtime v10.0.11 commit
79d0c463f1b55624c874a11585f7e47731e8d675 is archived with its SHA-256 and MIT
provenance. The bounded helper trims before comparing rather than using vectorized
fast paths. It uses the existing String length/index operations and explicit
output stores, avoiding borrowed values carried through stack joins. Boolean.Parse,
span overloads and narrow heap/array output locations remain diagnostic.

Four probes supply 3,660 repeated O0/O2 native/.NET comparisons with forced GC,
ASan/UBSan, frame-budget and cleanup checks. They cover 128 input selections with
both initial output values, parameter outputs, synchronous forwarding, successive
parses and argument faults before output mutation. The exact helper is also linked
into the test oracle: all UTF-16 units before/after both words and at every word
position plus the authored corpus give 852,096 managed comparisons. This exhaustive
character sweep is managed-only. All 29 selected checks pass, including existing
String whitespace, Guid/integer parsing and four public baseline checks (46 native
controls and one retained diagnostic gap).

Review checked reset timing, NUL/whitespace boundaries, ASCII case folding against
the exhaustive oracle, UTF-16 indexing bounds, private helper visibility and shared
classifier behavior. Profiles and all production/native bounds remain unchanged
from AC8.51. Evidence: artifacts/alchemy-boolean-tryparse{,-refresh,-engine}, with
core/manifest/fixture hashes and all six frozen input hashes verified.

The unchanged engine clears Boolean.TryParse and next reaches generic
ReadOnlyCollection<T>.CopyTo in List<T>.CopyEnumerable through GameState construction.
Discovery charges 1,124,897 metadata units in 4,038 ms; StateLookup remains 15 methods
and 11,527 units (440 ms). Diagnostic capacities remain types 2048, methods/locals
6144, parameters 8192, instructions 131072 and metadata 1572864, each command 60s.
No full engine C++ emission/execution, unchanged fingerprint execution or saved-game
replay closure is claimed.
