# ADR AC0228: Exact Guid string parsing

Recorded 2026-10-09 for AC8.84 after 6ec7039 (AC8.83).

Frozen InspectShieldComponentPrimitive.ParseOption 0x06000f09 IL_0029 needs
Guid.TryParseExact(String,String,out Guid). Add this exact overload to the owned
core and reuse the existing standard/expanded Guid validators with explicit
D/N/B/P/X selection. Both format cases work; format text is not trimmed. Null,
malformed, mismatched and overflowing inputs return false and reset the output.
Only a complete successful parse copies the four managed words to the output.
Span/provider parsing, ParseExact and random Guid generation remain unsupported.
No compiler/native profile, grant, runtime or production-budget changes
(CIL108/IR64/interface20/native80).

## Reference and review

The converted InspectShieldComponentPrimitive.ParseOption is in
Alchemy.Engine__7.cpp lines 27378-27457, with the exact call at 27424. SHA256:
`97384aa23802e2a960a590c4131b3d659aaba9ed854abf845c583c5d940e4521`.
Converted string/span TryParseExact starts at mscorlib__2.cpp lines 20404/20433;
SHA256 `d6edf2c52ff5c9b1d8dfccdbf7c575e253e1777da25599199a00a8914902f8f2`.
These are in the existing alchemy-reference-20261008T2312Z capture. Pinned upstream
Guid.cs lines 430-469 supplies the current .NET selection/reset contract. Cilica
reuses managed string/index operations instead of adopting Unity runtime or span
internals. Captured DLLs differ from frozen audit inputs.

Review checked null/length guards before indexing, bracket/dash selection, existing
legacy D/X grammars, output reset after late failure, allocation rooting while
writing borrowed fields/array elements and no new native undefined behavior. No
remaining findings for this bounded slice. The separate direct returned-struct to
heap-field restriction remains unchanged.

## Validation

Snapshot artifacts/alchemy-guid-exact starts at 6ec7039. All 16 distinct GuidExact
and adjacent GuidTryParse checks pass across two runs. The first run passed all
eight adjacent checks but the new fixture setup hit the known direct struct-return
to heap-field diagnostic. Materializing the setup value in a local, as in existing
Guid storage fixtures, isolates the intended out-parameter contract; all eight new
checks pass in 55.936s. No application source or frozen input changed.

There are 13,230 new native/.NET comparisons: (three roots x 136 input pairs +
90 Unicode-whitespace pairs + 840 mutations + 867 corpus/format pairs) x three
repeats x O0/O2. Cover all five formats, case, invalid format strings, mismatches,
nulls, overflow, legacy inputs, repeated resets, and object/array borrowed outputs.
ASan/UBSan, forced collection, exact cleanup/borrow counts, exact/one-short frame
budgets and 2 KiB frame warnings remain enabled. Native receipts retain sources
and executable hashes. Only the existing unrelated fixture build warning remains.
No full suite or hosted tests ran.

## Frozen scan

The initial 60-second diagnostic scan passed exact parsing and approached discovery
convergence: pending methods decreased through 137, 32 and six before timeout.
The last completed round held 15,253 bodies. This is progress, not closure admission.
A bounded 100-second isolated follow-up uses the identical binary and frozen inputs.
The follow-up finishes in 58,983ms at CIL2003: dynamic default type names exceed
the string literal budget. It charges 3,818,341 metadata units; StateLookup validates
15 methods/11,745 units in 449ms. Both emission fields are null. This is a capacity
boundary after method discovery, not full engine IR/native admission.
Core SHA256: `92758a62b697681f4c99eedc222698bc14693eef2b1f6a9275e9dc1b9a14d751`.
Manifest SHA256: `ebfaab90144307cba6d9072f54c2b9448fd36bd9479386c22466054b0791ec3e`.
All six frozen hashes remain unchanged. Exact commands, logs, diagnostic source
hashes and converged.json are in artifacts/alchemy-guid-exact-refresh and -engine.
Full closure and saved-game replay remain incomplete.
