# ADR AC0106: Bounded constant-array data

Recorded 2026-10-07 for AC7a.3.

## Decision

The frozen engine's ZombieSiege initializer requires the C# compiler-generated
constant/newarr/dup/ldtoken-field/RuntimeHelpers.InitializeArray sequence. Admit
this exact straight-line pattern with no incoming branch or exception-region
boundary after its constant. The allocated array cannot escape before initialization.
Preserve the allocation and its fault order, consume the compile-time-only handle,
and lower each decoded element to the existing checked scalar array store. No new
IR/native memory primitive or runtime field-handle identity is introduced.

Map the exact owned bodyless public helper and require its separate authenticated
StaticArrayInitialization grant. RuntimeFieldHandle is only a declaration operand;
general construction/transport and arbitrary InitializeArray calls remain diagnostic.
Read only same-image internal static readonly RVA fields. Storage is an integer
carrier or a fieldless, nongeneric nested explicit-layout value type with packing
one and the selected ValueType base. Source visibility checks remain active.
Require the entire declared storage within a file-backed PE section and sufficient
bytes before reading any payload. Decode little-endian bytes explicitly, independent
of the compiler host. Malformed data produces diagnostics, never input execution.

Admit Byte, Char, Int32 and Int64 arrays with 1–256 constant elements and at most
2,048 storage bytes. Payload bytes charge the shared metadata work budget; expanded
stores charge the existing IR statement budget. Resource limits are unchanged.
Integer CIL advances to v34. Native v61, layout v23, object IR v34 and interface
plan v13 are unchanged. Other widths, open/escaping handles, dynamic sizes and
other initialization patterns remain unsupported.

## Validation and review

Eight probes provide 2,352 repeated native/.NET comparisons at O0/O2: integer
extremes, all 64 bits, bytes, UTF-16 including isolated surrogates, mutation,
independent arrays sharing one data field, branch-selected data, bounds exceptions
and retained static arrays. Forced GC, ASan/UBSan, 2,048-byte native frames and
30-second subprocess caps remain enabled. General runtime handles stay diagnostic.

Six mutated inputs reject missing/out-of-section RVA, writable fields, undersized
layout, negative constant count and entry into the middle of initialization. The
last test preserves stack depth to exercise the pattern admission guard. Removing
the helper grant also rejects the program. Adjacent selected tests cover core
manifest authentication, runtime type identity and all public baseline probes.
The baseline now has 43 native controls and four diagnostic gaps.

Early focused runs exposed implicit IL constant decoding, named primitive operands
and Roslyn's integer carrier for small data blobs; all are corrected. One negative
initially failed at an earlier stack-depth guard and was strengthened to preserve
that depth. The final regression run found only a missing ledger slice entry,
subsequently corrected. Review checked declaration authentication, source access,
PE range/size arithmetic, bit preservation, control-flow exclusion, fault ordering,
GC roots and resource accounting. No application source/rules were changed.
Local main 48ecf14 remains an ancestor. Full engine/replay is unfinished.

Evidence: artifacts/alchemy-static-array-data, tests-d.log, tests-e.log and final
ledger validation. Production remains 256 types/512 methods/65,536 metadata units.
Core: 9424be4adf2770220ab4960957eb422bae3a785eea568030e91adc14a4d992e2.
Manifest: 5b02430733995370db4ccc283308488cced01506e3e937f519fb133a5668b6bc.
Fixture: b29997ed48d70cc1391eb43e6b6f98011e7e7fa7f0d8e91c5f4d09b855cca516.
