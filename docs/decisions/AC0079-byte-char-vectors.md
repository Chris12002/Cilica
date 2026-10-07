# ADR AC0079: Bounded Byte and Char vectors

Accepted 2026-10-07 for AC7a.1.

Binary/text library work needs byte and character arrays. Add two distinct vector
element identities to the integer-object profile, retaining the current UInt32
runtime carrier for every numeric element. Stores mask to eight or sixteen bits;
loads return unsigned Int32 values. This internal representation is not a packed
host buffer or external ABI. Bounds, allocation accounting, descriptors, exact
ownership checks and all resource limits reuse the current array runtime.

Discovery covers named newarr operands, primitive signatures, heap fields and
closed generic ldelem/stelem operands. Raw stelem.i1/i2 truncate even when no source
conversion was emitted. ldelem.u1/u2 require the exact admitted element kind;
unsupported load forms and other widths remain diagnostic. Zero arrays preserve
per-type canonical Empty instances. Generated descriptors/symbols, exact cast
messages and runtime type identities distinguish Byte, Char and Int32.

An installed .NET oracle measured array/interface type compatibility before
implementation. Byte[] satisfies IList<Byte> and IList<SByte>, but not Boolean;
Char[] satisfies IList<Char>, not UInt16 or Int16. The existing bounded assignment
closure preserves these type tests. Narrow vector interface method dispatch is
explicitly rejected by discovery and detached helper validation, pending exact
width-aware bridges. Element borrows remain rejected. There is no accidental
fallback to reference-array access or unrestricted Int32 borrowing.

Eleven probes pass 5,610 repeated O0/O2 native/.NET comparisons: stores/loads,
loops, generic copies, heap aliases across collection, Empty, type/interface tests,
GetType, cast messages and null/bounds faults. Two raw IL stores add 588 comparisons,
proving truncation without source casts. The promoted ByteArray baseline adds 294.
Altered IR rejects mismatched element identities and invalid fault edges at both
stages; model tests reject attached nominal IDs and the wrong profile. Initial
malformed-model tests failed because the altered identity was not inserted into
the fixture; the corrected setup passes without a validation relaxation.

All 142 selected tests pass (ac7a1e-tests.log, 67.883 seconds), including existing
array/interface/reference/value/empty suites and the public baseline: 35 native
controls and 12 diagnostic gaps. Native builds retain O0/O2, ASan/UBSan, forced GC,
exact cleanup, 2,048-byte frames and 30-second subprocess limits. No hosted work.
Review checked identity closure, exact descriptor checks, store masks, null/bounds
order, metadata field signatures, root tracing and unsupported dispatch/borrows.
Integer CIL v22, object layout v22, object IR v30 and native v56 record the change.

Frozen inputs are unchanged. Engine/state still stop at their snapshot/catalog
limits. Char-array String construction, UTF-8, hashing and full saved-game replay
remain unverified. Evidence: artifacts/alchemy-narrow-vectors/ac7a1b-*, ac7a1d-*,
ac7a1e-*, oracle.log and engine-and-state.json.
Core: 3b7a0e4eefb8a83156723876c7cd3f427e5cc644053a499846f5f3ab1178e686.
Manifest: ff63910d6200b4b640d5a93c31cc2bef9b66900d013e237f8ba70e5224ff1461.
