# Distinct Boolean vectors over existing narrow scalar storage

Recorded 2026-10-08 for compatibility batch 004.

Pinned DiffPlex selected roots allocate Boolean vectors and use Array.Empty<bool>.
Extend the existing Byte/Char array pipeline with ObjectArrayElement.Boolean,
appended to the element-kind enum to preserve existing numeric identities. Treat it
as a narrow Int32-carried element for validation, storage and native emission.
Stores mask to 255, never normalize to zero/one: CLI raw Boolean storage can contain
noncanonical low bytes. ldelem.u1 and exact generic ldelem/stelem preserve those bits.
Signed loads, wider stores/loads and element addresses retain diagnostics.

Boolean vectors have distinct descriptors/symbols, runtime types and cast names.
No Boolean/Byte array covariance or signed-byte interface alias is introduced.
Generic interface type tests admit only the exact Boolean element. Existing narrow
metadata helper recipes use that same identity for Count, flags and SyncRoot;
interface element access/copy/enumeration/clear remains explicitly unsupported.
Both detached stages keep the existing exact array and fault validation.

Signatures, heap fields, allocation, Empty, static initialization and generic
operands share the same element identity. Static field bytes decode as unsigned
bytes under existing declaration/data/size/grant checks. Runtime backing remains
UInt32 slots and existing array quotas, allocation/root tracing and ownership;
no packed host buffer, new intrinsic grant or external ABI is added.

Integer CIL v83, object IR v54, object layout v27 and native v76 record admission,
identity and emitted store semantics. Existing production limits remain unchanged.
Review checked every element-kind branch, same-width opcode matching without
nominal type aliasing, unsigned truncation, exact generic metadata, descriptor
uniqueness, null/bounds/evaluation order, retained roots and cached empty ownership.

21 focused and 62 adjacent checks pass. Authored raw IL compares noncanonical
Boolean bytes with normal .NET; malformed models/IR and unsupported operations
remain rejected. Raw harness inputs are chunked to preserve the unchanged 2KiB
stack cap. See [batch results](../compatibility-candidates/batch-004-results.md).
DiffPlex still has an ignore-case comparer gate; full candidate execution remains
unverified.
